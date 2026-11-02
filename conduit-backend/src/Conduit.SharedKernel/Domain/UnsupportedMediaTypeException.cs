namespace Conduit.SharedKernel.Domain;

public sealed class UnsupportedMediaTypeException()
    : DomainException(
        "Content-Type must be application/json",
        "UNSUPPORTED_MEDIA_TYPE",
        new Dictionary<string, string[]> { ["content-type"] = ["must be application/json"] });
