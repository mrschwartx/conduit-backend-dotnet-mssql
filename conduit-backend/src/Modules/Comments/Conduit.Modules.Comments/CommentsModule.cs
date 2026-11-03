using Conduit.Modules.Comments.Application;
using Conduit.Modules.Comments.Domain;
using Conduit.Modules.Comments.Infrastructure;
using Conduit.Modules.Comments.Presentation;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Modules.Comments;

public static class CommentsModule
{
    public static IServiceCollection AddCommentsModule(this IServiceCollection services)
    {
        services.AddSingleton<ICommentRepository, SqlServerCommentRepository>();

        services.AddSingleton<CreateComment>();
        services.AddSingleton<ListComments>();
        services.AddSingleton<DeleteComment>();
        services.AddSingleton<DeleteCommentsByArticle>();

        services.AddSingleton<IIntegrationEventHandler<ArticleDeleted>, ArticleDeletedHandler>();
        services.AddSingleton<IMigrationSource>(new EmbeddedMigrationSource(typeof(CommentsModule).Assembly));

        return services;
    }

    public static IEndpointRouteBuilder MapCommentsEndpoints(this IEndpointRouteBuilder routes) =>
        routes.MapCommentEndpoints();
}
