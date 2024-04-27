public class MainControllerTests
{
    private readonly Mock<TcpListener> _mockTcpListener;
    private readonly Mock<TcpClient> _mockTcpClient;
    private readonly Mock<SslStream> _mockSslIncoming;
    private readonly Mock<SslStream> _mockSslOutgoing;
    private MainController _controller;

    public MainControllerTests()
    {
        _mockTcpListener = new Mock<TcpListener>(IPAddress.Loopback, 0);
        _mockTcpClient = new Mock<TcpClient>();
        _mockSslIncoming = new Mock<SslStream>(new MemoryStream(), true);
        _mockSslOutgoing = new Mock<SslStream>(new MemoryStream(), true);

        _mockTcpListener.Setup(x => x.AcceptTcpClientAsync()).ReturnsAsync(_mockTcpClient.Object);
        _mockTcpClient.Setup(x => x.GetStream()).Returns(new MemoryStream());
        _controller = new MainController();
    }

    [Fact]
    public async Task ServeClientsAsync_SuccessfulConnection()
    {
        // Arrange
        var cert = new X509Certificate2();
        _mockSslIncoming.Setup(x => x.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12, false)).Returns(Task.CompletedTask);
        _mockSslOutgoing.Setup(x => x.AuthenticateAsClientAsync("chatHost")).Returns(Task.CompletedTask);

        // Act
        await _controller.StartServingClients(_mockTcpListener.Object, "chatHost", 1234);

        // Assert
        _mockSslIncoming.Verify(x => x.AuthenticateAsServerAsync(It.IsAny<X509Certificate>(), It.IsAny<bool>(), It.IsAny<SslProtocols>(), It.IsAny<bool>()), Times.Once());
        _mockSslOutgoing.Verify(x => x.AuthenticateAsClientAsync(It.IsAny<string>()), Times.Once());
    }
    public async Task ServeClientsAsync_RetryConnection()
    {
        // Arrange
        int attempts = 0;
        _mockTcpClient.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(() =>
            {
                if (attempts++ < 2)
                    throw new SocketException();
                return Task.CompletedTask;
            });

        // Act
        await _controller.StartServingClients(_mockTcpListener.Object, "chatHost", 1234);

        // Assert
        Assert.Equal(3, attempts); // Ensure three attempts were made
    }

    public async Task ServeClientsAsync_FailureAfterMaxRetries()
    {
        // Arrange
        _mockTcpClient.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Throws(new SocketException());

        // Act & Assert
        await Assert.ThrowsAsync
    }

    public async Task ApplicationLifecycleTest()
    {
        // Simulate starting the application
        _controller.LoadStatus();
        _controller.UpdateTray();
        await _controller.StartServingClients(_mockTcpListener.Object, "chatHost", 1234);

        // Simulate user interactions
        _controller.HandleChatMessage("online");
        _controller.HandleChatMessage("offline");

        // Assert status updates
        Assert.Equal("offline", _controller.Status);

        // Simulate shutdown
        _controller.Dispose();
        Assert.Empty(_controller.Connections); // Verify all connections were cleaned up
    }
}

