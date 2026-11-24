using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using RecipeBookApi.Domain;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Request-Id", "Retry-After");
    });
});
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("recipebook-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri("http://localhost:4318/v1/traces");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri("http://localhost:4318/v1/metrics");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("recipebook-api"));
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(opt =>
    {
        opt.Endpoint = new Uri("http://localhost:4318/v1/logs");
        opt.Protocol = OtlpExportProtocol.HttpProtobuf;
    });
});

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

var lokiClient = new HttpClient();
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    sw.Stop();

    var traceId = Activity.Current?.TraceId.ToString() ?? "";
    var nsTimestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L).ToString();
    var logMsg = JsonSerializer.Serialize(new
    {
        ts = DateTime.UtcNow.ToString("o"),
        level = context.Response.StatusCode >= 500 ? "error" : "info",
        service = "recipebook-api",
        method = context.Request.Method,
        route = context.Request.Path.Value,
        status = context.Response.StatusCode,
        duration_ms = sw.ElapsedMilliseconds,
        trace_id = traceId,
        msg = "http request complete"
    });

    var payload = new
    {
        streams = new[]
        {
            new
            {
                stream = new Dictionary<string, string>
                {
                    ["service"] = "recipebook-api",
                    ["exporter"] = "OTLP"
                },
                values = new[] { new[] { nsTimestamp, logMsg } }
            }
        }
    };

    try
    {
        await lokiClient.PostAsJsonAsync("http://localhost:3100/loki/api/v1/push", payload);
    }
    catch
    {
        // Ігноруємо помилки мережі, якщо Loki тимчасово недоступний
    }
});

var recipes = new List<Recipe>();
var idempotencyStore = new ConcurrentDictionary<string, Recipe>();
var rateLimitStore = new ConcurrentDictionary<string, (int Count, DateTime WindowStart)>();

const int WindowSeconds = 10;
const int MaxRequestsPerWindow = 8;

app.Use(async (context, next) =>
{
    var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(requestId))
    {
        requestId = Guid.NewGuid().ToString();
    }

    context.Items["RequestId"] = requestId;
    context.Response.Headers["X-Request-Id"] = requestId;

    await next();
});

app.Use(async (context, next) =>
{
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "local";
    var now = DateTime.UtcNow;

    var state = rateLimitStore.AddOrUpdate(
        ip,
        _ => (1, now),
        (_, current) => (now - current.WindowStart).TotalSeconds < WindowSeconds
            ? (current.Count + 1, current.WindowStart)
            : (1, now)
    );

    if (state.Count > MaxRequestsPerWindow)
    {
        context.Response.Headers["Retry-After"] = "2";
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.Response.WriteAsJsonAsync(new ApiError(
            Error: "too_many_requests",
            Code: "RATE_LIMIT_EXCEEDED",
            Details: $"Перевищено ліміт: макс. {MaxRequestsPerWindow} запитів за {WindowSeconds} сек.",
            RequestId: context.Items["RequestId"]?.ToString() ?? ""
        ));
        return;
    }

    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Headers["X-No-Chaos"] != "true")
    {
        var r = Random.Shared.NextDouble();

        if (r < 0.15)
        {
            var delayMs = Random.Shared.Next(1200, 2000);
            await Task.Delay(delayMs);
        }
        else if (r > 0.80)
        {
            var isUnavailable = Random.Shared.NextDouble() < 0.5;
            var statusCode = isUnavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
            var errText = isUnavailable ? "unavailable" : "unexpected";

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(new ApiError(
                Error: errText,
                Code: isUnavailable ? "SERVICE_UNAVAILABLE" : "INTERNAL_ERROR",
                Details: "Симуляція тимчасового збою сервера.",
                RequestId: context.Items["RequestId"]?.ToString() ?? ""
            ));
            return;
        }
    }

    await next();
});


app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/recipes", () => Results.Ok(recipes));

app.MapGet("/recipes/{id:guid}", (Guid id, HttpContext ctx) =>
{
    var recipe = recipes.FirstOrDefault(r => r.Id == id);
    if (recipe is null)
    {
        return Results.NotFound(new ApiError(
            Error: "not_found",
            Code: "RECIPE_NOT_FOUND",
            Details: $"Рецепт з ID {id} не знайдено.",
            RequestId: ctx.Items["RequestId"]?.ToString() ?? ""
        ));
    }
    return Results.Ok(recipe);
});

app.MapPost("/recipes", (CreateRecipeRequest req, HttpContext ctx) =>
{
    var requestId = ctx.Items["RequestId"]?.ToString() ?? "";
    var idemKey = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(idemKey))
    {
        return Results.BadRequest(new ApiError(
            Error: "idempotency_key_required",
            Code: "MISSING_IDEMPOTENCY_KEY",
            Details: "Заголовок Idempotency-Key є обов'язковим для POST запитів.",
            RequestId: requestId
        ));
    }

    if (idempotencyStore.TryGetValue(idemKey, out var existingRecipe))
    {
        return Results.Created($"/recipes/{existingRecipe.Id}", existingRecipe);
    }

    var recipe = new Recipe(req.Title, req.Description, req.Instructions, req.CookingTimeMinutes, Guid.NewGuid());
    if (req.Ingredients is not null)
    {
        foreach (var ing in req.Ingredients)
        {
            recipe.AddIngredient(new Ingredient(ing.Name, ing.Amount, ing.Unit));
        }
    }

    if (idempotencyStore.TryAdd(idemKey, recipe))
    {
        recipes.Add(recipe);
    }

    return Results.Created($"/recipes/{recipe.Id}", idempotencyStore[idemKey]);
});

app.MapPut("/recipes/{id:guid}", (Guid id, UpdateRecipeRequest req, HttpContext ctx) =>
{
    var recipe = recipes.FirstOrDefault(r => r.Id == id);
    if (recipe is null)
    {
        return Results.NotFound(new ApiError(
            Error: "not_found",
            Code: "RECIPE_NOT_FOUND",
            Details: $"Рецепт з ID {id} не знайдено.",
            RequestId: ctx.Items["RequestId"]?.ToString() ?? ""
        ));
    }

    recipes.Remove(recipe);
    var updated = new Recipe(req.Title, req.Description, req.Instructions, req.CookingTimeMinutes, recipe.AuthorId);
    if (req.Ingredients is not null)
    {
        foreach (var ing in req.Ingredients)
        {
            updated.AddIngredient(new Ingredient(ing.Name, ing.Amount, ing.Unit));
        }
    }

    recipes.Add(updated);
    return Results.NoContent();
});

app.MapDelete("/recipes/{id:guid}", (Guid id, HttpContext ctx) =>
{
    var recipe = recipes.FirstOrDefault(r => r.Id == id);
    if (recipe is null)
    {
        return Results.NotFound(new ApiError(
            Error: "not_found",
            Code: "RECIPE_NOT_FOUND",
            Details: $"Рецепт з ID {id} не знайдено.",
            RequestId: ctx.Items["RequestId"]?.ToString() ?? ""
        ));
    }

    recipes.Remove(recipe);
    return Results.NoContent();
});

app.Run();

public record ApiError(string Error, string? Code, string? Details, string RequestId);
public record CreateRecipeRequest(string Title, string Description, string Instructions, int CookingTimeMinutes, List<IngredientRequest>? Ingredients);
public record UpdateRecipeRequest(string Title, string Description, string Instructions, int CookingTimeMinutes, List<IngredientRequest>? Ingredients);
public record IngredientRequest(string Name, double Amount, string Unit);