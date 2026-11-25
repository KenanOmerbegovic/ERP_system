using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Warehouse.Domain.Stock;

/// <summary>A document would take more than is in stock: of an article, of a LOT, or a serial number that is not there.</summary>
public sealed record StockShortage(
    Guid StorehouseId,
    Guid ArticleId,
    string? LotNumber,
    string? SerialNumber,
    decimal Requested,
    decimal Available);

/// <summary>A document contradicts the stock in another way, e.g. receives a serial number that is already in stock.</summary>
public sealed record StockConflict(string Code, Guid ArticleId, string? LotNumber, string? SerialNumber);

public static class StockConflictCodes
{
    public const string SerialNumberInStock = "SerialNumber.AlreadyInStock";
    public const string LotExpiryMismatch = "Lot.ExpiryMismatch";
}

/// <summary>
/// The stock rows one document touches, loaded (and locked) by the caller: the stock items of every warehouse and
/// article on it, its LOTs and serial numbers, and the existing LOT balances. LOT balances it creates are collected
/// in <see cref="AddedLotBalances"/>.
/// </summary>
public sealed class StockState
{
    private readonly CompanyId _companyId;
    private readonly Dictionary<(Guid Storehouse, Guid Article), StockItem> _items;
    private readonly Dictionary<(Guid Article, string Number), Lot> _lots;
    private readonly Dictionary<(Guid Storehouse, Guid Lot), LotBalance> _lotBalances;
    private readonly Dictionary<(Guid Article, string Number), SerialNumber> _serials;
    private readonly List<LotBalance> _added = [];

    public StockState(
        CompanyId companyId,
        IEnumerable<StockItem> items,
        IEnumerable<Lot> lots,
        IEnumerable<LotBalance> lotBalances,
        IEnumerable<SerialNumber> serials)
    {
        _companyId = companyId;
        _items = items.ToDictionary(item => (item.StorehouseId, item.ArticleId));
        _lots = lots.ToDictionary(lot => (lot.ArticleId, lot.Number));
        _lotBalances = lotBalances.ToDictionary(balance => (balance.StorehouseId, balance.LotId));
        _serials = serials.ToDictionary(serial => (serial.ArticleId, serial.Number));
    }

    public IReadOnlyList<LotBalance> AddedLotBalances => _added.AsReadOnly();

    internal StockItem Item(Guid storehouseId, Guid articleId) =>
        _items.TryGetValue((storehouseId, articleId), out var item)
            ? item
            : throw new InvalidOperationException($"Stock item {storehouseId}/{articleId} was not loaded.");

    internal Lot Lot(Guid articleId, string number) =>
        _lots.TryGetValue((articleId, number), out var lot)
            ? lot
            : throw new InvalidOperationException($"LOT {number} of article {articleId} was not loaded.");

    internal SerialNumber Serial(Guid articleId, string number) =>
        _serials.TryGetValue((articleId, number), out var serial)
            ? serial
            : throw new InvalidOperationException($"Serial number {number} of article {articleId} was not loaded.");

    internal decimal LotQuantity(Guid storehouseId, Guid lotId) =>
        _lotBalances.TryGetValue((storehouseId, lotId), out var balance) ? balance.Quantity : 0m;

    internal LotBalance LotBalanceFor(Guid storehouseId, Guid articleId, Guid lotId)
    {
        if (!_lotBalances.TryGetValue((storehouseId, lotId), out var balance))
        {
            balance = LotBalance.Create(_companyId, storehouseId, articleId, lotId);
            _lotBalances.Add((storehouseId, lotId), balance);
            _added.Add(balance);
        }

        return balance;
    }
}

public sealed record PostingResult(
    IReadOnlyList<StockShortage> Shortages,
    IReadOnlyList<StockConflict> Conflicts,
    IReadOnlyList<StockMovement> Movements)
{
    public bool Succeeded => Shortages.Count == 0 && Conflicts.Count == 0;
}

/// <summary>
/// Applies a draft's stock movements to <see cref="StockState"/> and records each line's value. If anything is short
/// or conflicts, the result lists every problem and the caller saves nothing.
/// <list type="bullet">
/// <item>Receipt: in at quantity × unit cost. Its storno takes the goods out at that value (see <see cref="StockItem.RemoveAtValue"/>).</item>
/// <item>Issue: out at the weighted-average cost. Its storno brings them back at that value.</item>
/// <item>Transfer: out of the source at its average, into the target at the same value. Its storno moves them back at the value
/// the target can give up, so a transfer never creates or destroys value.</item>
/// </list>
/// </summary>
public static class StockPosting
{
    public static PostingResult Post(StockDocument document, StockState state, DateTimeOffset postedAt)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(state);
        if (!document.IsDraft)
        {
            throw new InvalidOperationException("Only a draft can be posted.");
        }

        var run = new Run(document, state, postedAt);
        foreach (var line in document.Lines)
        {
            run.Post(line);
        }

