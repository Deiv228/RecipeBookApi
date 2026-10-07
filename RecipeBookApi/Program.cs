using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

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

const string connectionString = "Data Source=recipes.db";

using (var connection = new SqliteConnection(connectionString))
{
    connection.Open();

    var createTableCmd = connection.CreateCommand();
    createTableCmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS items (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL
        );
    ";
    createTableCmd.ExecuteNonQuery();

    var checkCmd = connection.CreateCommand();
    checkCmd.CommandText = "SELECT COUNT(*) FROM items;";
    long count = (long)checkCmd.ExecuteScalar()!;

    if (count == 0)
    {
        var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO items (name) VALUES ('Борщ український');
            INSERT INTO items (name) VALUES ('Паста Карбонара');
            INSERT INTO items (name) VALUES ('Сирники з джемом');
        ";
        insertCmd.ExecuteNonQuery();
    }
}

app.MapGet("/items", () =>
{
    var items = new List<object>();

    using (var connection = new SqliteConnection(connectionString))
    {
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM items;";

        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                items.Add(new
                {
                    id = reader.GetInt32(0),
                    name = reader.GetString(1)
                });
            }
        }
    }

    return Results.Ok(items);
});

app.Run();