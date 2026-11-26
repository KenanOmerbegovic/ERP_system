using Erp.Localization.BiH.Vat;
using Erp.Modules.Core.Contracts.Vat;
using Erp.SharedKernel.Domain;

namespace Erp.Localization.BiH.Tests.Vat;

/// <summary>FR-PUR-02 / FR-SAL-04: PDV on invoice lines by the configured, effective-dated rates (example-based).</summary>
public class BiHVatCalculatorTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly DateOnly Date = new(2026, 11, 25);

    private readonly FakeRates _rates = new(
        new VatRateInfo("PDV17", "Opća stopa", 0.17m, new DateOnly(2006, 1, 1)),
        new VatRateInfo("OSL", "Oslobođeno", 0m, new DateOnly(2006, 1, 1)));

    [Fact]
    public async Task FR_PUR_02_standard_rate_is_applied_per_line_and_rounded_to_fening()
    {
        var result = await Calculate(new VatLineInput("1", 100.00m, "PDV17"), new VatLineInput("2", 0.10m, "PDV17"), new VatLineInput("3", 12.34m, "PDV17"));

        Assert.True(result.IsSuccess);
        Assert.Equal([17.00m, 0.02m, 2.10m], result.Value.Lines.Select(line => line.VatAmount)); // 0.017 → 0.02, 2.0978 → 2.10
        var total = Assert.Single(result.Value.Totals);
        Assert.Equal(("PDV17", 0.17m, 112.44m, 19.12m), (total.VatCode, total.Rate, total.Base, total.VatAmount));
        Assert.Equal((112.44m, 19.12m, 131.56m), (result.Value.NetAmount, result.Value.VatAmount, result.Value.GrossAmount));
    }

    [Fact]
    public async Task FR_PUR_02_exempt_lines_have_a_zero_base_line_of_their_own()
    {
        var result = await Calculate(new VatLineInput("1", 50m, "OSL"), new VatLineInput("2", 10m, "PDV17"));

        Assert.Equal([("PDV17", 10m, 1.70m), ("OSL", 50m, 0m)], result.Value.Totals.Select(t => (t.VatCode, t.Base, t.VatAmount)));
        Assert.Equal(61.70m, result.Value.GrossAmount);
    }

    [Fact]
    public async Task FR_PUR_02_rate_valid_on_the_document_date_applies()
    {
        _rates.Add(new VatRateInfo("PDV17", "Opća stopa", 0.18m, new DateOnly(2026, 12, 1)));

        var before = await Calculate(new DateOnly(2026, 11, 30), new VatLineInput("1", 100m, "PDV17"));
        var after = await Calculate(new DateOnly(2026, 12, 1), new VatLineInput("1", 100m, "PDV17"));

        Assert.Equal(17m, before.Value.VatAmount);
        Assert.Equal(18m, after.Value.VatAmount);
    }

    [Fact]
    public async Task FR_PUR_02_unknown_code_fails()
    {
        var result = await Calculate(new VatLineInput("1", 100m, "PDV99"));

        Assert.True(result.IsFailure);
        Assert.Equal(VatErrors.RateNotFoundCode, result.Error.Code);
    }

    private Task<Erp.SharedKernel.Results.Result<VatCalculation>> Calculate(params VatLineInput[] lines) => Calculate(Date, lines);

    private Task<Erp.SharedKernel.Results.Result<VatCalculation>> Calculate(DateOnly date, params VatLineInput[] lines) =>
        new BiHVatCalculator(_rates).CalculateAsync(new VatCalculationRequest(Company, date, lines), CancellationToken.None);

    private sealed class FakeRates(params VatRateInfo[] rates) : IVatRateLookup
    {
        private readonly List<VatRateInfo> _rates = [.. rates];

        public void Add(VatRateInfo rate) => _rates.Add(rate);

        public Task<VatRateInfo?> FindAsync(CompanyId companyId, string code, DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult(_rates.Where(r => r.Code == code && r.ValidFrom <= date).MaxBy(r => r.ValidFrom));
    }
}
