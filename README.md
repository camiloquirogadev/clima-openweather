# Clima · OpenWeatherMap + ASP.NET Core

App de clima hecha con ASP.NET Core (.NET 8) y un frontend estático. Muestra el
clima actual, el pronóstico de los próximos días y un mapa para elegir el punto.

## Tecnologías

- ASP.NET Core 8 (Minimal API)
- HttpClient tipado + Polly (reintentos)
- Caché en memoria
- Leaflet + OpenStreetMap
- xUnit

## Requisitos

- .NET 8 SDK
- Una API key gratuita de [OpenWeatherMap](https://openweathermap.org/api)

## Instalación

```bash
git clone https://github.com/usuario/clima-openweather.git
cd clima-openweather
dotnet user-secrets set "OpenWeather:ApiKey" "TU_API_KEY"
dotnet run
```

Abrir `http://localhost:5108`.

> La key recién creada puede tardar un par de horas en activarse.

## Endpoints

Ambos aceptan `?ciudad=` o `?lat=&lon=`.

```
GET /api/clima?ciudad=Bariloche
GET /api/pronostico?lat=-41.13&lon=-71.31
```

Respuesta:

```json
{
  "ciudad": "San Carlos de Bariloche",
  "pais": "AR",
  "descripcion": "Nubes",
  "iconoCodigo": "04d",
  "iconoUrl": "https://openweathermap.org/img/wn/04d@2x.png",
  "temperaturaC": 6.5,
  "sensacionC": 4.2,
  "humedadPct": 71,
  "vientoKmh": 11.2,
  "lat": -41.1335,
  "lon": -71.3103
}
```

## Tests

```bash
dotnet test tests/ClimaOpenWeather.Tests
```

## Estructura

```
Program.cs        Minimal API y configuración
Dtos/             DTOs de OpenWeather y los propios
Services/         Consumo de la API y mapeo
wwwroot/          Frontend (HTML, CSS, íconos)
tests/            Tests de ClimaService
```

## Créditos

Íconos [Meteocons](https://github.com/basmilius/weather-icons) de Bas Milius (MIT).
Datos de [OpenWeatherMap](https://openweathermap.org).
Mapas de [OpenStreetMap](https://www.openstreetmap.org/copyright).
