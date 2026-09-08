# Clima · OpenWeatherMap + ASP.NET Core

Minimal API en .NET 8 sobre OpenWeatherMap, con un frontend estático que muestra el
clima actual, el pronóstico de los próximos días y un mapa para elegir el punto a mano.

## Correr

```bash
dotnet user-secrets set "OpenWeather:ApiKey" "TU_API_KEY"
dotnet run
```

Queda en `http://localhost:5108` (ver `Properties/launchSettings.json`).

La key se lee de `OpenWeather:ApiKey`, así que en producción sirve igual la variable
de entorno `OpenWeather__ApiKey`. Si falta, la app no arranca. Recién sacada de
openweathermap.org tarda un rato largo en activarse: hasta entonces devuelve 401 y
la API contesta "la key no es válida o todavía no está activa", no "ciudad no encontrada".

## Endpoints

Los dos aceptan `?ciudad=` o `?lat=&lon=`. Con coordenadas fuera de rango devuelven 400.

```
GET /api/clima?ciudad=Rosario
GET /api/pronostico?lat=-32.9468&lon=-60.6393
```

```json
{
  "ciudad": "Rosario",
  "pais": "AR",
  "descripcion": "Nubes dispersas",
  "iconoCodigo": "03d",
  "iconoUrl": "https://openweathermap.org/img/wn/03d@2x.png",
  "temperaturaC": 16.2,
  "sensacionC": 15.5,
  "humedadPct": 63,
  "vientoKmh": 16.7,
  "lat": -32.9468,
  "lon": -60.6393
}
```

Códigos de error: `404` ubicación inexistente, `429` cuota agotada, `500` key inválida,
`502`/`503` OpenWeather caído o devolviendo cualquier cosa.

## Estructura

```
Program.cs                      Minimal API, HttpClient tipado, políticas de Polly
Dtos/OpenWeatherResponse.cs     JSON crudo de /data/2.5/weather
Dtos/ForecastResponse.cs        JSON crudo de /data/2.5/forecast
Dtos/ClimaDto.cs                lo que devolvemos nosotros
Dtos/PronosticoDiaDto.cs        un día ya agregado (mín/máx)
Services/ClimaService.cs        consumo, agregación por día y caché
wwwroot/index.html              buscador, tarjeta, mapa (Leaflet) e íconos
tests/                          xunit sobre ClimaService con un handler fake
```

## Detalles que no se ven

- **Caché en memoria** (10 min el clima, 30 el pronóstico). El plan free son 60 req/min
  y cada click en el mapa dispara dos llamadas.
- **Reintentos con Polly** ante 5xx/408/timeout, con backoff. El 429 no se reintenta:
  con la cuota agotada sólo la quema más rápido.
- **El pronóstico se agrupa con el huso de la ciudad** (`city.timezone`), no con la fecha
  del server. Si no, consultar Tokio desde Argentina corre todos los días un casillero.
- **El último día se descarta si viene cortado**: la API manda 40 slots de 3 h desde
  *ahora*, así que el quinto día suele tener dos mediciones y un mín/máx que no significa nada.
  Por eso a veces son 4 tarjetas y no 5.
- **La API key nunca se loguea.** Va en el query string, así que a los logs sólo va la ruta.

## Tests

```bash
dotnet test tests/ClimaOpenWeather.Tests
```

Cubren el mapeo a los DTOs, el 404 contra el 401, el caché, el agrupado por huso horario
y que la key no aparezca en los logs.

## Íconos

Meteocons de Bas Milius (MIT) — ver `wwwroot/icons/weather/CREDITS.md`. Si el código de
OpenWeather no está mapeado, cae al PNG que devuelve la propia API.
