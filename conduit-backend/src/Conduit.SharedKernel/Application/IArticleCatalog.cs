namespace Conduit.SharedKernel.Application;

public interface IArticleCatalog
{
    Task<Guid?> FindIdBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
