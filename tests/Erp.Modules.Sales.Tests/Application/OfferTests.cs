using Erp.Modules.Sales.Application;
using Erp.Modules.Sales.Application.Offers;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Domain.Offers;
using Erp.Modules.Sales.Tests.TestSupport;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using static Erp.Modules.Sales.Tests.TestSupport.SalesScenario;

namespace Erp.Modules.Sales.Tests.Application;

[Collection(SalesCollection.Name)]
public sealed class OfferTests(SalesHost host)
{
    private readonly SalesScenario _scenario = new(host);

    [Fact]
    public async Task FR_SAL_01_offer_is_saved_as_draft_with_the_articles_default_vat_code()
    {
        await _scenario.NumberSeriesAsync();
        var customer = await _scenario.CustomerAsync();
        var article = await _scenario.ArticleAsync(vatCode: "OSL");
        var service = await _scenario.ServiceAsync();

        var created = await _scenario.DraftOfferAsync(customer, Line(article, 2m, 10m), Line(service, 1m, 50m, "PDV17"));

        ResultAssert.Succeeds(created);
        var offer = await Get(created.Value);
        Assert.Equal(OfferState.Draft, offer.Status);
        Assert.Null(offer.Number);
        Assert.Equal(70m, offer.NetAmount);
        Assert.Equal([("OSL", Erp.Modules.Core.Contracts.Articles.ArticleKind.Goods), ("PDV17", Erp.Modules.Core.Contracts.Articles.ArticleKind.Service)],
            offer.Lines.Select(l => (l.VatCode, l.Kind)));
    }

    [Fact]
    public async Task FR_SAL_01_offer_needs_an_active_customer_and_known_vat_codes()
    {
        var supplierOnly = await _scenario.CustomerAsync("D001", isCustomer: false);
        var article = await _scenario.ArticleAsync(vatCode: null);

        var notCustomer = await _scenario.DraftOfferAsync(supplierOnly, Line(article, 1m, 1m, "PDV17"));
        var noVat = await _scenario.DraftOfferAsync(await _scenario.CustomerAsync(), Line(article, 1m, 1m));
        var unknownVat = await _scenario.DraftOfferAsync(await _scenario.CustomerAsync("K002"), Line(article, 1m, 1m, "PDV99"));

        ResultAssert.FailsValidation(notCustomer, "CustomerId", SalesValidationCodes.NotACustomer);
        ResultAssert.FailsValidation(noVat, "Lines[0].VatCode", SalesValidationCodes.VatCodeRequired);
        ResultAssert.FailsValidation(unknownVat, "Lines[0].VatCode", SalesValidationCodes.VatRateNotFound);
    }

    [Fact]
    public async Task FR_SAL_01_sending_numbers_the_offer_and_freezes_it()
    {
        await _scenario.NumberSeriesAsync();
        var customer = await _scenario.CustomerAsync();
        var article = await _scenario.ArticleAsync();
        var offer = (await _scenario.DraftOfferAsync(customer, Line(article, 1m, 10m))).Value;

        ResultAssert.Succeeds(await _scenario.SendOfferAsync(offer));
        var update = await _scenario.RunAsync<UpdateOfferHandler, Result>(h => h.HandleAsync(
            new UpdateOffer(_scenario.CompanyId, offer, customer, _scenario.Today, null, null, [Line(article, 5m, 10m)]), CancellationToken.None));
        var delete = await _scenario.RunAsync<DeleteOfferHandler, Result>(h => h.HandleAsync(new DeleteOffer(_scenario.CompanyId, offer), CancellationToken.None));

        Assert.Equal(("PON-2026-00001", OfferState.Sent), ((await Get(offer)).Number, (await Get(offer)).Status));
        ResultAssert.Fails(update, SalesErrors.OfferNotDraft);
        ResultAssert.Fails(delete, SalesErrors.OfferNotDraft);
    }

    [Fact]
    public async Task FR_SAL_02_accepted_offer_becomes_a_draft_order_with_its_lines()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var service = await _scenario.ServiceAsync();
        var offer = (await _scenario.DraftOfferAsync(customer, Line(article, 3m, 12.5m), Line(service, 1m, 40m))).Value;
        ResultAssert.Succeeds(await _scenario.SendOfferAsync(offer));

        var accepted = await _scenario.AcceptOfferAsync(offer, warehouse);

