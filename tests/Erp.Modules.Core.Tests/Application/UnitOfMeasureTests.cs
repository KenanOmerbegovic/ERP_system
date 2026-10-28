using Erp.Modules.Core.Application;
using Erp.Modules.Core.Application.UnitsOfMeasure;
using Erp.Modules.Core.Tests.TestSupport;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Tests.Application;

public sealed class UnitOfMeasureTests : IDisposable
{
    private readonly CoreTestDatabase _database = new();
    private readonly CoreHandlers _handlers;

    public UnitOfMeasureTests()
    {
        _handlers = new CoreHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Create_saves_unit_in_company()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateUnitOfMeasure.HandleAsync(
            new CreateUnitOfMeasure(companyId, " kg ", " Kilogram "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var unit = await _database.Query().UnitsOfMeasure.SingleAsync();
        Assert.Equal(companyId, unit.CompanyId);
        Assert.Equal("kg", unit.Code);
        Assert.Equal("Kilogram", unit.Name);
    }

    [Fact]
    public async Task Create_validates_input()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateUnitOfMeasure.HandleAsync(
            new CreateUnitOfMeasure(companyId, new string('x', 11), ""),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "Code", "MaximumLengthValidator");
        ResultAssert.FailsValidation(result, "Name", "NotEmptyValidator");
    }

    [Fact]
    public async Task Create_in_unknown_company_is_not_found()
    {
        var result = await _handlers.CreateUnitOfMeasure.HandleAsync(
            new CreateUnitOfMeasure(CompanyId.New(), "kom", "Komad"),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.CompanyNotFound);
    }

    [Fact]
    public async Task Create_rejects_duplicate_code_in_same_company()
    {
        var companyId = await _handlers.ACompanyAsync();
        await _handlers.AUnitAsync(companyId, code: "kom");

        var result = await _handlers.CreateUnitOfMeasure.HandleAsync(
            new CreateUnitOfMeasure(companyId, "kom", "Komad"),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.DuplicateUnitOfMeasureCode);
    }

    [Fact]
    public async Task Update_changes_fields()
    {
        var companyId = await _handlers.ACompanyAsync();
        var unitId = await _handlers.AUnitAsync(companyId, code: "kom");

        var result = await _handlers.UpdateUnitOfMeasure.HandleAsync(
            new UpdateUnitOfMeasure(companyId, unitId, "pak", "Paket"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var unit = await _database.Query().UnitsOfMeasure.SingleAsync();
        Assert.Equal("pak", unit.Code);
        Assert.Equal("Paket", unit.Name);
    }

    [Fact]
    public async Task Update_rejects_code_of_another_unit()
    {
        var companyId = await _handlers.ACompanyAsync();
        await _handlers.AUnitAsync(companyId, code: "kom");
        var kgId = await _handlers.AUnitAsync(companyId, code: "kg", name: "Kilogram");

        var result = await _handlers.UpdateUnitOfMeasure.HandleAsync(
            new UpdateUnitOfMeasure(companyId, kgId, "kom", "Kilogram"),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.DuplicateUnitOfMeasureCode);
    }

    [Fact]
    public async Task Update_cannot_reach_unit_of_another_company()
    {
        var owner = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var other = await _handlers.ACompanyAsync(taxId: "4200000000002");
        var unitId = await _handlers.AUnitAsync(owner);

        var result = await _handlers.UpdateUnitOfMeasure.HandleAsync(
            new UpdateUnitOfMeasure(other, unitId, "kom", "Komad"),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.UnitOfMeasureNotFound);
    }

    [Fact]
    public async Task Update_validates_input()
    {
        var companyId = await _handlers.ACompanyAsync();
        var unitId = await _handlers.AUnitAsync(companyId);

        var result = await _handlers.UpdateUnitOfMeasure.HandleAsync(
            new UpdateUnitOfMeasure(companyId, unitId, "", "Komad"),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "Code", "NotEmptyValidator");
    }

    [Fact]
    public async Task List_returns_only_units_of_the_company_ordered_by_code()
    {
        var companyId = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var otherId = await _handlers.ACompanyAsync(taxId: "4200000000002");
        await _handlers.AUnitAsync(companyId, code: "kom");
        await _handlers.AUnitAsync(companyId, code: "kg", name: "Kilogram");
        await _handlers.AUnitAsync(otherId, code: "l", name: "Litar");

        var result = await _handlers.ListUnitsOfMeasure.HandleAsync(new ListUnitsOfMeasure(companyId), CancellationToken.None);

        Assert.Equal(["kg", "kom"], result.Value.Select(u => u.Code));
    }

    [Fact]
    public async Task List_of_unknown_company_is_not_found()
    {
        var result = await _handlers.ListUnitsOfMeasure.HandleAsync(new ListUnitsOfMeasure(CompanyId.New()), CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.CompanyNotFound);
    }
}
