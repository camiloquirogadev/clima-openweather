using ClimaOpenWeather.Dtos;

namespace ClimaOpenWeather.Services;

public interface IClimaService
{
    // null = ubicación inexistente (404 de OpenWeather).
    // El resto de las fallas sale como ClimaApiException.
    Task<ClimaDto?> ObtenerActualAsync(
        string? ciudad, double? lat, double? lon, CancellationToken ct = default);

    Task<IReadOnlyList<PronosticoDiaDto>?> ObtenerPronosticoAsync(
        string? ciudad, double? lat, double? lon, CancellationToken ct = default);
}
