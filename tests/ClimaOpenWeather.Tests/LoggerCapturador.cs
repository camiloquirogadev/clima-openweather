using Microsoft.Extensions.Logging;

namespace ClimaOpenWeather.Tests;

// ILogger que acumula los mensajes ya formateados para poder assertear sobre ellos.
internal sealed class LoggerCapturador<T> : ILogger<T>
{
    public List<string> Mensajes { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Mensajes.Add(formatter(state, exception));
    }
}
