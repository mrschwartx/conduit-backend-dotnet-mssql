using Conduit.Host.Configuration;
using Conduit.Modules.Articles.Application;
using Conduit.Modules.Comments.Application;
using Conduit.Modules.Users.Application;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;

namespace Conduit.Host.Seeding;

public sealed class SeedCommand(
    AppSettings settings,
    SqlMigrator migrator,
    RegisterUser registerUser,
    VerifyAccessToken verifyAccessToken,
    FollowUser followUser,
    CreateArticle createArticle,
    SetFavorite setFavorite,
    CreateComment createComment,
    ILogger<SeedCommand> logger)
{
    private const string FirstUser = "johndoe123";

    private const string SecondUser = "jane78";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (settings.IsProduction && !settings.SeedAllowProduction)
        {
            logger.LogError("Refusing to seed a production database");
            return 1;
        }

        try
        {
            await migrator.MigrateAsync(cancellationToken);
            await SeedAsync(cancellationToken);
            return 0;
        }
        catch (ConflictException)
        {
            logger.LogWarning("Skipped seeding because the demo users already exist");
            return 0;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Seeding failed: {Message}", error.Message);
            return 1;
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var john = await RegisterAsync(FirstUser, "johndoe123@mail.com", "password123", cancellationToken);
        var jane = await RegisterAsync(SecondUser, "jane@mail.com", "securepassword456", cancellationToken);

        await followUser.ExecuteAsync(john, SecondUser, cancellationToken);
        await followUser.ExecuteAsync(jane, FirstUser, cancellationToken);

        var janeArticle = await createArticle.ExecuteAsync(
            jane,
            Article("Learn ASP.NET Core", "Build a REST API step by step", "ASP.NET Core makes it simple to build a modular monolith.", ["dotnet", "api"]),
            cancellationToken);
        var johnArticle = await createArticle.ExecuteAsync(
            john,
            Article("Read Atomic Habits", "Notes on building better habits", "Small habits compound into remarkable results.", ["books", "habits"]),
            cancellationToken);

        await setFavorite.ExecuteAsync(john, janeArticle.Slug, true, cancellationToken);
        await setFavorite.ExecuteAsync(jane, johnArticle.Slug, true, cancellationToken);

        await createComment.ExecuteAsync(
            john,
            janeArticle.Slug,
            new Dictionary<string, object?> { ["body"] = "Great introduction, thanks for sharing." },
            cancellationToken);
        await createComment.ExecuteAsync(
            jane,
            johnArticle.Slug,
            new Dictionary<string, object?> { ["body"] = "I need to read this one soon." },
            cancellationToken);

        logger.LogInformation("Seeding completed");
    }

    private async Task<Guid> RegisterAsync(
        string username,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await registerUser.ExecuteAsync(
            new Dictionary<string, object?> { ["username"] = username, ["email"] = email, ["password"] = password },
            cancellationToken);
        var authenticated = await verifyAccessToken.ExecuteAsync(user.Token, cancellationToken);
        logger.LogInformation("Seeded user {Username}", username);
        return authenticated.UserId;
    }

    private static Dictionary<string, object?> Article(
        string title,
        string description,
        string body,
        IReadOnlyList<string> tags) =>
        new()
        {
            ["title"] = title,
            ["description"] = description,
            ["body"] = body,
            ["tagList"] = tags.Cast<object?>().ToList()
        };
}
