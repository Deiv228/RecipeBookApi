using RecipeBookApi.Domain;

var builder = WebApplication.CreateBuilder(args);

// Налаштування API Explorer та CORS
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// In-Memory сховище доменних об'єктів для розширеного CRUD у Лабораторній №4
var recipes = new List<Recipe>();

// GET: Отримати всі рецепти
app.MapGet("/recipes", () => Results.Ok(recipes));

// GET: Отримати рецепт за ID
app.MapGet("/recipes/{id:guid}", (Guid id) =>
{
    var recipe = recipes.FirstOrDefault(r => r.Id == id);
    return recipe is not null ? Results.Ok(recipe) : Results.NotFound();
});

// POST: Створити новий рецепт
app.MapPost("/recipes", (CreateRecipeRequest req) =>
{
    var recipe = new Recipe(req.Title, req.Description, req.Instructions, req.CookingTimeMinutes, Guid.NewGuid());

    if (req.Ingredients is not null)
    {
        foreach (var ing in req.Ingredients)
        {
            recipe.AddIngredient(new Ingredient(ing.Name, ing.Amount, ing.Unit));
        }
    }

    recipes.Add(recipe);
    return Results.Created($"/recipes/{recipe.Id}", recipe);
});

// PUT: Оновити рецепт
app.MapPut("/recipes/{id:guid}", (Guid id, UpdateRecipeRequest req) =>
{
    var recipe = recipes.FirstOrDefault(r => r.Id == id);
    if (recipe is null) return Results.NotFound();

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

// DELETE: Видалити рецепт
app.MapDelete("/recipes/{id:guid}", (Guid id) =>
{
    var recipe = recipes.FirstOrDefault(r => r.Id == id);
    if (recipe is null) return Results.NotFound();

    recipes.Remove(recipe);
    return Results.NoContent();
});

app.Run();

// DTO для запитів
public record CreateRecipeRequest(
    string Title,
    string Description,
    string Instructions,
    int CookingTimeMinutes,
    List<IngredientRequest>? Ingredients
);

public record UpdateRecipeRequest(
    string Title,
    string Description,
    string Instructions,
    int CookingTimeMinutes,
    List<IngredientRequest>? Ingredients
);

public record IngredientRequest(string Name, double Amount, string Unit);