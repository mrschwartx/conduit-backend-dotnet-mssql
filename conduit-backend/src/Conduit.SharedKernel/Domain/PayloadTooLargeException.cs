namespace Conduit.SharedKernel.Domain;

public sealed class PayloadTooLargeException()
    : DomainException(
        "Request body is too large",
        "PAYLOAD_TOO_LARGE",
        new Dictionary<string, string[]> { ["body"] = ["is too large"] });
