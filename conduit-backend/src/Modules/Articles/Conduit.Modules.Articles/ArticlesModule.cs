using Conduit.Modules.Articles.Application;
using Conduit.Modules.Articles.Domain;
using Conduit.Modules.Articles.Infrastructure;
using Conduit.Modules.Articles.Presentation;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Modules.Articles;

public static class ArticlesModule
{
    public static IServiceCollection AddArticlesModule(this IServiceCollection services)
    {
        services.AddSingleton<IArticleRepository, PostgresArticleRepository>();
        services.AddSingleton<ArticleSlugs>();

        services.AddSingleton<CreateArticle>();
        services.AddSingleton<GetArticle>();
        services.AddSingleton<ListArticles>();
        services.AddSingleton<GetFeed>();
        services.AddSingleton<UpdateArticle>();
        services.AddSingleton<DeleteArticle>();
        services.AddSingleton<SetFavorite>();
        services.AddSingleton<ListPopularTags>();

        services.AddSingleton<IArticleCatalog, ArticleCatalog>();
        services.AddSingleton<IMigrationSource>(new EmbeddedMigrationSource(typeof(ArticlesModule).Assembly));

        return services;
    }

    public static IEndpointRouteBuilder MapArticlesEndpoints(this IEndpointRouteBuilder routes) =>
        routes.MapArticleEndpoints();
}
