using System.Text.Json.Serialization;

namespace ClimaOpenWeather.Dtos;

public class ForecastResponse
{
    [JsonPropertyName("list")]
    public List<ForecastItem> List { get; set; } = new();

    [JsonPropertyName("city")]
    public CityInfo? City { get; set; }
}

public class CityInfo
{
    // Offset de la ciudad respecto de UTC, en segundos.
    [JsonPropertyName("timezone")]
    public int Timezone { get; set; }
}

public class ForecastItem
{
    // Siempre en UTC, formato "2026-08-25 15:00:00".
    [JsonPropertyName("dt_txt")]
    public string DtTxt { get; set; } = string.Empty;

    [JsonPropertyName("main")]
    public MainInfo? Main { get; set; }

    [JsonPropertyName("weather")]
    public List<WeatherItem> Weather { get; set; } = new();
}
