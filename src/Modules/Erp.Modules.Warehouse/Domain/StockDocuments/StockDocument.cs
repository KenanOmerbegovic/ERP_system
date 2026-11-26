using Erp.SharedKernel.Domain;

namespace Erp.Modules.Warehouse.Domain.StockDocuments;

/// <summary>
/// A goods receipt, issue or transfer. A draft can be edited or deleted; confirming it moves the stock,
/// gives it its number and makes it immutable (NFR-INT-01). A confirmed document is corrected only by a
/// storno: a new confirmed document of the same type that reverses it.
/// </summary>
public sealed class StockDocument : AggregateRoot<Guid>
{
    private readonly List<StockDocumentLine> _lines = [];

    private StockDocument(Guid id, CompanyId companyId, StockDocumentType type, DateTimeOffset createdAt)
        : base(id, companyId)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown document type.");
        }

        Type = type;
        Status = StockDocumentStatus.Draft;
        CreatedAt = createdAt;
    }

    private StockDocument()
    {
    }

    public StockDocumentType Type { get; private set; }

    public StockDocumentStatus Status { get; private set; }

    /// <summary>Assigned on confirmation from the company's number series; null while a draft.</summary>
    public string? Number { get; private set; }

    public DateOnly DocumentDate { get; private set; }

    /// <summary>Where goods leave from: issues and transfers.</summary>
    public Guid? SourceStorehouseId { get; private set; }

    /// <summary>Where goods arrive: receipts and transfers.</summary>
    public Guid? TargetStorehouseId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Set on a storno: the document it reverses.</summary>
    public Guid? ReversesDocumentId { get; private set; }

    /// <summary>Set on a confirmed document once it is reversed. The only change a confirmed document accepts.</summary>
    public Guid? ReversedByDocumentId { get; private set; }

    /// <summary>
    /// The module whose document this stock document belongs to (e.g. "purchasing"), when it was created through the
    /// Warehouse command contract; null for documents made in Warehouse. Only the owning module reverses it.
    /// </summary>
    public string? SourceModule { get; private set; }

    public Guid? SourceDocumentId { get; private set; }

    public bool IsOwnedByAnotherModule => SourceModule is not null;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public IReadOnlyList<StockDocumentLine> Lines => _lines.AsReadOnly();

    public bool IsStorno => ReversesDocumentId is not null;

    public bool IsDraft => Status == StockDocumentStatus.Draft;

    /// <summary>Sum of the line values in KM, once confirmed.</summary>
    public decimal? TotalValue => IsDraft ? null : _lines.Sum(line => line.Value ?? 0m);

    public static StockDocument CreateDraft(
        CompanyId companyId,
        StockDocumentType type,
        DateOnly documentDate,
        Guid? sourceStorehouseId,
        Guid? targetStorehouseId,
        string? note,
        IReadOnlyList<StockLineInput> lines,
        DateTimeOffset createdAt)
    {
        var document = new StockDocument(Guid.CreateVersion7(), companyId, type, createdAt);
        document.SetContent(type, documentDate, sourceStorehouseId, targetStorehouseId, note, lines);
        return document;
    }

    public void UpdateDraft(
        StockDocumentType type,
        DateOnly documentDate,
        Guid? sourceStorehouseId,
        Guid? targetStorehouseId,
        string? note,
        IReadOnlyList<StockLineInput> lines)
    {
        EnsureDraft();
        SetContent(type, documentDate, sourceStorehouseId, targetStorehouseId, note, lines);
    }

    /// <summary>Called after the stock was posted: every line carries its value by now.</summary>
    public void Confirm(string number, DateTimeOffset confirmedAt)
    {
        EnsureDraft();
        if (_lines.Any(line => line.Value is null))
        {
            throw new InvalidOperationException("Every line must be posted before the document is confirmed.");
        }

        Number = Text.Required(number);
        Status = StockDocumentStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    /// <summary>
    /// A draft storno of this confirmed document, dated <paramref name="documentDate"/>, with the same lines and
    /// their posted values; posting it applies the opposite movements. Marks this document as reversed.
    /// </summary>
    public StockDocument CreateStorno(DateOnly documentDate, DateTimeOffset createdAt)
    {
        if (IsDraft || IsStorno || ReversedByDocumentId is not null)
        {
            throw new InvalidOperationException("Only a confirmed document that is not a storno and not yet reversed can be reversed.");
        }

        if (documentDate < DocumentDate)
        {
            throw new ArgumentException("A storno cannot be dated before the document it reverses.", nameof(documentDate));
        }

        var storno = new StockDocument(Guid.CreateVersion7(), CompanyId, Type, createdAt)
        {
            DocumentDate = documentDate,
            SourceStorehouseId = SourceStorehouseId,
            TargetStorehouseId = TargetStorehouseId,
            Note = Number is null ? null : $"Storno {Number}",
            ReversesDocumentId = Id,
        };
        foreach (var line in _lines)
        {
            storno._lines.Add(line.CopyForStorno(storno.Id));
        }

        ReversedByDocumentId = storno.Id;
        return storno;
    }

    /// <summary>Marks a draft as created for another module's document.</summary>
    public void AssignSource(string sourceModule, Guid sourceDocumentId)
    {
        EnsureDraft();
        SourceModule = Text.Required(sourceModule);
        SourceDocumentId = sourceDocumentId;
    }

    private void SetContent(
        StockDocumentType type,
        DateOnly documentDate,
        Guid? sourceStorehouseId,
        Guid? targetStorehouseId,
        string? note,
        IReadOnlyList<StockLineInput> lines)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown document type.");
        }

        var needsSource = type is StockDocumentType.Issue or StockDocumentType.Transfer;
        var needsTarget = type is StockDocumentType.Receipt or StockDocumentType.Transfer;
        if (needsSource != (sourceStorehouseId is not null) || needsTarget != (targetStorehouseId is not null))
        {
            throw new ArgumentException($"A {type} has the wrong warehouses.", nameof(sourceStorehouseId));
        }

        if (type == StockDocumentType.Transfer && sourceStorehouseId == targetStorehouseId)
        {
            throw new ArgumentException("A transfer needs two different warehouses.", nameof(targetStorehouseId));
        }

        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("A document needs at least one line.", nameof(lines));
        }

        Type = type;
        DocumentDate = documentDate;
        SourceStorehouseId = sourceStorehouseId;
        TargetStorehouseId = targetStorehouseId;
        Note = Text.Optional(note);
        _lines.Clear();
        for (var index = 0; index < lines.Count; index++)
        {
            _lines.Add(StockDocumentLine.Create(CompanyId, Id, index + 1, type, lines[index]));
        }
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("A confirmed document is immutable; reverse it with a storno.");
        }
    }
}

public enum StockDocumentType
{
    Receipt,
    Issue,
    Transfer,
}

public enum StockDocumentStatus
{
    Draft,
    Confirmed,
}
