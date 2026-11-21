using CustomPresenter.Application.Live;
using CustomPresenter.Infrastructure.ObsServer;

namespace CustomPresenter.IntegrationTests.Builders;

internal sealed class ObsServerTestBuilder
{
    private int _port;

    public ObsServerTestBuilder WithPort(int port)
    {
        _port = port;
        return this;
    }

    public (ObsWebServer Server, LiveStateManager Manager, ChannelLiveBroadcaster Broadcaster) Build()
    {
        var broadcaster = new ChannelLiveBroadcaster();
        var manager = new LiveStateManager([broadcaster]);
        var options = _port == 0 ? ObsServerOptions.Ephemeral() : ObsServerOptions.WithPort(_port);
        var server = new ObsWebServer(options, manager, broadcaster);
        return (server, manager, broadcaster);
    }
}
