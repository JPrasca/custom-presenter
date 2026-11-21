using System.Drawing;
using System.Text.Json;
using CustomPresenter.Domain.Display;
using CustomPresenter.Domain.Live;
using Photino.NET;

namespace CustomPresenter.Desktop.Display;

/// <summary>
/// Adaptador Photino.NET para la ventana de proyección nativa (estrictamente solo lectura).
/// </summary>
public sealed class PhotinoProjectorWindowManager : IProjectorWindowManager
{
    private const int WindowedWidth = 960;
    private const int WindowedHeight = 540;
    private const string CloseMessageToken = "projector:close";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Lock _sync = new();
    private readonly string _projectorUrl;
    private PhotinoWindow? _window;

    public PhotinoProjectorWindowManager(string projectorUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorUrl);
        _projectorUrl = projectorUrl;
    }

    /// <summary>
    /// La ventana projector nunca expone handlers IPC de escritura ni acceso a disco.
    /// </summary>
    public bool AllowsWriteIpcHandlers => false;

    public bool IsOpen
    {
        get
        {
            lock (_sync)
            {
                return _window is not null;
            }
        }
    }

    public MonitorInfo? ActiveMonitor { get; private set; }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var probe = new PhotinoWindow();
        var monitors = probe.Monitors;
        if (monitors is null || monitors.Count == 0)
        {
            return [];
        }

        return monitors
            .Select((m, index) => new MonitorInfo(
                $"mon-{index + 1}",
                $"Monitor {index + 1} ({m.MonitorArea.Width}x{m.MonitorArea.Height})",
                m.MonitorArea.X,
                m.MonitorArea.Y,
                Math.Max(1, m.MonitorArea.Width),
                Math.Max(1, m.MonitorArea.Height),
                index == 0))
            .ToList();
    }

    public Task OpenAsync(
        MonitorInfo targetMonitor,
        ProjectorDisplayMode displayMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetMonitor);
        targetMonitor.Validate();

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        PhotinoWindow? previous;
        lock (_sync)
        {
            previous = _window;
            _window = null;
            ActiveMonitor = null;
        }

        previous?.Close();

        var window = new PhotinoWindow()
            .SetTitle("Custom Presenter — Proyector");

        if (displayMode == ProjectorDisplayMode.Windowed)
        {
            window
                .SetChromeless(false)
                .SetFullScreen(false)
                .SetResizable(true)
                .SetSize(WindowedWidth, WindowedHeight)
                .Center();
        }
        else
        {
            window
                .SetChromeless(true)
                .SetLocation(new Point(targetMonitor.X, targetMonitor.Y))
                .SetSize(targetMonitor.Width, targetMonitor.Height)
                .SetFullScreen(true);
        }

        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            if (string.Equals(message, CloseMessageToken, StringComparison.Ordinal)
                && sender is PhotinoWindow target)
            {
                HandleExternalClose();
                target.Close();
            }
        });

        window.RegisterWindowClosingHandler((_, _) =>
        {
            HandleExternalClose();
            return false;
        });

        window.Load(_projectorUrl);

        lock (_sync)
        {
            _window = window;
            ActiveMonitor = targetMonitor;
        }

        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        PhotinoWindow? current;
        lock (_sync)
        {
            current = _window;
            _window = null;
            ActiveMonitor = null;
        }

        current?.Close();
        return Task.CompletedTask;
    }

    public ValueTask SendLiveStateAsync(
        LiveState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        PhotinoWindow? current;
        lock (_sync)
        {
            current = _window;
        }

        if (current is null)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            current.SendWebMessage(json);
        }
        catch (ObjectDisposedException)
        {
            HandleExternalClose();
        }
        catch (InvalidOperationException)
        {
            HandleExternalClose();
        }

        return ValueTask.CompletedTask;
    }

    private void HandleExternalClose()
    {
        lock (_sync)
        {
            _window = null;
            ActiveMonitor = null;
        }
    }
}
