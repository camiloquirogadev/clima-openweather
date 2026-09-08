namespace ClimaOpenWeather.Dtos;

public record PronosticoDiaDto(
    string Fecha,
    string DiaSemana,
    double MinC,
    double MaxC,
    string Descripcion,
    string IconoCodigo,
    string IconoUrl);
