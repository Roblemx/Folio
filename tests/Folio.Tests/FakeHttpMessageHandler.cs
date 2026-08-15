using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Folio.Tests;

/// <summary>A test handler that returns canned responses (by call index) without any network.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder) =>
        _responder = responder;

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var index = CallCount++;
        return Task.FromResult(_responder(request, index));
    }

    public static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    public static HttpClient ClientReturning(string json) =>
        new(new FakeHttpMessageHandler((_, _) => Ok(json)));

    public static HttpClient ClientThrowing() =>
        new(new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("offline")));
}
