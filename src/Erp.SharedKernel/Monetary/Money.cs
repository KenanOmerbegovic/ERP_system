using System.Globalization;

namespace Erp.SharedKernel.Monetary;

/// <summary>
/// An amount in a currency. Arithmetic keeps full decimal precision; call
/// <see cref="Round"/> explicitly where a value must be expressed in minor units
/// (e.g. line totals, tax amounts). Mixing currencies is a bug and throws.
/// </summary>
public sealed record Money : IComparable<Money>
{
    /// <summary>Midpoints round away from zero (commercial rounding): 0.125 to 0.13.</summary>
    public const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    public Money(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public bool IsPositive => Amount > 0m;

    /// <summary>True when the amount has no more decimals than the currency allows.</summary>
    public bool IsRounded => Amount == Math.Round(Amount, Currency.MinorUnits, Rounding);

    public static Money Km(decimal amount) => new(amount, Currency.BAM);

    public static Money Zero(Currency currency) => new(0m, currency);

    /// <summary>Sums amounts in <paramref name="currency"/>; an empty sequence gives zero.</summary>
    public static Money Sum(IEnumerable<Money> values, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(values);
        var total = Zero(currency);
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    public Money Round() => new(Math.Round(Amount, Currency.MinorUnits, Rounding), Currency);

    public Money Abs() => new(Math.Abs(Amount), Currency);

    public int CompareTo(Money? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator -(Money value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(-value.Amount, value.Currency);
    }

    public static Money operator *(Money left, decimal factor)
    {
        ArgumentNullException.ThrowIfNull(left);
        return new(left.Amount * factor, left.Currency);
    }

    public static Money operator *(decimal factor, Money right) => right * factor;

    public static Money operator /(Money left, decimal divisor)
    {
        ArgumentNullException.ThrowIfNull(left);
        return new(left.Amount / divisor, left.Currency);
    }

    public static bool operator <(Money left, Money right) => Compare(left, right) < 0;

    public static bool operator >(Money left, Money right) => Compare(left, right) > 0;

    public static bool operator <=(Money left, Money right) => Compare(left, right) <= 0;

    public static bool operator >=(Money left, Money right) => Compare(left, right) >= 0;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {Currency.Code}");

    private static int Compare(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine {left.Currency.Code} and {right.Currency.Code} amounts.");
        }
    }
}
