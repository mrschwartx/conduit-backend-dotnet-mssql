using System.Text;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Http;
using Microsoft.AspNetCore.Http;

namespace Conduit.UnitTests.SharedKernel;

public sealed class RequestBodyReaderTests
{
    [Fact]
    public async Task ReadsTheEnvelopeAsNestedValues()
    {
        var request = Request("""{"article":{"title":"T","tagList":["a","b"],"missing":null}}""");

        var article = await RequestBodyReader.ReadEnvelopeAsync(request, "article", CancellationToken.None);

        Assert.Equal("T", article["title"]);
        Assert.Equal(new object?[] { "a", "b" }, Assert.IsType<List<object?>>(article["tagList"]).ToArray());
        Assert.Null(article["missing"]);
        Assert.True(article.ContainsKey("missing"));
    }

    [Fact]
    public async Task ReturnsAnEmptyEnvelopeWhenTheKeyIsNotAnObject()
    {
        Assert.Empty(await RequestBodyReader.ReadEnvelopeAsync(Request("""{"article":"x"}"""), "article", CancellationToken.None));
        Assert.Empty(await RequestBodyReader.ReadEnvelopeAsync(Request("""{"other":{}}"""), "article", CancellationToken.None));
        Assert.Empty(await RequestBodyReader.ReadEnvelopeAsync(Request("[]"), "article", CancellationToken.None));
    }

    [Fact]
    public async Task AcceptsRequestsWithoutABody()
    {
        var request = new DefaultHttpContext().Request;
        request.ContentLength = 0;

        Assert.Empty(await RequestBodyReader.ReadJsonAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsNonJsonContentTypesWhenABodyIsPresent()
    {
        var request = Request("""{"a":1}""", "text/plain");

        await Assert.ThrowsAsync<UnsupportedMediaTypeException>(() => RequestBodyReader.ReadJsonAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    public async Task RejectsMalformedJson(string body)
    {
        await Assert.ThrowsAsync<MalformedBodyException>(() => RequestBodyReader.ReadJsonAsync(Request(body), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsBodiesAboveTheLimit()
    {
        var body = "{\"a\":\"" + new string('x', RequestBodyReader.MaxBytes) + "\"}";

        await Assert.ThrowsAsync<PayloadTooLargeException>(() => RequestBodyReader.ReadJsonAsync(Request(body), CancellationToken.None));
    }

    [Theory]
    [InlineData("application/json", true)]
    [InlineData("Application/JSON; charset=utf-8", true)]
    [InlineData("application/vnd.api+json", true)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void DetectsJsonContentTypes(string? contentType, bool expected)
    {
        Assert.Equal(expected, RequestBodyReader.IsJson(contentType));
    }

    private static HttpRequest Request(string body, string contentType = "application/json")
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var request = new DefaultHttpContext().Request;
        request.ContentType = contentType;
        request.ContentLength = bytes.Length;
        request.Body = new MemoryStream(bytes);
        return request;
    }
}
