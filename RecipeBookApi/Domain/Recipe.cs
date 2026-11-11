namespace RecipeBookApi.Domain;

public class Recipe
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Instructions { get; private set; } = string.Empty;
    public int CookingTimeMinutes { get; private set; }
    public Guid AuthorId { get; private set; }

    private readonly List<Ingredient> _ingredients = new();
    public IReadOnlyCollection<Ingredient> Ingredients => _ingredients.AsReadOnly();

    public Recipe(string title, string description, string instructions, int cookingTimeMinutes, Guid authorId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Назва рецепта обов'язкова.");

        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        Instructions = instructions;
        CookingTimeMinutes = cookingTimeMinutes;
        AuthorId = authorId;
    }

    public void AddIngredient(Ingredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        _ingredients.Add(ingredient);
    }
}