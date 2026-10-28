using Erp.SharedKernel.Results;

namespace Erp.SharedKernel.Monetary;

public static class MoneyErrors
{
    public static Error UnknownCurrency(string? code) =>
        new("Money.UnknownCurrency", $"Unknown currency '{code}'.");
}
