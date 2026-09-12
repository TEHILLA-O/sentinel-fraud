namespace Sentinel.SharedKernel;

/// <summary>
/// Monetary value using <see cref="decimal"/>. Floating-point types are forbidden for money.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Money amount cannot be negative.");
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money Gbp(decimal amount) => new(amount, "GBP");

    public static Money Usd(decimal amount) => new(amount, "USD");

    public long ToMinorUnits() => (long)decimal.Round(Amount * 100m, 0, MidpointRounding.AwayFromZero);

    public static Money FromMinorUnits(long minorUnits, string currency) =>
        new(minorUnits / 100m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static Money operator +(Money left, Money right) => left.Add(right);

    public override string ToString() => $"{Amount:N2} {Currency}";

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot combine {Currency} with {other.Currency}.");
        }
    }
}
