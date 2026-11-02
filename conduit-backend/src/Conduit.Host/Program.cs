using Conduit.Host.Configuration;
using Conduit.Host.Seeding;
using Conduit.Modules.Articles;
using Conduit.Modules.Comments;
using Conduit.Modules.Users;
using Conduit.SharedKernel;
using Conduit.SharedKernel.Infrastructure.Http;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Conduit.Host;

public sealed class Program
{
    private const string CorsPolicyName = "conduit-client";

    private const string SeedFlag = "--seed";

    public static async Task<int> Main(string[] args)
    {
        WebApplication app;
        AppSettings settings;
        try
        {
            GlobalizationCheck.EnsureNormalizationAvailable();
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddDotEnvFile(".env", args);
            settings = AppSettings.From(builder.Configuration);
            ConfigureServices(builder, settings);
            app = builder.Build();
        }
        catch (ConfigurationException error)
        {
            await Console.Error.WriteLineAsync(error.Message);
            return 1;
        }

        await using (app)
        {
            if (args.Contains(SeedFlag))
            {
                return await app.Services.GetRequiredService<SeedCommand>().RunAsync();
            }

            ConfigurePipeline(app);
            await app.RunAsync();
            return 0;
        }
    }

    private static void ConfigureServices(WebApplicationBuilder builder, AppSettings settings)
    {
        builder.WebHost.UseUrls($"http://{settings.Host}:{settings.Port}");

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z' ";
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter("Conduit", settings.LogLevel);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);

        var connectionString = PostgresConnectionString.Build(
            settings.DatabaseUrl,
            settings.DatabasePoolMax,
            settings.DatabaseTimeoutMs);

        builder.Services.AddSingleton(settings);
        builder.Services.AddSharedKernel(connectionString);
        builder.Services.AddUsersModule(settings.ToUsersOptions());
        builder.Services.AddArticlesModule();
        builder.Services.AddCommentsModule();
        builder.Services.AddSingleton<SeedCommand>();
        builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(settings.ClientUrl)
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "Authorization")));
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseCors(CorsPolicyName);
        app.UseRouting();
        app.UseMiddleware<AccessTokenMiddleware>();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = (context, report) =>
            {
                context.Response.ContentType = "text/plain";
                return context.Response.WriteAsync(report.Status == HealthStatus.Healthy ? "OK" : "Unhealthy");
            }
        });
        app.MapUsersEndpoints();
        app.MapArticlesEndpoints();
        app.MapCommentsEndpoints();
        app.MapFallback("{*path}", () => Results.Json(
            new ErrorResponse(new Dictionary<string, string[]> { ["path"] = ["not found"] }),
            statusCode: StatusCodes.Status404NotFound));
    }
}
