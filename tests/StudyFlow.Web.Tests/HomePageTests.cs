using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StudyFlow.Web.Tests;

public sealed class HomePageTests(WebApplicationFactory<Program> application)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task HomeReturnsRussianHtml()
    {
        using var client = application.CreateClient();
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("lang=\"ru\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HomeProvidesDocumentationLink()
    {
        using var client = application.CreateClient();
        var html = await client.GetStringAsync("/");

        Assert.Contains("href=\"https://mirea-studyflow-bandukov-ilya.github.io/Docs/\"", html);
    }

    [Fact]
    public async Task UnknownRouteReturnsNotFound()
    {
        using var client = application.CreateClient();
        using var response = await client.GetAsync("/unknown-page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
