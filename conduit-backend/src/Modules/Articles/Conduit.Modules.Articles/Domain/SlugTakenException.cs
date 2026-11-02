namespace Conduit.Modules.Articles.Domain;

public sealed class SlugTakenException() : Exception("Slug is already taken");
