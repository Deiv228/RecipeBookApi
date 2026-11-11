namespace RecipeBookApi.Domain;

public class Ingredient
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public double Amount { get; private set; }
    public string Unit { get; private set; } = string.Empty;

    public Ingredient(string name, double amount, string unit)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва інгредієнта не може бути порожньою.");

        Id = Guid.NewGuid();
        Name = name;
        Amount = amount;
        Unit = unit;
    }
}