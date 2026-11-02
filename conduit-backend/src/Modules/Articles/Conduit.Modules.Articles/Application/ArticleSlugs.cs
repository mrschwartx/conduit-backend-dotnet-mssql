using Conduit.Modules.Articles.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class ArticleSlugs(IArticleRepository articles)
{
    public const int MaxAttempts = 5;

    public async Task<string> AllocateAsync(string title, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var baseSlug = Slug.From(title);
        for (var attempt = 1; ; attempt++)
        {
            var candidate = Slug.Candidate(baseSlug, attempt);
            if (!Slug.IsReserved(candidate) && !await articles.SlugTakenAsync(candidate, excludeId, cancellationToken))
            {
                return candidate;
            }
        }
    }

    public static async Task<T> PersistAsync<T>(Func<Task<string>> allocate, Func<string, Task<T>> persist)
    {
        for (var attempt = 1; ; attempt++)
        {
            var slug = await allocate();
            try
            {
                return await persist(slug);
            }
            catch (SlugTakenException) when (attempt < MaxAttempts)
            {
            }
        }
    }
}
