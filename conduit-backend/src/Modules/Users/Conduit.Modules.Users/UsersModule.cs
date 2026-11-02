using Conduit.Modules.Users.Application;
using Conduit.Modules.Users.Domain;
using Conduit.Modules.Users.Infrastructure;
using Conduit.Modules.Users.Presentation;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Infrastructure.Http;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Modules.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, UsersModuleOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IPasswordHasher>(new BcryptPasswordHasher(options.BcryptRounds));
        services.AddSingleton<ITokenService>(provider => new JwtTokenService(
            options.AuthSecret,
            options.TokenTtl,
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IUserRepository, PostgresUserRepository>();
        services.AddSingleton<IFollowRepository, PostgresFollowRepository>();

        services.AddSingleton<RegisterUser>();
        services.AddSingleton<AuthenticateUser>();
        services.AddSingleton<GetCurrentUser>();
        services.AddSingleton<UpdateCurrentUser>();
        services.AddSingleton<GetProfile>();
        services.AddSingleton<FollowUser>();
        services.AddSingleton<UnfollowUser>();
        services.AddSingleton<VerifyAccessToken>();

        services.AddSingleton<IAuthorDirectory, AuthorDirectory>();
        services.AddSingleton<IRequestAuthenticator, UserTokenAuthenticator>();
        services.AddSingleton<IMigrationSource>(new EmbeddedMigrationSource(typeof(UsersModule).Assembly));

        return services;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapAccountEndpoints();
        routes.MapProfileEndpoints();
        return routes;
    }
}
