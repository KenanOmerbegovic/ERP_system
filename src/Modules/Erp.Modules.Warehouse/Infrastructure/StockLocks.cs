using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Infrastructure;

/// <summary>
/// PostgreSQL row locks for <see cref="IStockLocks"/>. Rows are created with <c>INSERT … ON CONFLICT DO NOTHING</c>
/// (which waits for a concurrent insert of the same key to finish) and then locked with <c>SELECT … FOR UPDATE</c>,
/// one by one in a fixed order. Entities are loaded only after they are locked, so EF Core sees current values.
/// </summary>
internal sealed class StockLocks(WarehouseDbContext db) : IStockLocks
{
    public async Task ClaimCommandAsync(
        CompanyId companyId, string sourceModule, Guid sourceDocumentId, string action, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO warehouse.command_log (id, company_id, source_module, source_document_id, action)
            VALUES ({Guid.CreateVersion7()}, {companyId.Value}, {sourceModule}, {sourceDocumentId}, {action})
            ON CONFLICT (company_id, source_module, source_document_id, action) DO NOTHING
            """,
            cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"""
            SELECT 1 FROM warehouse.command_log
            WHERE company_id = {companyId.Value} AND source_module = {sourceModule} AND source_document_id = {sourceDocumentId} AND action = {action}
            FOR UPDATE
            """,
            cancellationToken);
    }

    public async Task<bool> LockDocumentAsync(CompanyId companyId, Guid documentId, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var found = await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM warehouse.stock_documents WHERE company_id = {companyId.Value} AND id = {documentId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return found.Count == 1;
    }

    public async Task LockStockItemsAsync(
        CompanyId companyId, IReadOnlyCollection<(Guid StorehouseId, Guid ArticleId)> keys, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        foreach (var (storehouseId, articleId) in keys.Distinct().OrderBy(key => key.StorehouseId).ThenBy(key => key.ArticleId))
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO warehouse.stock_items (id, company_id, warehouse_id, article_id, quantity, value)
                VALUES ({Guid.CreateVersion7()}, {companyId.Value}, {storehouseId}, {articleId}, 0, 0)
                ON CONFLICT (company_id, warehouse_id, article_id) DO NOTHING
                """,
                cancellationToken);
            await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM warehouse.stock_items WHERE company_id = {companyId.Value} AND warehouse_id = {storehouseId} AND article_id = {articleId} FOR UPDATE",
                cancellationToken);
        }
    }

    public async Task LockSerialNumbersAsync(
        CompanyId companyId, IReadOnlyCollection<(Guid ArticleId, string Number)> keys, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        foreach (var (articleId, number) in keys.Distinct().OrderBy(key => key.ArticleId).ThenBy(key => key.Number, StringComparer.Ordinal))
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO warehouse.serial_numbers (id, company_id, article_id, number, warehouse_id)
                VALUES ({Guid.CreateVersion7()}, {companyId.Value}, {articleId}, {number}, NULL)
                ON CONFLICT (company_id, article_id, number) DO NOTHING
                """,
                cancellationToken);
            await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM warehouse.serial_numbers WHERE company_id = {companyId.Value} AND article_id = {articleId} AND number = {number} FOR UPDATE",
                cancellationToken);
        }
    }

    public async Task EnsureLotsAsync(
        CompanyId companyId, IReadOnlyCollection<(Guid ArticleId, string Number, DateOnly? ExpiryDate)> lots, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        foreach (var (articleId, number, expiryDate) in lots.OrderBy(lot => lot.ArticleId).ThenBy(lot => lot.Number, StringComparer.Ordinal))
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO warehouse.lots (id, company_id, article_id, number, expiry_date)
                VALUES ({Guid.CreateVersion7()}, {companyId.Value}, {articleId}, {number}, {expiryDate})
                ON CONFLICT (company_id, article_id, number) DO NOTHING
                """,
                cancellationToken);
        }
    }

    public async Task<int> NextNumberAsync(CompanyId companyId, StockDocumentType documentType, int year, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var type = documentType.ToString();
        var numbers = await db.Database
            .SqlQuery<int>(
                $"""
                INSERT INTO warehouse.number_counters (company_id, document_type, year, last_number)
                VALUES ({companyId.Value}, {type}, {year}, 1)
                ON CONFLICT (company_id, document_type, year)
                DO UPDATE SET last_number = number_counters.last_number + 1
                RETURNING last_number AS "Value"
                """)
            .ToListAsync(cancellationToken);
        return numbers.Single();
    }

    private void EnsureTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Row locks are held until the transaction ends; begin one first.");
        }
    }
}
