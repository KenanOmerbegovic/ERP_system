using Erp.SharedKernel.Domain;

namespace Erp.Modules.Warehouse.Domain.Commands;

/// <summary>
/// One command another module sent through the Warehouse contract, by its idempotency key. Written in the same
/// transaction as the stock document it created, so a repeated key finds the document and moves nothing.
/// </summary>
public sealed class CommandLogEntry : Entity<Guid>
{
    public const string Receive = "receive";
    public const string Issue = "issue";
    public const string Reverse = "reverse";

    private CommandLogEntry()
    {
    }

    public string SourceModule { get; private set; } = null!;

    public Guid SourceDocumentId { get; private set; }

    /// <summary><see cref="Receive"/>, <see cref="Issue"/> or <see cref="Reverse"/>.</summary>
    public string Action { get; private set; } = null!;

    /// <summary>SHA-256 of the command without its key; a repeated key must send the same command.</summary>
    public string? PayloadHash { get; private set; }

    /// <summary>Null until the command succeeded; a claimed but unfinished entry never outlives its transaction.</summary>
    public Guid? DocumentId { get; private set; }

    public bool IsCompleted => DocumentId is not null;

    public void Complete(string payloadHash, Guid documentId)
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("The command already completed.");
        }

        PayloadHash = payloadHash;
        DocumentId = documentId;
    }
}
