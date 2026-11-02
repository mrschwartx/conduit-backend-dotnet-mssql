namespace Conduit.SharedKernel.Domain;

public sealed class MalformedBodyException()
    : DomainException(
        "Malformed JSON body",
        "MALFORMED_BODY",
        new Dictionary<string, string[]> { ["body"] = ["is not valid JSON"] });