        ResultAssert.Succeeds(accepted);
        var order = await _scenario.RunAsync<GetSalesOrderHandler, Result<Contracts.Orders.SalesOrderResponse>>(h =>
            h.HandleAsync(new GetSalesOrder(_scenario.CompanyId, accepted.Value), CancellationToken.None));
        Assert.Equal(SalesDocumentState.Draft, order.Value.Status);
        Assert.Equal((offer, "PON-2026-00001", customer, (Guid?)warehouse), (order.Value.OfferId!.Value, order.Value.OfferNumber, order.Value.CustomerId, order.Value.WarehouseId));
        Assert.Equal([(article, 3m, 12.5m), (service, 1m, 40m)], order.Value.Lines.Select(l => (l.ArticleId, l.Quantity, l.UnitPrice)));
        var decided = await Get(offer);
        Assert.Equal((OfferState.Accepted, (Guid?)accepted.Value), (decided.Status, decided.SalesOrderId));
    }

    [Fact]
    public async Task FR_SAL_02_offer_gives_at_most_one_order_and_a_rejected_offer_none()
    {
        await _scenario.NumberSeriesAsync();
        var customer = await _scenario.CustomerAsync();
        var article = await _scenario.ArticleAsync();
        var accepted = (await _scenario.DraftOfferAsync(customer, Line(article, 1m, 10m))).Value;
        var rejected = (await _scenario.DraftOfferAsync(customer, Line(article, 1m, 10m))).Value;
        var draft = (await _scenario.DraftOfferAsync(customer, Line(article, 1m, 10m))).Value;
        ResultAssert.Succeeds(await _scenario.SendOfferAsync(accepted));
        ResultAssert.Succeeds(await _scenario.SendOfferAsync(rejected));
        ResultAssert.Succeeds(await _scenario.AcceptOfferAsync(accepted));
        ResultAssert.Succeeds(await _scenario.RunAsync<RejectOfferHandler, Result>(h => h.HandleAsync(new RejectOffer(_scenario.CompanyId, rejected), CancellationToken.None)));

        ResultAssert.Fails(await _scenario.AcceptOfferAsync(accepted), SalesErrors.OfferNotSent);
        ResultAssert.Fails(await _scenario.AcceptOfferAsync(rejected), SalesErrors.OfferNotSent);
        ResultAssert.Fails(await _scenario.AcceptOfferAsync(draft), SalesErrors.OfferNotSent);
        Assert.Equal(1, await _scenario.QueryAsync(db => db.SalesOrders.CountAsync(o => o.CompanyId == _scenario.CompanyId)));
    }

    [Fact]
    public async Task FR_SAL_02_deleting_the_draft_order_undoes_the_acceptance()
    {
        await _scenario.NumberSeriesAsync();
        var customer = await _scenario.CustomerAsync();
        var article = await _scenario.ArticleAsync();
        var offer = (await _scenario.DraftOfferAsync(customer, Line(article, 1m, 10m))).Value;
        ResultAssert.Succeeds(await _scenario.SendOfferAsync(offer));
        var order = (await _scenario.AcceptOfferAsync(offer)).Value;

        ResultAssert.Succeeds(await _scenario.RunAsync<DeleteSalesOrderHandler, Result>(h => h.HandleAsync(new DeleteSalesOrder(_scenario.CompanyId, order), CancellationToken.None)));

        var reopened = await Get(offer);
        Assert.Equal((OfferState.Sent, (Guid?)null), (reopened.Status, reopened.SalesOrderId));
        ResultAssert.Succeeds(await _scenario.AcceptOfferAsync(offer));
    }

    [Fact]
    public async Task FR_SAL_01_sending_without_a_number_series_fails_and_keeps_the_draft()
    {
        var customer = await _scenario.CustomerAsync();
        var article = await _scenario.ArticleAsync();
        var offer = (await _scenario.DraftOfferAsync(customer, Line(article, 1m, 10m))).Value;

        ResultAssert.Fails(await _scenario.SendOfferAsync(offer), SalesErrors.NumberSeriesNotConfigured);
        Assert.Equal(OfferStatus.Draft, await _scenario.QueryAsync(db => db.Offers.Where(o => o.Id == offer).Select(o => o.Status).FirstAsync()));
    }

    private async Task<Contracts.Offers.OfferResponse> Get(Guid offer)
    {
        var result = await _scenario.RunAsync<GetOfferHandler, Result<Contracts.Offers.OfferResponse>>(h =>
            h.HandleAsync(new GetOffer(_scenario.CompanyId, offer), CancellationToken.None));
        ResultAssert.Succeeds(result);
        return result.Value;
    }
}
