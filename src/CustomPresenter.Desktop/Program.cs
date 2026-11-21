using CustomPresenter.Application.Live;
using CustomPresenter.Infrastructure.ObsServer;

namespace CustomPresenter.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        using var obsServerTask = StartObsServerAsync(liveManager, broadcaster);
        obsServerTask.GetAwaiter().GetResult();
    }

    private static async Task StartObsServerAsync(
        LiveStateManager liveManager,
        ChannelLiveBroadcaster broadcaster)
    {
        await using var obsServer = new ObsWebServer(
            new ObsServerOptions(),
            liveManager,
            broadcaster);
        await obsServer.StartAsync().ConfigureAwait(false);
        _ = liveManager.Snapshot();
    }
}
