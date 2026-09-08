namespace ClimaOpenWeather.Services;

// Fallas de OpenWeather que no son 404: 401 (key), 429 (cuota), 5xx, timeout.
// El 404 sigue devolviendo null; esto lleva el status y el mensaje que se le
// muestra al cliente.
public class ClimaApiException : Exception
{
    public int StatusCode { get; }

    public ClimaApiException(int statusCode, string mensaje, Exception? inner = null)
        : base(mensaje, inner) => StatusCode = statusCode;
}
