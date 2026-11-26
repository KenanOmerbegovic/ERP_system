using Erp.Modules.Core.Contracts.Vat;
using Erp.SharedKernel.Monetary;
using Erp.SharedKernel.Results;

namespace Erp.Localization.BiH.Vat;

/// <summary>
/// PDV in Bosnia and Herzegovina (Zakon o PDV-u): one standard rate plus exempt supplies; the rates themselves are company
/// configuration (Core's VAT rates, effective-dated), looked up for the document date. Each line's PDV is its net amount
/// × the rate, rounded to fening (commercial rounding); totals per rate are the sums of their lines, so an invoice's PDV
/// always equals the sum of its lines.
/// </summary>
public sealed class BiHVatCalculator(IVatRateLookup rates) : IVatCalculator
{
    public async Task<Result<VatCalculation>> CalculateAsync(VatCalculationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rateByCode = new Dictionary<string, VatRateInfo>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var code in request.Lines.Select(line => line.VatCode.Trim()).Distinct(StringComparer.Ordinal))
        {
            var rate = await rates.FindAsync(request.CompanyId, code, request.Date, cancellationToken);
            if (rate is null)
            {
                missing.Add(code);
            }
            else
            {
                rateByCode[code] = rate;
            }
        }

        if (missing.Count > 0)
        {
            var keys = request.Lines.Where(line => !rateByCode.ContainsKey(line.VatCode.Trim())).Select(line => line.Key).ToList();
            return new VatRateNotFoundError(missing, request.Date, keys);
        }

        var lines = request.Lines
            .Select(line =>
            {
                var rate = rateByCode[line.VatCode.Trim()];
                var net = Money.Km(line.NetAmount).Round();
                var vat = (net * rate.Rate).Round();
                return new VatLineResult(line.Key, net.Amount, rate.Code, rate.Rate, vat.Amount);
            })
            .ToList();
        var totals = lines
            .GroupBy(line => (line.VatCode, line.Rate))
            .OrderByDescending(group => group.Key.Rate)
            .ThenBy(group => group.Key.VatCode, StringComparer.Ordinal)
            .Select(group => new VatRateTotal(group.Key.VatCode, group.Key.Rate, group.Sum(l => l.NetAmount), group.Sum(l => l.VatAmount)))
            .ToList();
        var net = lines.Sum(line => line.NetAmount);
        var vatTotal = lines.Sum(line => line.VatAmount);
        return new VatCalculation(lines, totals, net, vatTotal, net + vatTotal);
    }
}
