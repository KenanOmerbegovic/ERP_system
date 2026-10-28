namespace Erp.Modules.Core.Contracts.UnitsOfMeasure;

/// <summary>Body of create and update requests.</summary>
public sealed record UnitOfMeasureRequest(string Code, string Name);

public sealed record UnitOfMeasureResponse(Guid Id, string Code, string Name);
