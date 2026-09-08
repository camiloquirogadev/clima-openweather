using System.Text.Json.Serialization;

namespace ClimaOpenWeather.Dtos;

public class OpenWeatherResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("weather")]
    public List<WeatherItem> Weather { get; set; } = new();

    [JsonPropertyName("main")]
    public MainInfo? Main { get; set; }

    [JsonPropertyName("wind")]
    public WindInfo? Wind { get; set; }

    [JsonPropertyName("sys")]
    public SysInfo? Sys { get; set; }

    [JsonPropertyName("coord")]
    public CoordInfo? Coord { get; set; }
}

public class CoordInfo
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}

public class WeatherItem
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;
}

public class MainInfo
{
    [JsonPropertyName("temp")]
    public double Temp { get; set; }

    [JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }

    [JsonPropertyName("temp_min")]
    public double TempMin { get; set; }

    [JsonPropertyName("temp_max")]
    public double TempMax { get; set; }
}

public class WindInfo
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }
}

public class SysInfo
{
    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;
}
