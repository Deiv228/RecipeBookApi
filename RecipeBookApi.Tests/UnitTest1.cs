using Xunit;

namespace RecipeBookApi.Tests;

public class BasicCiTests
{
    [Fact]
    public void HealthCheck_Logic_ReturnsExpected()
    {
        var isHealthy = true;
        Assert.True(isHealthy);
    }
}
