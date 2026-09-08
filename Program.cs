using System.Net.Http;
using ClimaOpenWeather.Services;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

// ClimaService es scoped: sin este chequeo la key faltante recién tira en la
// primera request y no al arrancar.
if (string.IsNullOrWhiteSpace(builder.Configuration["OpenWeather:ApiKey"]))
{
    throw new InvalidOperationException(
        "Falta OpenWeather:ApiKey. Configurala con: " +
        "dotnet user-secrets set \"OpenWeather:ApiKey\" \"TU_API_KEY\"");
}

builder.Services.AddMemoryCache();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(
                "https://demo-clima-openweather.netlify.app",
                "http://localhost:5108",
                "https://localhost:7201")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddHttpClient<IClimaService, ClimaService>(client =>
{
    client.BaseAddress = new Uri("https://api.openweathermap.org");
    // HttpClient.Timeout cubre la cadena completa, reintentos incluidos.
    // Techo global acá: 4 intentos x 5s + 2.8s de backoff = 22.8s peor caso.
    // El timeout por intento va en la policy de abajo.
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "ClimaOpenWeather-Demo/1.0");
})
.AddPolicyHandler(PoliticaReintentos())
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5)));

var app = builder.Build();

app.UseCors("Frontend");
app.UseDefaultFiles();
app.UseStaticFiles();

// /api/clima?ciudad=Rosario  ó  /api/clima?lat=-32.9&lon=-60.6
app.MapGet("/api/clima", (
    string? ciudad, double? lat, double? lon, IClimaService clima, CancellationToken ct) =>
    Responder(
        () => clima.ObtenerActualAsync(ciudad, lat, lon, ct),
        ciudad, lat, lon,
        "No se encontró clima para la ubicación indicada."));

app.MapGet("/api/pronostico", (
    string? ciudad, double? lat, double? lon, IClimaService clima, CancellationToken ct) =>
    Responder(
        () => clima.ObtenerPronosticoAsync(ciudad, lat, lon, ct),
        ciudad, lat, lon,
        "No se encontró pronóstico para la ubicación indicada."));

app.Run();

static async Task<IResult> Responder<T>(
    Func<Task<T?>> consulta, string? ciudad, double? lat, double? lon, string mensajeVacio)
    where T : class
{
    var error = ValidarUbicacion(ciudad, lat, lon);
    if (error is not null)
        return Results.BadRequest(new { mensaje = error });

    try
    {
        var r = await consulta();
        return r is null
            ? Results.NotFound(new { mensaje = mensajeVacio })
            : Results.Ok(r);
    }
    catch (ClimaApiException ex)
    {
        return Results.Json(new { mensaje = ex.Message }, statusCode: ex.StatusCode);
    }
}

static string? ValidarUbicacion(string? ciudad, double? lat, double? lon)
{
    if (lat.HasValue != lon.HasValue)
        return "Si mandás coordenadas necesito lat y lon juntas.";

    if (lat.HasValue)
    {
        if (double.IsNaN(lat.Value) || lat.Value is < -90 or > 90)
            return "lat tiene que estar entre -90 y 90.";

        if (double.IsNaN(lon!.Value) || lon.Value is < -180 or > 180)
            return "lon tiene que estar entre -180 y 180.";

        return null;
    }

    return string.IsNullOrWhiteSpace(ciudad)
        ? "Indicá una ciudad o coordenadas (lat & lon)."
        : null;
}

// HandleTransientHttpError = 5xx + 408 + HttpRequestException; sumamos el timeout
// por intento. 429 queda afuera: con la cuota agotada el reintento no la recupera,
// la consume. Backoff 400/800/1600 ms.
static IAsyncPolicy<HttpResponseMessage> PoliticaReintentos() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .Or<TimeoutRejectedException>()
        .WaitAndRetryAsync(3, intento => TimeSpan.FromMilliseconds(200 * Math.Pow(2, intento)));
