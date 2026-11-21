using System.Net;
using CustomPresenter.Infrastructure.ObsServer;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class ObsServerOptionsTests
{
    [Fact]
    public void OpcionesPorDefectoUsanPuerto47321YLoopback()
    {
        // Arrange & Act
        var options = new ObsServerOptions();

        // Assert
        Assert.Equal(ObsServerOptions.DefaultPort, options.Port);
        Assert.Equal(47321, options.Port);
        Assert.Equal(IPAddress.Loopback, options.BindAddress);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(47321)]
    [InlineData(65535)]
    public void WithPortAceptaPuertosValidosEnRangoUsuario(int port)
    {
        // Arrange & Act
        var options = ObsServerOptions.WithPort(port);

        // Assert
        Assert.Equal(port, options.Port);
        Assert.Equal(IPAddress.Loopback, options.BindAddress);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(80)]
    [InlineData(1023)]
    [InlineData(65536)]
    public void WithPortConPuertoFueraDeRangoLanzaArgumentOutOfRangeException(int invalidPort)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ObsServerOptions.WithPort(invalidPort));
    }

    [Fact]
    public void ForContainerUsaAnyYValidaPuerto()
    {
        // Arrange & Act
        var options = ObsServerOptions.ForContainer(47321);

        // Assert
        Assert.Equal(IPAddress.Any, options.BindAddress);
        Assert.Equal(47321, options.Port);
        Assert.Throws<ArgumentOutOfRangeException>(() => ObsServerOptions.ForContainer(80));
    }

    [Fact]
    public void FromEnvironmentActivaAnySoloConVariableExplicita()
    {
        // Arrange & Act
        var local = ObsServerOptions.FromEnvironment(null, null);
        var container = ObsServerOptions.FromEnvironment("true", "48000");

        // Assert
        Assert.Equal(IPAddress.Loopback, local.BindAddress);
        Assert.Equal(ObsServerOptions.DefaultPort, local.Port);
        Assert.Equal(IPAddress.Any, container.BindAddress);
        Assert.Equal(48000, container.Port);
        Assert.Throws<ArgumentOutOfRangeException>(() => ObsServerOptions.FromEnvironment("true", "80"));
    }
}
