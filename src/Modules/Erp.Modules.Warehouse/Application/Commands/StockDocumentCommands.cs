using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Application.StockDocuments;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.Modules.Warehouse.Domain.Commands;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Commands;

/// <summary>
/// Warehouse's side of <see cref="IStockDocumentCommands"/>. Each command runs in one transaction: claim and lock the
/// idempotency key, then create, validate and confirm the stock document exactly as a Warehouse user would, and record
/// the result under the key. A repeated key returns the recorded document; a failure rolls everything back, the claim
/// included, so the caller can correct the command and send it again.
/// </summary>
internal sealed class StockDocumentCommands(
    IWarehouseDbContext db,
    IStockLocks locks,
    IClock clock,
    IValidator<CreateStockDocument> validator) : IStockDocumentCommands
{
    public async Task<Result<StockDocumentReference>> ReceiveAsync(StockReceiptCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var hash = Hash(new { command.WarehouseId, command.DocumentDate, command.Note, command.Lines });

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var entry = await ClaimAsync(command.CompanyId, command.Key, CommandLogEntry.Receive, cancellationToken);
        if (entry.IsCompleted)
        {
            return entry.PayloadHash == hash ? await ReferenceAsync(entry.DocumentId!.Value, cancellationToken) : StockCommandErrors.IdempotencyKeyReused;
        }

        var draft = new CreateStockDocument(
            command.CompanyId,
            StockDocumentType.Receipt,
            command.DocumentDate,
            null,
            command.WarehouseId,
            command.Note,
            command.Lines
                .Select(line => new StockLineInput(line.ArticleId, line.Quantity, line.UnitCost, line.LotNumber, line.LotExpiryDate, line.SerialNumbers))
                .ToList());
        var validation = await validator.ValidateAsync(draft, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (await StockDocumentDraftChecks.CheckAsync(db, draft, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var document = StockDocument.CreateDraft(
            draft.CompanyId, draft.Type, draft.DocumentDate, null, draft.TargetWarehouseId, draft.Note, draft.Lines, clock.UtcNow);
        document.AssignSource(command.Key.SourceModule, command.Key.SourceDocumentId);
        db.StockDocuments.Add(document);
        return await ConfirmAndRecordAsync(document, entry, hash, transaction, cancellationToken);
    }

    public async Task<Result<StockDocumentReference>> ReverseAsync(StockReversalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var hash = Hash(command.OriginalKey);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var entry = await ClaimAsync(command.CompanyId, command.Key, CommandLogEntry.Reverse, cancellationToken);
        if (entry.IsCompleted)
        {
            return entry.PayloadHash == hash ? await ReferenceAsync(entry.DocumentId!.Value, cancellationToken) : StockCommandErrors.IdempotencyKeyReused;
        }

        var original = await db.CommandLog.AsNoTracking().FirstOrDefaultAsync(
            c => c.CompanyId == command.CompanyId
                && c.SourceModule == command.OriginalKey.SourceModule
                && c.SourceDocumentId == command.OriginalKey.SourceDocumentId
                && c.Action == CommandLogEntry.Receive
                && c.DocumentId != null,
            cancellationToken);
        if (original is null || !await locks.LockDocumentAsync(command.CompanyId, original.DocumentId!.Value, cancellationToken))
        {
            return StockCommandErrors.OriginalNotFound;
        }

        var document = await db.StockDocuments.Include(d => d.Lines).FirstAsync(d => d.Id == original.DocumentId, cancellationToken);
        if (document.ReversedByDocumentId is not null)
        {
            return WarehouseErrors.StockDocumentAlreadyReversed;
        }

        var storno = document.CreateStorno(clock.Today, clock.UtcNow);
        storno.AssignSource(command.Key.SourceModule, command.Key.SourceDocumentId);
        db.StockDocuments.Add(storno);
        return await ConfirmAndRecordAsync(storno, entry, hash, transaction, cancellationToken);
    }

    private async Task<Result<StockDocumentReference>> ConfirmAndRecordAsync(
        StockDocument document,
        CommandLogEntry entry,
        string hash,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        // ConfirmAsync saves the document, stock and outbox event; the log entry is completed in the same save.
        entry.Complete(hash, document.Id);
        var confirmed = await StockDocumentPosting.ConfirmAsync(db, locks, clock, document, cancellationToken);
        if (confirmed.IsFailure)
        {
            return confirmed.Error;
        }

        await transaction.CommitAsync(cancellationToken);
        return ToReference(document);
    }

    private async Task<CommandLogEntry> ClaimAsync(
        Erp.SharedKernel.Domain.CompanyId companyId, IdempotencyKey key, string action, CancellationToken cancellationToken)
    {
        await locks.ClaimCommandAsync(companyId, key.SourceModule, key.SourceDocumentId, action, cancellationToken);
        return await db.CommandLog.FirstAsync(
            c => c.CompanyId == companyId && c.SourceModule == key.SourceModule && c.SourceDocumentId == key.SourceDocumentId && c.Action == action,
            cancellationToken);
    }

    private async Task<Result<StockDocumentReference>> ReferenceAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.StockDocuments.AsNoTracking().Include(d => d.Lines).FirstAsync(d => d.Id == documentId, cancellationToken);
        return ToReference(document);
    }

    private static StockDocumentReference ToReference(StockDocument document) =>
        new(
            document.Id,
            document.Number!,
            document.TotalValue ?? 0m,
            document.Lines.OrderBy(line => line.LineNumber).Select(line => line.Value ?? 0m).ToList());

    private static string Hash(object payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web))));
}
