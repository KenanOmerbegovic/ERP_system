using Erp.Modules.Warehouse.Domain.Numbering;
using Erp.Modules.Warehouse.Domain.Stock;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Warehouse.Tests.Domain;

/// <summary>Example-based tests of the cost and posting rules, without a database.</summary>
public class StockDomainTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 2);

    [Fact]
    public void FR_WH_04_weighted_average_of_two_receipts()
    {
        var item = StockItem.Create(Company, Guid.NewGuid(), Guid.NewGuid());

        item.Receive(10m, 20.00m); // 10 @ 2.00
        item.Receive(10m, 30.00m); // 10 @ 3.00

        Assert.Equal(20m, item.Quantity);
        Assert.Equal(50.00m, item.Value);
        Assert.Equal(2.50m, item.AverageCost);
    }

    [Fact]
    public void FR_WH_04_issue_takes_average_cost_rounded_to_fening()
    {
        var item = StockItem.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        item.Receive(3m, 10.00m); // average 3.3333…

        var value = item.Issue(1m);

        Assert.Equal(3.33m, value);
        Assert.Equal(2m, item.Quantity);
        Assert.Equal(6.67m, item.Value);
    }

    [Fact]
    public void FR_WH_04_issuing_everything_leaves_no_value_behind()
    {
        var item = StockItem.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        item.Receive(3m, 10.00m);
        Assert.Equal(3.33m, item.Issue(1m)); // 10.00 / 3
        Assert.Equal(3.34m, item.Issue(1m)); // 6.67 / 2 = 3.335, rounded away from zero

        var last = item.Issue(1m);

        Assert.Equal(3.33m, last); // whatever is left: 10.00 in total
        Assert.Equal(0m, item.Quantity);
        Assert.Equal(0m, item.Value);
    }

    [Fact]
    public void FR_WH_03_issue_beyond_quantity_is_a_bug_in_the_domain()
    {
        var item = StockItem.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        item.Receive(1m, 1.00m);

        Assert.Throws<InvalidOperationException>(() => item.Issue(2m));
    }

    [Fact]
    public void Storno_of_receipt_takes_original_value_while_the_stock_allows()
    {
        var item = StockItem.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        item.Receive(10m, 100.00m); // 10 @ 10
        item.Receive(10m, 10.00m);  // 10 @ 1 → 20, 110

        var removed = item.RemoveAtValue(10m, 10.00m);

        Assert.Equal(10.00m, removed);
        Assert.Equal(10m, item.Quantity);
        Assert.Equal(100.00m, item.Value);
    }

    [Fact]
    public void Storno_of_receipt_never_leaves_value_without_quantity()
    {
        var item = StockItem.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        item.Receive(10m, 100.00m);
        item.Receive(10m, 10.00m);
        item.Issue(10m); // 55.00 out; 10 left worth 55.00

        var removed = item.RemoveAtValue(10m, 10.00m); // reverse the cheap receipt

        Assert.Equal(55.00m, removed); // the remaining 45.00 is a price difference
        Assert.Equal(0m, item.Quantity);
        Assert.Equal(0m, item.Value);
    }

    [Fact]
    public void Storno_of_receipt_never_leaves_negative_value()
    {
        var item = StockItem.Create(Company, Guid.NewGuid(), Guid.NewGuid());
        item.Receive(10m, 100.00m);
        item.Receive(10m, 10.00m);
        item.Issue(15m); // 82.50 out; 5 left worth 27.50

        var removed = item.RemoveAtValue(5m, 100.00m);

        Assert.Equal(27.50m, removed);
        Assert.Equal(0m, item.Value);
    }

    [Fact]
    public void FR_WH_01_number_series_formats_year_and_padded_counter()
    {
        var series = NumberSeries.Create(Company, StockDocumentType.Receipt, new DateOnly(2026, 1, 1), "PRM-", 5);

        Assert.Equal("PRM-2026-00007", series.Format(2026, 7));
    }

    [Fact]
    public void NFR_INT_01_confirmed_document_cannot_be_changed()
    {
        var document = ConfirmedReceipt();

        Assert.Throws<InvalidOperationException>(() => document.UpdateDraft(
            StockDocumentType.Receipt, Today, null, document.TargetStorehouseId, null, [new StockLineInput(Guid.NewGuid(), 1m, 1m, null, null, [])]));
        Assert.Throws<InvalidOperationException>(() => document.Confirm("PRM-2026-00002", Now));
    }

    [Fact]
    public void NFR_INT_01_document_is_reversed_once_and_a_storno_is_not_reversed()
    {
        var document = ConfirmedReceipt();

        var storno = document.CreateStorno(Today, Now);

        Assert.Equal(document.Id, storno.ReversesDocumentId);
        Assert.Equal(storno.Id, document.ReversedByDocumentId);
        Assert.Throws<InvalidOperationException>(() => document.CreateStorno(Today, Now));
        Assert.Throws<InvalidOperationException>(() => storno.CreateStorno(Today, Now));
    }

    [Fact]
    public void FR_WH_01_document_needs_the_warehouses_its_type_moves_between()
    {
        var line = new StockLineInput(Guid.NewGuid(), 1m, null, null, null, []);
        var warehouse = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => StockDocument.CreateDraft(Company, StockDocumentType.Issue, Today, null, warehouse, null, [line], Now));
        Assert.Throws<ArgumentException>(() => StockDocument.CreateDraft(Company, StockDocumentType.Transfer, Today, warehouse, warehouse, null, [line], Now));
    }

    [Fact]
    public void FR_WH_03_posting_reports_every_shortage_and_changes_nothing_it_should_not()
    {
        var warehouse = Guid.NewGuid();
        var articleA = Guid.NewGuid();
        var articleB = Guid.NewGuid();
        var itemA = StockItem.Create(Company, warehouse, articleA);
        itemA.Receive(5m, 10.00m);
        var itemB = StockItem.Create(Company, warehouse, articleB);
        var issue = StockDocument.CreateDraft(
            Company, StockDocumentType.Issue, Today, warehouse, null, null,
            [new StockLineInput(articleA, 6m, null, null, null, []), new StockLineInput(articleB, 1m, null, null, null, [])],
            Now);

        var result = StockPosting.Post(issue, new StockState(Company, [itemA, itemB], [], [], []), Now);

        Assert.False(result.Succeeded);
        Assert.Equal(
            [new StockShortage(warehouse, articleA, null, null, 6m, 5m), new StockShortage(warehouse, articleB, null, null, 1m, 0m)],
            result.Shortages);
        Assert.Equal(5m, itemA.Quantity);
    }

    private static StockDocument ConfirmedReceipt()
    {
        var warehouse = Guid.NewGuid();
        var article = Guid.NewGuid();
        var document = StockDocument.CreateDraft(
            Company, StockDocumentType.Receipt, Today, null, warehouse, null, [new StockLineInput(article, 2m, 1.50m, null, null, [])], Now);
        var posting = StockPosting.Post(document, new StockState(Company, [StockItem.Create(Company, warehouse, article)], [], [], []), Now);
        Assert.True(posting.Succeeded);
        document.Confirm("PRM-2026-00001", Now);
        return document;
    }
}
