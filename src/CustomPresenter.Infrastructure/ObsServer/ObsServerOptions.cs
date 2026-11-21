using System.Net;

namespace CustomPresenter.Infrastructure.ObsServer;

/// <summary>
/// Configuración del micro-servidor local para OBS Studio.
/// </summary>
public sealed class ObsServerOptions
{
    public const int DefaultPort = 47321;
    public const int MinUserPort = 1024;
    public const int MaxPort = 65535;

    public ObsServerOptions()
        : this(DefaultPort)
    {
    }

    private ObsServerOptions(int port)
    {
        Port = port;
    }

    /// <summary>
    /// Dirección de enlace estricta en loopback (127.0.0.1).
    /// </summary>
    public IPAddress BindAddress { get; } = IPAddress.Loopback;

    public int Port { get; }

    /// <summary>
    /// Crea opciones con un puerto configurable validado dentro del rango de usuario (1024..65535).
    /// </summary>
    public static ObsServerOptions WithPort(int port)
    {
        if (port < MinUserPort || port > MaxPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                $"El puerto debe estar entre {MinUserPort} y {MaxPort}.");
        }

        return new ObsServerOptions(port);
    }

    /// <summary>
    /// Crea opciones con puerto efímero asignado por el sistema operativo (uso en pruebas).
    /// </summary>
    public static ObsServerOptions Ephemeral() => new(0);
}
