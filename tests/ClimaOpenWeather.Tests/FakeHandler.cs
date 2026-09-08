using System.Net;
using System.Text;

namespace ClimaOpenWeather.Tests;

// Handler fake: responde con payloads fijos y cuenta llamadas (lo usa el test de caché).
// El responder crea una response nueva por llamada porque ClimaService la dispone.
internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public int Llamadas { get; private set; }
    public List<Uri> Urls { get; } = new();

    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responder = responder;

    public FakeHandler(HttpStatusCode status, string json = "{}")
        : this(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        })
    { }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Llamadas++;
        Urls.Add(request.RequestUri!);
        return Task.FromResult(_responder(request));
    }
}
