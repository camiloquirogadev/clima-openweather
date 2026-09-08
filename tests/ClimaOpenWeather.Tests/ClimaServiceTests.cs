using System.Globalization;
using System.Net;
using System.Text;
using ClimaOpenWeather.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClimaOpenWeather.Tests;

public class ClimaServiceTests
{
    private const string ClimaRosario = """
    {
      "name": "Rosario",
      "coord": { "lat": -32.9468, "lon": -60.6393 },
      "sys": { "country": "AR" },
      "weather": [ { "description": "nubes dispersas", "icon": "03d" } ],
      "main": { "temp": 16.24, "feels_like": 15.51, "humidity": 63 },
      "wind": { "speed": 4.63 }
    }
    """;

    [Fact]
    public async Task Mapea_el_clima_actual_al_dto_propio()
    {
        var svc = CrearServicio(new FakeHandler(HttpStatusCode.OK, ClimaRosario));

        var dto = await svc.ObtenerActualAsync("Rosario", null, null);

        Assert.NotNull(dto);
        Assert.Equal("Rosario", dto.Ciudad);
        Assert.Equal("AR", dto.Pais);
        Assert.Equal("Nubes dispersas", dto.Descripcion);
        Assert.Equal(16.2, dto.TemperaturaC);
        Assert.Equal(63, dto.HumedadPct);
        // 4.63 m/s * 3.6 = 16.668 -> 16.7 km/h
        Assert.Equal(16.7, dto.VientoKmh);
        Assert.Equal("03d", dto.IconoCodigo);
    }

    [Fact]
    public async Task Ciudad_inexistente_devuelve_null()
    {
        var svc = CrearServicio(new FakeHandler(HttpStatusCode.NotFound, """{"message":"city not found"}"""));

        Assert.Null(await svc.ObtenerActualAsync("Narnia", null, null));
    }

    [Fact]
    public async Task Key_invalida_no_se_confunde_con_ciudad_inexistente()
    {
        var svc = CrearServicio(new FakeHandler(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<ClimaApiException>(
            () => svc.ObtenerActualAsync("Rosario", null, null));

        Assert.Equal(500, ex.StatusCode);
        Assert.Contains("API key", ex.Message);
    }

    [Fact]
    public async Task Cuota_agotada_devuelve_429_y_no_null()
    {
        var svc = CrearServicio(new FakeHandler(HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<ClimaApiException>(
            () => svc.ObtenerActualAsync("Rosario", null, null));

        Assert.Equal(429, ex.StatusCode);
    }

    [Fact]
    public async Task La_segunda_consulta_sale_del_cache()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, ClimaRosario);
        var svc = CrearServicio(handler);

        await svc.ObtenerActualAsync("Rosario", null, null);
        await svc.ObtenerActualAsync("Rosario", null, null);

        Assert.Equal(1, handler.Llamadas);
    }

    [Fact]
    public async Task Nunca_loguea_la_api_key()
    {
        var logger = new LoggerCapturador<ClimaService>();
        var handler = new FakeHandler(_ => throw new HttpRequestException("sin red"));
        var svc = CrearServicio(handler, logger);

        await Assert.ThrowsAsync<ClimaApiException>(
            () => svc.ObtenerActualAsync("Rosario", null, null));

        Assert.NotEmpty(logger.Mensajes);
        Assert.DoesNotContain(logger.Mensajes, m => m.Contains("test-key"));
    }

    [Fact]
    public async Task El_pronostico_arranca_manana_en_la_hora_de_la_ciudad()
    {
        // Offset +14h (UTC+14). Agrupando con la fecha del host, este caso se corre un día.
        const int offsetSegundos = 14 * 3600;
        var offset = TimeSpan.FromSeconds(offsetSegundos);

        var handler = new FakeHandler(HttpStatusCode.OK,
            ForecastJson(offsetSegundos, DateTime.UtcNow, slots: 40));
        var svc = CrearServicio(handler);

        var dias = await svc.ObtenerPronosticoAsync(null, 1.87, -157.4);

        Assert.NotNull(dias);
        var manana = DateTime.UtcNow.Add(offset).Date.AddDays(1);
        Assert.Equal(manana.ToString("yyyy-MM-dd"), dias[0].Fecha);
    }

    [Fact]
    public async Task Descarta_el_ultimo_dia_si_viene_cortado()
    {
        // Tres días completos (8 slots de 3 h cada uno) y un cuarto con sólo dos.
        var desde = DateTime.UtcNow.Date.AddDays(1);
        var handler = new FakeHandler(HttpStatusCode.OK, ForecastJson(0, desde, slots: 26));
        var svc = CrearServicio(handler);

        var dias = await svc.ObtenerPronosticoAsync(null, 0, 0);

        Assert.NotNull(dias);
        Assert.Equal(3, dias.Count);
    }

    [Fact]
    public async Task Toma_el_minimo_y_el_maximo_de_todo_el_dia()
    {
        var desde = DateTime.UtcNow.Date.AddDays(1);
        var handler = new FakeHandler(HttpStatusCode.OK, ForecastJson(0, desde, slots: 8));
        var svc = CrearServicio(handler);

        var dias = await svc.ObtenerPronosticoAsync(null, 0, 0);

        Assert.NotNull(dias);
        // ForecastJson usa temp_min = i y temp_max = 20 + i sobre 8 slots.
        Assert.Equal(0, dias[0].MinC);
        Assert.Equal(27, dias[0].MaxC);
    }

    private static ClimaService CrearServicio(
        FakeHandler handler, ILogger<ClimaService>? logger = null)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openweathermap.org")
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenWeather:ApiKey"] = "test-key"
            })
            .Build();

        return new ClimaService(
            http,
            new MemoryCache(new MemoryCacheOptions()),
            config,
            logger ?? NullLogger<ClimaService>.Instance);
    }

    // Slots cada 3 h en UTC, como los devuelve /data/2.5/forecast.
    private static string ForecastJson(int timezoneSegundos, DateTime desdeUtc, int slots)
    {
        var arranque = new DateTime(
            desdeUtc.Year, desdeUtc.Month, desdeUtc.Day, desdeUtc.Hour / 3 * 3, 0, 0);

        var sb = new StringBuilder();
        sb.Append("""{"city":{"timezone":""").Append(timezoneSegundos).Append("},\"list\":[");

        for (var i = 0; i < slots; i++)
        {
            if (i > 0) sb.Append(',');
            var dt = arranque.AddHours(3 * i).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            sb.Append(CultureInfo.InvariantCulture, $$"""
                {"dt_txt":"{{dt}}",
                 "main":{"temp":18,"feels_like":18,"humidity":50,"temp_min":{{i % 8}},"temp_max":{{20 + i % 8}}},
                 "weather":[{"description":"cielo claro","icon":"01d"}]}
                """);
        }

        sb.Append("]}");
        return sb.ToString();
    }
}
