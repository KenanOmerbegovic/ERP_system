using System.Globalization;
using Erp.SharedKernel.Monetary;

namespace Erp.SharedKernel.Tests.Monetary;

public class MoneyTests
{
    [Theory]
    [InlineData("0.125", "0.13")]
    [InlineData("0.135", "0.14")]
    [InlineData("0.124", "0.12")]
    [InlineData("0.126", "0.13")]
    [InlineData("-0.125", "-0.13")]
    [InlineData("-0.135", "-0.14")]
    [InlineData("2.5", "2.50")]
    [InlineData("10", "10.00")]
    [InlineData("1.005", "1.01")]
    [InlineData("1.004999", "1.00")]
    public void Round_rounds_midpoints_away_from_zero_to_two_decimals(string amount, string expected)
    {
        var rounded = Money.Km(decimal.Parse(amount, CultureInfo.InvariantCulture)).Round();

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), rounded.Amount);
        Assert.Equal(Currency.BAM, rounded.Currency);
    }

    [Fact]
    public void Arithmetic_keeps_full_precision_until_rounded()
    {
        // 3 x 0.333 KM with 17 % VAT: rounding only at the end.
        var net = Money.Km(0.333m) * 3m;
        var gross = net * 1.17m;

        Assert.Equal(0.999m, net.Amount);
        Assert.Equal(1.16883m, gross.Amount);
        Assert.False(gross.IsRounded);
        Assert.Equal(Money.Km(1.17m), gross.Round());
    }

    [Fact]
    public void Rounding_each_line_differs_from_rounding_the_total()
    {
        var lines = new[] { Money.Km(0.005m), Money.Km(0.005m), Money.Km(0.005m) };

        var roundedPerLine = Money.Sum(lines.Select(l => l.Round()), Currency.BAM);
        var roundedTotal = Money.Sum(lines, Currency.BAM).Round();

        Assert.Equal(Money.Km(0.03m), roundedPerLine);
        Assert.Equal(Money.Km(0.02m), roundedTotal);
    }

    [Fact]
    public void Addition_and_subtraction_in_same_currency()
    {
        var a = Money.Km(10.50m);
        var b = Money.Km(2.25m);

        Assert.Equal(Money.Km(12.75m), a + b);
        Assert.Equal(Money.Km(8.25m), a - b);
        Assert.Equal(Money.Km(-10.50m), -a);
    }

    [Fact]
    public void Multiplication_and_division_by_decimal()
    {
        var price = Money.Km(10m);

        Assert.Equal(Money.Km(25m), price * 2.5m);
        Assert.Equal(Money.Km(25m), 2.5m * price);
        Assert.Equal(Money.Km(2.5m), price / 4m);
    }

    [Fact]
    public void Division_by_zero_throws()
    {
        Assert.Throws<DivideByZeroException>(() => Money.Km(1m) / 0m);
    }

    [Fact]
    public void Mixing_currencies_throws()
    {
        var km = Money.Km(1m);
        var eur = new Money(1m, Currency.EUR);

        Assert.Throws<InvalidOperationException>(() => km + eur);
        Assert.Throws<InvalidOperationException>(() => km - eur);
        Assert.Throws<InvalidOperationException>(() => km < eur);
        Assert.Throws<InvalidOperationException>(() => Money.Sum([km, eur], Currency.BAM));
    }

    [Fact]
    public void Equality_ignores_trailing_zeros_but_not_currency()
    {
        Assert.Equal(Money.Km(1.0m), Money.Km(1.00m));
        Assert.Equal(Money.Km(1.0m).GetHashCode(), Money.Km(1.00m).GetHashCode());
        Assert.NotEqual(Money.Km(1m), new Money(1m, Currency.EUR));
    }

    [Fact]
    public void Comparison_operators()
    {
        var small = Money.Km(1m);
        var large = Money.Km(2m);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= Money.Km(1.00m));
        Assert.True(large >= small);
        Assert.Equal(-1, small.CompareTo(large));
    }

    [Fact]
    public void Sign_helpers_and_abs()
    {
        Assert.True(Money.Zero(Currency.BAM).IsZero);
        Assert.True(Money.Km(-0.01m).IsNegative);
        Assert.True(Money.Km(0.01m).IsPositive);
        Assert.Equal(Money.Km(5m), Money.Km(-5m).Abs());
    }

    [Fact]
    public void Sum_of_empty_sequence_is_zero_in_given_currency()
    {
        var total = Money.Sum([], Currency.EUR);

        Assert.Equal(Money.Zero(Currency.EUR), total);
    }

    [Fact]
    public void IsRounded_respects_currency_minor_units()
    {
        Assert.True(Money.Km(1.20m).IsRounded);
        Assert.True(Money.Km(1.2000m).IsRounded);
        Assert.False(Money.Km(1.201m).IsRounded);
    }

    [Fact]
    public void ToString_is_culture_invariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("bs-Latn-BA");

            Assert.Equal("1234.56 BAM", Money.Km(1234.56m).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Null_currency_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Money(1m, null!));
    }
}
