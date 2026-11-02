using Conduit.Host;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Conduit.IntegrationTests.Support;

public sealed class ConduitApiFactory : WebApplicationFactory<Program>
{
    public ApiClient CreateApiClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        }));
}
