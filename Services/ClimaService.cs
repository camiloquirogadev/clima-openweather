using System.Globalization;
using System.Net;
using System.Text.Json;
using ClimaOpenWeather.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Polly.Timeout;

namespace ClimaOpenWeather.Services;

public class ClimaService : IClimaService
{
    private const string RutaActual = "/data/2.5/weather";
    private const string RutaPronostico = "/data/2.5/forecast";

    private static readonly CultureInfo Es = new("es-AR");

    // Free tier: 60 req/min. Cada click en el mapa = 2 llamadas (actual + pronóstico).
    private static readonly TimeSpan TtlActual = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TtlPronostico = TimeSpan.FromMinutes(30);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ClimaService> _logger;
    private readonly string _apiKey;

    public ClimaService(
        HttpClient http, IMemoryCache cache, IConfiguration config, ILogger<ClimaService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        // Program.cs corta el arranque si falta, así que acá ya está garantizada.
        _apiKey = config["OpenWeather:ApiKey"]!;
    }

    public async Task<ClimaDto?> ObtenerActualAsync(
        string? ciudad, double? lat, double? lon, CancellationToken ct = default)
    {
        var loc = BuildLocationQuery(ciudad, lat, lon);
        if (loc is null) return null;

        var clave = $"{RutaActual}?{loc}";
        if (_cache.TryGetValue(clave, out ClimaDto? cacheado))
            return cacheado;

        var raw = await GetJsonAsync<OpenWeatherResponse>(RutaActual, loc, ct);
        if (raw?.Main is null || raw.Weather.Count == 0) return null;

        var w = raw.Weather[0];
        var dto = new ClimaDto(
            Ciudad: raw.Name,
            Pais: raw.Sys?.Country ?? "",
            Descripcion: Capitalizar(w.Description),
            IconoCodigo: w.Icon,
            IconoUrl: IconUrl(w.Icon),
            TemperaturaC: Math.Round(raw.Main.Temp, 1),
            SensacionC: Math.Round(raw.Main.FeelsLike, 1),
            HumedadPct: raw.Main.Humidity,
            // m/s -> km/h: units=metric no aplica a wind.speed.
            VientoKmh: Math.Round((raw.Wind?.Speed ?? 0) * 3.6, 1),
            Lat: raw.Coord?.Lat ?? lat ?? 0,
            Lon: raw.Coord?.Lon ?? lon ?? 0);

        _cache.Set(clave, dto, TtlActual);
        return dto;
    }

    public async Task<IReadOnlyList<PronosticoDiaDto>?> ObtenerPronosticoAsync(
        string? ciudad, double? lat, double? lon, CancellationToken ct = default)
    {
        var loc = BuildLocationQuery(ciudad, lat, lon);
        if (loc is null) return null;

        var clave = $"{RutaPronostico}?{loc}";
        if (_cache.TryGetValue(clave, out IReadOnlyList<PronosticoDiaDto>? cacheado))
            return cacheado;

        var raw = await GetJsonAsync<ForecastResponse>(RutaPronostico, loc, ct);
        if (raw is null || raw.List.Count == 0) return null;

        // dt_txt viene en UTC. El corte de día se hace con city.timezone (offset en
        // segundos), no con la fecha del server: si no, el agrupado se corre un día
        // cuando el offset de la ciudad y el del host no coinciden.
        var offset = TimeSpan.FromSeconds(raw.City?.Timezone ?? 0);
        var hoyLocal = DateTime.UtcNow.Add(offset).Date;

        var dias = raw.List
            .Where(i => i.Main is not null && i.Weather.Count > 0)
            .Select(i => new
            {
                Local = DateTime.ParseExact(
                    i.DtTxt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).Add(offset),
                Item = i
            })
            .GroupBy(x => x.Local.Date)
            .Where(g => g.Key > hoyLocal)
            .OrderBy(g => g.Key)
            .ToList();

        // 40 slots de 3h contados desde ahora => el último día llega parcial.
        // Con menos de 4 muestras el mín/máx no representa la jornada, lo descartamos.
        if (dias.Count > 1 && dias[^1].Count() < 4)
            dias.RemoveAt(dias.Count - 1);

        var resultado = dias
            .Take(5)
            .Select(g =>
            {
                // Ícono y descripción del slot más cercano a las 12 local.
                var mediodia = g.OrderBy(x => Math.Abs(x.Local.Hour - 12)).First().Item;
                var wm = mediodia.Weather[0];
                return new PronosticoDiaDto(
                    Fecha: g.Key.ToString("yyyy-MM-dd"),
                    DiaSemana: Capitalizar(g.Key.ToString("dddd", Es)),
                    MinC: Math.Round(g.Min(x => x.Item.Main!.TempMin), 0),
                    MaxC: Math.Round(g.Max(x => x.Item.Main!.TempMax), 0),
                    Descripcion: Capitalizar(wm.Description),
                    IconoCodigo: wm.Icon,
                    IconoUrl: IconUrl(wm.Icon));
            })
            .ToList();

        if (resultado.Count == 0) return null;

        _cache.Set(clave, (IReadOnlyList<PronosticoDiaDto>)resultado, TtlPronostico);
        return resultado;
    }

    private static string? BuildLocationQuery(string? ciudad, double? lat, double? lon)
    {
        if (lat.HasValue && lon.HasValue)
            return $"lat={lat.Value.ToString(CultureInfo.InvariantCulture)}" +
                   $"&lon={lon.Value.ToString(CultureInfo.InvariantCulture)}";

        if (!string.IsNullOrWhiteSpace(ciudad))
            return $"q={Uri.EscapeDataString(ciudad.Trim())}";

        return null;
    }

    // url lleva appid en el query string: a los logs va `ruta`, nunca `url`.
    private async Task<T?> GetJsonAsync<T>(string ruta, string loc, CancellationToken ct) where T : class
    {
        var url = $"{ruta}?{loc}&appid={_apiKey}&units=metric&lang=es";

        HttpResponseMessage resp;
        try
        {
            resp = await _http.GetAsync(url, ct);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TimeoutRejectedException or TaskCanceledException
            && !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "No se pudo llegar a OpenWeather ({Ruta})", ruta);
            throw new ClimaApiException(
                503, "El servicio de clima no está respondiendo. Probá de nuevo en un momento.", ex);
        }

        using (resp)
        {
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;

            if (!resp.IsSuccessStatusCode)
                throw ErrorDeApi(resp.StatusCode, ruta);

            try
            {
                return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Respuesta ilegible de OpenWeather ({Ruta})", ruta);
                throw new ClimaApiException(502, "OpenWeather devolvió una respuesta inesperada.", ex);
            }
        }
    }

    private ClimaApiException ErrorDeApi(HttpStatusCode status, string ruta)
    {
        _logger.LogWarning("OpenWeather respondió {Status} en {Ruta}", (int)status, ruta);

        return status switch
        {
            HttpStatusCode.Unauthorized => new ClimaApiException(
                500,
                "La API key no es válida o todavía no está activa (OpenWeather puede tardar un par de horas)."),

            HttpStatusCode.TooManyRequests => new ClimaApiException(
                429, "Se agotó la cuota de la API por ahora. Esperá un minuto."),

            _ => new ClimaApiException(502, $"OpenWeather devolvió un error ({(int)status}).")
        };
    }

    private static string IconUrl(string icon) =>
        $"https://openweathermap.org/img/wn/{icon}@2x.png";

    private static string Capitalizar(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