        return new PostingResult(run.Shortages, run.Conflicts, run.Movements);
    }

    private sealed class Run(StockDocument document, StockState state, DateTimeOffset postedAt)
    {
        public List<StockShortage> Shortages { get; } = [];

        public List<StockConflict> Conflicts { get; } = [];

        public List<StockMovement> Movements { get; } = [];

        private Guid Source => document.SourceStorehouseId ?? throw new InvalidOperationException("The document has no source warehouse.");

        private Guid Target => document.TargetStorehouseId ?? throw new InvalidOperationException("The document has no target warehouse.");

        public void Post(StockDocumentLine line)
        {
            var original = document.IsStorno
                ? line.OriginalValue ?? throw new InvalidOperationException("A storno line carries the reversed line's value.")
                : 0m;

            switch (document.Type, document.IsStorno)
            {
                case (StockDocumentType.Receipt, false):
                    if (CanBringIn(line, checkLotExpiry: true))
                    {
                        var value = line.ReceiptValue;
                        BringIn(line, Target, value);
                        line.RecordPosting(value, 0m);
                    }

                    break;
                case (StockDocumentType.Receipt, true):
                    if (CanTakeOut(line, Target))
                    {
                        var removed = TakeOut(line, Target, original);
                        line.RecordPosting(original, original - removed);
                    }

                    break;
                case (StockDocumentType.Issue, false):
                    if (CanTakeOut(line, Source))
                    {
                        line.RecordPosting(TakeOut(line, Source, atValue: null), 0m);
                    }

                    break;
                case (StockDocumentType.Issue, true):
                    if (CanBringIn(line, checkLotExpiry: false))
                    {
                        BringIn(line, Source, original);
                        line.RecordPosting(original, 0m);
                    }

                    break;
                case (StockDocumentType.Transfer, false):
                    if (CanTakeOut(line, Source))
                    {
                        var value = TakeOut(line, Source, atValue: null);
                        BringIn(line, Target, value);
                        line.RecordPosting(value, 0m);
                    }

                    break;
                case (StockDocumentType.Transfer, true):
                    if (CanTakeOut(line, Target))
                    {
                        var value = TakeOut(line, Target, original);
                        BringIn(line, Source, value);
                        line.RecordPosting(value, 0m);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unknown document type {document.Type}.");
            }
        }

        private bool CanTakeOut(StockDocumentLine line, Guid storehouseId)
        {
            var before = Shortages.Count;
            var item = state.Item(storehouseId, line.ArticleId);
            if (!item.HasAvailable(line.Quantity))
            {
                Shortages.Add(new StockShortage(storehouseId, line.ArticleId, null, null, line.Quantity, item.Quantity));
            }

            if (line.LotNumber is { } lotNumber)
            {
                var available = state.LotQuantity(storehouseId, state.Lot(line.ArticleId, lotNumber).Id);
                if (available < line.Quantity)
                {
                    Shortages.Add(new StockShortage(storehouseId, line.ArticleId, lotNumber, null, line.Quantity, available));
                }
            }

            foreach (var number in line.SerialNumbers)
            {
                if (!state.Serial(line.ArticleId, number).IsIn(storehouseId))
                {
                    Shortages.Add(new StockShortage(storehouseId, line.ArticleId, null, number, 1m, 0m));
                }
            }

            return Shortages.Count == before;
        }

        private bool CanBringIn(StockDocumentLine line, bool checkLotExpiry)
        {
            var before = Conflicts.Count;
            if (checkLotExpiry && line is { LotNumber: { } lotNumber, LotExpiryDate: { } expiry }
                && state.Lot(line.ArticleId, lotNumber).ExpiryDate != expiry)
            {
                Conflicts.Add(new StockConflict(StockConflictCodes.LotExpiryMismatch, line.ArticleId, lotNumber, null));
            }

            foreach (var number in line.SerialNumbers)
            {
                if (state.Serial(line.ArticleId, number).InStock)
                {
                    Conflicts.Add(new StockConflict(StockConflictCodes.SerialNumberInStock, line.ArticleId, null, number));
                }
            }

            return Conflicts.Count == before;
        }

        /// <summary>Returns the value taken: the average cost, or <paramref name="atValue"/> as far as the stock allows.</summary>
        private decimal TakeOut(StockDocumentLine line, Guid storehouseId, decimal? atValue)
        {
            var item = state.Item(storehouseId, line.ArticleId);
            var value = atValue is { } fixedValue ? item.RemoveAtValue(line.Quantity, fixedValue) : item.Issue(line.Quantity);
            var lotId = line.LotNumber is { } lotNumber ? state.Lot(line.ArticleId, lotNumber).Id : (Guid?)null;
            if (lotId is { } id)
            {
                state.LotBalanceFor(storehouseId, line.ArticleId, id).Remove(line.Quantity);
            }

            foreach (var number in line.SerialNumbers)
            {
                state.Serial(line.ArticleId, number).MoveOut(storehouseId);
            }

            Record(line, storehouseId, lotId, -line.Quantity, -value);
            return value;
        }

        private void BringIn(StockDocumentLine line, Guid storehouseId, decimal value)
        {
            state.Item(storehouseId, line.ArticleId).Receive(line.Quantity, value);
            var lotId = line.LotNumber is { } lotNumber ? state.Lot(line.ArticleId, lotNumber).Id : (Guid?)null;
            if (lotId is { } id)
            {
                state.LotBalanceFor(storehouseId, line.ArticleId, id).Add(line.Quantity);
            }

            foreach (var number in line.SerialNumbers)
            {
                state.Serial(line.ArticleId, number).MoveIn(storehouseId);
            }

            Record(line, storehouseId, lotId, line.Quantity, value);
        }

        private void Record(StockDocumentLine line, Guid storehouseId, Guid? lotId, decimal quantity, decimal value) =>
            Movements.Add(StockMovement.Create(
                document.CompanyId, document.Id, line.Id, document.DocumentDate, storehouseId, line.ArticleId, lotId, quantity, value, postedAt));
    }
}
