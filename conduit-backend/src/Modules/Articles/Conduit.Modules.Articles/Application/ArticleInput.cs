using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed record ArticleFields(
    ArticleTitle? Title,
    ArticleDescription? Description,
    ArticleBody? Body,
    TagList? TagList);

public static class ArticleInput
{
    public static readonly IReadOnlyList<string> AllFields = ["title", "description", "body", "tagList"];

    public static ArticleFields Parse(IReadOnlyDictionary<string, object?> source, IReadOnlyCollection<string> fields)
    {
        var errors = new ErrorCollector();
        var title = fields.Contains("title")
            ? errors.Capture(() => ArticleTitle.Create(source.GetValueOrDefault("title")))
            : null;
        var description = fields.Contains("description")
            ? errors.Capture(() => ArticleDescription.Create(source.GetValueOrDefault("description")))
            : null;
        var body = fields.Contains("body")
            ? errors.Capture(() => ArticleBody.Create(source.GetValueOrDefault("body")))
            : null;
        var tagList = fields.Contains("tagList")
            ? errors.Capture(() => TagList.Create(source.GetValueOrDefault("tagList")))
            : null;
        errors.ThrowIfAny();
        return new ArticleFields(title, description, body, tagList);
    }

    public static List<string> Provided(IReadOnlyDictionary<string, object?> source) =>
        AllFields.Where(source.ContainsKey).ToList();
}
