using System.Text.Json;
using Erp.Messaging.Outbox;
using Erp.Modules.Core.Application;
using Erp.Modules.Core.Application.Articles;
using Erp.Modules.Core.Application.Vat;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Core.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Tests.Application;

/// <summary>VAT rates (configuration for FR-PUR-02 and FR-SAL-04), article default VAT codes, and partner events for Purchasing.</summary>
public sealed class VatRateTests : IDisposable
{
    private readonly CoreTestDatabase _database = new();
    private readonly CoreHandlers _handlers;

    public VatRateTests()
    {
        _handlers = new CoreHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task FR_PUR_02_rates_are_effective_dated_and_never_edited()
    {
        var companyId = await _handlers.ACompanyAsync();
        await Create(companyId, "PDV17", 0.17m, new DateOnly(2006, 1, 1));
        await Create(companyId, "PDV17", 0.18m, new DateOnly(2027, 1, 1));
        var lookup = new VatRateLookup(_database.Query());

        Assert.Equal(0.17m, (await lookup.FindAsync(companyId, "PDV17", new DateOnly(2026, 12, 31), CancellationToken.None))!.Rate);
        Assert.Equal(0.18m, (await lookup.FindAsync(companyId, " PDV17 ", new DateOnly(2027, 1, 1), CancellationToken.None))!.Rate);
        Assert.Null(await lookup.FindAsync(companyId, "PDV17", new DateOnly(2005, 12, 31), CancellationToken.None));
        Assert.Null(await lookup.FindAsync(await _handlers.ACompanyAsync(taxId: "4200000000002"), "PDV17", new DateOnly(2026, 1, 1), CancellationToken.None));
        var list = await new ListVatRatesHandler(_database.Query()).HandleAsync(new ListVatRates(companyId), CancellationToken.None);
        Assert.Equal([0.17m, 0.18m], list.Select(r => r.Rate));
    }

    [Fact]
    public async Task Duplicate_start_date_and_invalid_rates_are_rejected()
    {
        var companyId = await _handlers.ACompanyAsync();
        await Create(companyId, "PDV17", 0.17m, new DateOnly(2006, 1, 1));

        var duplicate = await Handler().HandleAsync(new CreateVatRate(companyId, "PDV17", "Opća", 0.18m, new DateOnly(2006, 1, 1)), CancellationToken.None);
        var tooHigh = await Handler().HandleAsync(new CreateVatRate(companyId, "X", "X", 1.5m, new DateOnly(2006, 1, 1)), CancellationToken.None);

        ResultAssert.Fails(duplicate, CoreErrors.DuplicateVatRate);
        ResultAssert.FailsValidation(tooHigh, "Rate", "InclusiveBetweenValidator");
    }

    [Fact]
    public async Task Article_default_vat_code_must_be_configured_and_is_published()
    {
        var companyId = await _handlers.ACompanyAsync();
        var unitId = await _handlers.AUnitAsync(companyId);
        await Create(companyId, "PDV17", 0.17m, new DateOnly(2006, 1, 1));

        var unknown = await _handlers.CreateArticle.HandleAsync(
            new CreateArticle(companyId, "A001", "Vijak", ArticleType.Goods, TrackingType.None, unitId, "PDV99", IsActive: true), CancellationToken.None);
        var created = await _handlers.CreateArticle.HandleAsync(
            new CreateArticle(companyId, "A001", "Vijak", ArticleType.Goods, TrackingType.None, unitId, "PDV17", IsActive: true), CancellationToken.None);

        ResultAssert.Fails(unknown, CoreErrors.ArticleVatCodeNotFound);
        Assert.Equal("PDV17", (await _database.Query().Articles.SingleAsync()).DefaultVatCode);
        Assert.Equal("PDV17", Assert.Single((await OutboxEventsAsync()).OfType<ArticleCreated>(), e => e.ArticleId == created.Value).DefaultVatCode);
    }

    [Fact]
    public async Task FR_PUR_01_business_partners_are_published_for_other_modules()
    {
        var companyId = await _handlers.ACompanyAsync();
        var partnerId = await _handlers.APartnerAsync(companyId, code: "D001", name: "Dobavljač d.o.o.");

        var events = await OutboxEventsAsync();

        var created = Assert.Single(events.OfType<BusinessPartnerCreated>());
        Assert.Equal((partnerId, companyId.Value, "D001", "Dobavljač d.o.o."), (created.BusinessPartnerId, created.CompanyId, created.Code, created.Name));
    }

    private CreateVatRateHandler Handler() => new(_database.Db, new CreateVatRateValidator());

    private async Task Create(Erp.SharedKernel.Domain.CompanyId companyId, string code, decimal rate, DateOnly validFrom)
    {
        var result = await Handler().HandleAsync(new CreateVatRate(companyId, code, "Opća stopa", rate, validFrom), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    private async Task<IReadOnlyList<object>> OutboxEventsAsync() =>
        (await _database.Query().Set<OutboxMessage>().OrderBy(message => message.OccurredAt).ToListAsync())
            .Select(message => JsonSerializer.Deserialize(message.Content, typeof(ArticleCreated).Assembly.GetType(message.Type)!, JsonSerializerOptions.Web)!)
            .ToList();
}
