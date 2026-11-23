using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingRealm.Registry;

namespace ParkingRealm.Tests;

public class VehicleRegistryTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            return respond(request, ct);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpVehicleRegistry Registry(StubHandler handler, TimeSpan? timeout = null, bool configured = true) =>
        new(new HttpClient(handler)
        {
            BaseAddress = configured ? new Uri("http://registry.test/") : null,
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        }, NullLogger<HttpVehicleRegistry>.Instance);

    [Fact]
    public async Task Matching_record_is_found_and_the_number_is_escaped()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, """{"registrationNumber":"ab12345"}""")));
        Assert.Equal(RegistryOutcome.Found, await Registry(handler).LookupAsync("AB12345", default));
        Assert.Equal("http://registry.test/vehicles/AB12345", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Unknown_number_is_not_found() =>
        Assert.Equal(RegistryOutcome.NotFound, await Registry(
            new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))).LookupAsync("AB12345", default));

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"registrationNumber":""}""")]
    [InlineData("""{"registrationNumber":null}""")]
    [InlineData("""{"registrationNumber":42}""")]
    [InlineData("""{"registrationNumber":"CD12345"}""")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task Missing_malformed_or_mismatched_data_is_not_found(string body) =>
        Assert.Equal(RegistryOutcome.NotFound, await Registry(
            new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body)))).LookupAsync("AB12345", default));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Error_responses_are_unavailable(HttpStatusCode status) =>
        Assert.Equal(RegistryOutcome.Unavailable, await Registry(
            new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)))).LookupAsync("AB12345", default));

    [Fact]
    public async Task Connection_failure_is_unavailable() =>
        Assert.Equal(RegistryOutcome.Unavailable, await Registry(
            new StubHandler((_, _) => throw new HttpRequestException("refused"))).LookupAsync("AB12345", default));

    [Fact]
    public async Task Timeout_is_unavailable() =>
        Assert.Equal(RegistryOutcome.Unavailable, await Registry(
            new StubHandler(async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage();
            }), TimeSpan.FromMilliseconds(50)).LookupAsync("AB12345", default));

    [Fact]
    public async Task Missing_configuration_is_unavailable() =>
        Assert.Equal(RegistryOutcome.Unavailable, await Registry(
            new StubHandler((_, _) => throw new InvalidOperationException()), configured: false).LookupAsync("AB12345", default));
}
