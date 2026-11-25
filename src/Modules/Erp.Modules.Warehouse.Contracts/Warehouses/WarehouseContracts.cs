namespace Erp.Modules.Warehouse.Contracts.Warehouses;

/// <summary>Body of create and update requests.</summary>
public sealed record WarehouseRequest(string Code, string Name, bool IsActive);

public sealed record WarehouseResponse(Guid Id, string Code, string Name, bool IsActive);
