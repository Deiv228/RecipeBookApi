namespace RecipeBookApi.Domain;

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    public User(string username, string email)
    {
        Id = Guid.NewGuid();
        Username = username;
        Email = email;
    }
}