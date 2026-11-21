using System.Globalization;
using System.Net;

namespace CustomPresenter.Infrastructure.ObsServer;

/// <summary>
/// Configuración del micro-servidor local para OBS Studio y modo contenedor.
/// </summary>
public sealed class ObsServerOptions
{
    public const int DefaultPort = 47321;
    public const int MinUserPort = 1024;
    public const int MaxPort = 65535;

    public ObsServerOptions()
        : this(IPAddress.Loopback, DefaultPort)
    {
    }

    private ObsServerOptions(IPAddress bindAddress, int port)
    {
        BindAddress = bindAddress;
        Port = port;
    }

    /// <summary>
    /// Dirección de enlace del servidor (127.0.0.1 por defecto; 0.0.0.0 solo en contenedor).
    /// </summary>
    public IPAddress BindAddress { get; }

    public int Port { get; }

    /// <summary>
    /// Crea opciones con un puerto configurable validado dentro del rango de usuario (1024..65535).
    /// </summary>
    public static ObsServerOptions WithPort(int port)
    {
        ValidatePort(port);
        return new ObsServerOptions(IPAddress.Loopback, port);
    }

    /// <summary>
    /// Crea opciones para ejecución dentro de un contenedor Docker (0.0.0.0 interno, publicado en 127.0.0.1 del host).
    /// </summary>
    public static ObsServerOptions ForContainer(int port = DefaultPort)
    {
        ValidatePort(port);
        return new ObsServerOptions(IPAddress.Any, port);
    }

    public static ObsServerOptions FromEnvironment() =>
        FromEnvironment(
            Environment.GetEnvironmentVariable("CUSTOM_PRESENTER_CONTAINER"),
            Environment.GetEnvironmentVariable("CUSTOM_PRESENTER_PORT"));

    public static ObsServerOptions FromEnvironment(string? containerFlag, string? portValue)
    {
        var port = DefaultPort;
        if (!string.IsNullOrWhiteSpace(portValue))
        {
            if (!int.TryParse(portValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
            {
                throw new ArgumentOutOfRangeException(nameof(portValue), portValue, "Puerto inválido.");
            }

            ValidatePort(port);
        }

        var isContainer = string.Equals(containerFlag?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        return isContainer ? ForContainer(port) : WithPort(port);
    }

    /// <summary>
    /// Crea opciones con puerto efímero asignado por el sistema operativo (uso en pruebas).
    /// </summary>
    public static ObsServerOptions Ephemeral() => new(IPAddress.Loopback, 0);

    private static void ValidatePort(int port)
    {
        if (port < MinUserPort || port > MaxPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                $"El puerto debe estar entre {MinUserPort} y {MaxPort}.");
        }
    }
}
