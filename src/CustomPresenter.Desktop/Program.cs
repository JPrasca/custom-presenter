using CustomPresenter.Application.Live;

namespace CustomPresenter.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var liveManager = new LiveStateManager([]);
        _ = liveManager.Snapshot();
    }
}
