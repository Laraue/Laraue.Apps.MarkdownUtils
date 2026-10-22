using System.Net;
using System.Text;
using Laraue.Apps.MarkdownUtils.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Laraue.Apps.MarkdownUtils.WebApiHost.Tests;

public class RateLimitingTests
{
    private static readonly IPAddress Proxy = IPAddress.Parse("10.0.0.2");
    private static readonly IPAddress Untrusted = IPAddress.Parse("203.0.113.50");

    [Fact]
    public async Task Transpile_OverTheBucket_Returns429WithRetryAfter()
    {
        await using var factory = CreateFactory(("RateLimiting:Transpile:TokenLimit", "2"));

        Assert.Equal(StatusCodes.Status200OK, (await Transpile(factory, Proxy, "198.51.100.1")).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Transpile(factory, Proxy, "198.51.100.1")).StatusCode);

        var rejected = await Transpile(factory, Proxy, "198.51.100.1");

        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.StatusCode);
        Assert.True(int.Parse(rejected.Headers.RetryAfter.ToString()) >= 1);
    }

    [Fact]
    public async Task Transpile_ClientsBehindTrustedProxy_HaveSeparateBuckets()
    {
        await using var factory = CreateFactory(("RateLimiting:Transpile:TokenLimit", "1"));

        Assert.Equal(StatusCodes.Status200OK, (await Transpile(factory, Proxy, "198.51.100.1")).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Transpile(factory, Proxy, "198.51.100.2")).StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, (await Transpile(factory, Proxy, "198.51.100.1")).StatusCode);
    }

    [Fact]
    public async Task Transpile_ForwardedForFromUntrustedAddress_IsIgnored()
    {
        await using var factory = CreateFactory(("RateLimiting:Transpile:TokenLimit", "1"));

        Assert.Equal(StatusCodes.Status200OK, (await Transpile(factory, Untrusted, "198.51.100.1")).StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, (await Transpile(factory, Untrusted, "198.51.100.2")).StatusCode);
    }

    [Fact]
    public async Task Translate_OverTheMinuteWindow_Returns429UntilTheMinuteEnds()
    {
        // The default windows: 3 per minute, 15 per 10 minutes, 30 per hour, 50 per day.
        await using var factory = CreateFactory();

        for (var i = 0; i < 3; i++)
            Assert.Equal(StatusCodes.Status200OK, (await Translate(factory, "198.51.100.1")).StatusCode);

        var rejected = await Translate(factory, "198.51.100.1");

        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.StatusCode);
        Assert.InRange(int.Parse(rejected.Headers.RetryAfter.ToString()), 1, 60);

        Assert.Equal(StatusCodes.Status200OK, (await Translate(factory, "198.51.100.2")).StatusCode);
    }

    [Fact]
    public async Task Translate_OverTheDailyWindow_Returns429UntilTheNextDay()
    {
        await using var factory = CreateFactory(("RateLimiting:Translate:Windows:3:PermitLimit", "2"));

        Assert.Equal(StatusCodes.Status200OK, (await Translate(factory, "198.51.100.1")).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Translate(factory, "198.51.100.1")).StatusCode);

        var rejected = await Translate(factory, "198.51.100.1");

        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.StatusCode);
        Assert.True(int.Parse(rejected.Headers.RetryAfter.ToString()) > TimeSpan.FromHours(23).TotalSeconds);
    }

    [Fact]
    public async Task WrongLimit_FailsTheStart()
    {
        await using var factory = CreateFactory(("RateLimiting:Translate:Windows:0:PermitLimit", "0"));

        Assert.ThrowsAny<Exception>(() => factory.Server);
    }

    private static WebApplicationFactory<Program> CreateFactory(params (string Key, string Value)[] settings)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("Cors:Hosts:0", "http://localhost");
            builder.UseSetting("OpenAiClientOptions:BaseUrl", "http://localhost");
            builder.UseSetting("ForwardedHeaders:KnownProxies:0", Proxy.ToString());

            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOpenAiClient>();
                services.AddSingleton<IOpenAiClient, FakeOpenAiClient>();
            });
        });
    }

    private static Task<HttpResponse> Transpile(WebApplicationFactory<Program> factory, IPAddress remote, string forwardedFor)
    {
        return Post(factory, remote, forwardedFor, "/api/markdown-transpiler/transpile", """{"content":"# Hi"}""");
    }

    private static Task<HttpResponse> Translate(WebApplicationFactory<Program> factory, string forwardedFor)
    {
        return Post(factory, Proxy, forwardedFor, "/api/markdown-translator/translate", """{"content":"# Hi","from":"en","to":"es"}""");
    }

    private static async Task<HttpResponse> Post(
        WebApplicationFactory<Program> factory,
        IPAddress remote,
        string forwardedFor,
        string path,
        string body)
    {
        var context = await factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = remote;
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = path;
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        });

        return context.Response;
    }

    private sealed class FakeOpenAiClient : IOpenAiClient
    {
        public Task<ChatCompletionResponse> ChatCompletion(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatCompletionResponse
            {
                Id = "1",
                Model = request.Model,
                Choices = [new Choice { Index = 0, FinishReason = "stop", Message = new Message { Role = "assistant", Content = "# Hola" } }],
                Usage = new Usage { PromptTokens = 1, CompletionTokens = 1, TotalTokens = 2, PromptCacheHitTokens = 0, PromptCacheMissTokens = 1 },
            });
        }
    }
}
