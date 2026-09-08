namespace ClimaOpenWeather.Dtos;

public record ClimaDto(
    string Ciudad,
    string Pais,
    string Descripcion,
    string IconoCodigo,
    string IconoUrl,
    double TemperaturaC,
    double SensacionC,
    int HumedadPct,
    double VientoKmh,
    double Lat,
    double Lon);
