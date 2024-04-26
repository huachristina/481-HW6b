using System.Threading.Tasks;
using Xunit;
using Deceive; // Replace with your actual namespace where StartupHandler is located

public class ApplicationStartupTests
{
    [Fact]
    public async Task StartupSequence_CompletesSuccessfully()
    {
        // Arrange
        var startupHandler = new StartupHandler();

        // Act
        var result = await startupHandler.StartApplicationAsync();

        // Assert
        Assert.True(result, "The application failed to start properly.");
    }
}