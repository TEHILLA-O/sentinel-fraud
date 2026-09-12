using Sentinel.Contracts;
using Sentinel.RiskEngine.Domain.Geography;

namespace Sentinel.RiskEngine.Domain.Rules;

public sealed class HighValueRule : IRiskRule
{
    public string Code => RuleCodes.HighValue;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var p = context.ParametersFor(Code);
        var threshold = p.GetDecimal("threshold", 3000m);
        var amount = context.Transaction.Amount.Amount;

        if (amount < threshold)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var intensity = amount >= threshold * 2m ? 1.5m : 1m;
        var score = (int)decimal.Round(p.Score * intensity, 0, MidpointRounding.AwayFromZero);
        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            score,
            $"Amount {context.Transaction.Amount} exceeds high-value threshold {threshold:N2} {context.Transaction.Amount.Currency}.",
            new Dictionary<string, string>
            {
                ["amount"] = context.Transaction.Amount.ToString(),
                ["threshold"] = threshold.ToString("N2")
            }));
    }
}

public sealed class HighVelocityRule : IRiskRule
{
    public string Code => RuleCodes.HighVelocity;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var p = context.ParametersFor(Code);
        var fiveMinuteLimit = p.GetInt("fiveMinuteCount", 5);
        var oneMinuteLimit = p.GetInt("oneMinuteLimit", p.GetInt("oneMinuteCount", 3));
        var tx5 = context.Velocity.TransactionsLast5Minutes;
        var tx1 = context.Velocity.TransactionsLast1Minute;

        if (tx5 < fiveMinuteLimit && tx1 < oneMinuteLimit)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var score = p.Score;
        if (tx5 >= fiveMinuteLimit * 2 || tx1 >= oneMinuteLimit * 2)
        {
            score = Math.Min(p.Score + 10, 50);
        }
        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            score,
            $"Velocity is elevated: {tx1} tx in 1 minute, {tx5} tx in 5 minutes.",
            new Dictionary<string, string>
            {
                ["tx1m"] = tx1.ToString(),
                ["tx5m"] = tx5.ToString(),
                ["spend5m"] = context.Velocity.SpendLast5Minutes.ToString()
            }));
    }
}

public sealed class ForeignCountryRule : IRiskRule
{
    public string Code => RuleCodes.ForeignCountry;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var home = context.Profile.HomeCountry;
        var current = context.Transaction.Country;
        var common = context.Profile.CommonCountries;

        if (string.Equals(home, current, StringComparison.OrdinalIgnoreCase) ||
            common.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var p = context.ParametersFor(Code);
        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            p.Score,
            $"Transaction country {current} is outside home country {home} and common countries.",
            new Dictionary<string, string>
            {
                ["homeCountry"] = home,
                ["transactionCountry"] = current,
                ["commonCountries"] = string.Join(',', common)
            }));
    }
}

public sealed class UnknownDeviceRule : IRiskRule
{
    public string Code => RuleCodes.UnknownDevice;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var deviceId = context.Transaction.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            var p = context.ParametersFor(Code);
            return ValueTask.FromResult(RiskRuleResult.Hit(
                Code,
                p.Score,
                "No device identifier was supplied.",
                new Dictionary<string, string> { ["deviceId"] = "missing" }));
        }

        var known = context.KnownDevices.Any(d =>
            string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        var common = context.Profile.CommonDevices.Contains(deviceId, StringComparer.OrdinalIgnoreCase);

        if (known || common)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        return ValueTask.FromResult(RiskRuleResult.Hit(
            context.ParametersFor(Code).RuleCode,
            context.ParametersFor(Code).Score,
            $"Device {deviceId} has not been seen for this customer.",
            new Dictionary<string, string>
            {
                ["deviceId"] = deviceId,
                ["knownDeviceCount"] = context.KnownDevices.Count.ToString()
            }));
    }
}

public sealed class CardNotPresentRule : IRiskRule
{
    public string Code => RuleCodes.CardNotPresent;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var tx = context.Transaction;
        if (tx.CardPresent || tx.Channel is Channel.TRANSFER)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var remote = tx.Channel is Channel.WEB or Channel.MOBILE;
        if (!remote && tx.CardPresent)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        if (tx.CardPresent)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var p = context.ParametersFor(Code);
        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            p.Score,
            $"Card-not-present {tx.Channel} transaction.",
            new Dictionary<string, string>
            {
                ["channel"] = tx.Channel.ToString(),
                ["cardPresent"] = "false"
            }));
    }
}

public sealed class ImpossibleTravelRule : IRiskRule
{
    public string Code => RuleCodes.ImpossibleTravel;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var current = context.Transaction;
        if (!current.Channel.RequiresPhysicalPresence())
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var previous = context.RecentTransactions
            .Where(t => t.Channel.RequiresPhysicalPresence())
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefault();

        if (previous is null)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var from = Geo.Resolve(previous.Country, previous.City);
        var to = Geo.Resolve(current.Country, current.City);
        if (from is null || to is null)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var hours = Math.Abs((current.Timestamp - previous.Timestamp).TotalHours);
        if (hours <= 0)
        {
            hours = 1d / 60d;
        }

        var distance = Geo.DistanceKm(from.Value, to.Value);
        var impliedSpeed = distance / hours;
        var p = context.ParametersFor(Code);
        var maxSpeed = p.GetDouble("maxSpeedKmh", 900);

        if (impliedSpeed <= maxSpeed)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            p.Score,
            $"Physical presence at {from.Value.Label} then {to.Value.Label} implies {impliedSpeed:N0} km/h.",
            new Dictionary<string, string>
            {
                ["from"] = $"{previous.Country}/{previous.City}",
                ["to"] = $"{current.Country}/{current.City}",
                ["distanceKm"] = distance.ToString("N1"),
                ["hours"] = hours.ToString("N2"),
                ["impliedSpeedKmh"] = impliedSpeed.ToString("N0")
            }));
    }
}

public sealed class MerchantRiskRule : IRiskRule
{
    public string Code => RuleCodes.MerchantRisk;

    private static readonly HashSet<MerchantCategory> HighRisk =
    [
        MerchantCategory.Gambling,
        MerchantCategory.Crypto,
        MerchantCategory.MoneyTransfer,
        MerchantCategory.Adult
    ];

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var category = context.Transaction.MerchantCategory;
        if (!HighRisk.Contains(category))
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var unusual = !context.Profile.CommonCategories.Contains(category);
        var p = context.ParametersFor(Code);
        var score = unusual ? p.Score : Math.Max(5, p.Score / 2);
        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            score,
            $"Merchant category {category} is treated as elevated risk.",
            new Dictionary<string, string>
            {
                ["category"] = category.ToString(),
                ["unusualForCustomer"] = unusual.ToString()
            }));
    }
}

public sealed class UnusualTimeRule : IRiskRule
{
    public string Code => RuleCodes.UnusualTime;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var hour = context.Transaction.Timestamp.UtcDateTime.Hour;
        var typical = context.Profile.TypicalHoursUtc;
        if (typical.Count == 0)
        {
            if (hour is >= 1 and <= 4)
            {
                return ValueTask.FromResult(RiskRuleResult.Hit(
                    Code,
                    context.ParametersFor(Code).Score,
                    $"Transaction occurred at {hour:00}:00 UTC with no established pattern.",
                    new Dictionary<string, string> { ["hourUtc"] = hour.ToString() }));
            }

            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        if (typical.Contains(hour) || typical.Contains((hour + 23) % 24) || typical.Contains((hour + 1) % 24))
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            context.ParametersFor(Code).Score,
            $"Hour {hour:00}:00 UTC is outside the customer's typical hours.",
            new Dictionary<string, string>
            {
                ["hourUtc"] = hour.ToString(),
                ["typicalHours"] = string.Join(',', typical)
            }));
    }
}

public sealed class NewAccountRule : IRiskRule
{
    public string Code => RuleCodes.NewAccount;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var days = context.ParametersFor(Code).GetInt("days", 14);
        var age = context.EvaluatedAt - context.Profile.AccountOpenedAt;
        if (age.TotalDays >= days)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            context.ParametersFor(Code).Score,
            $"Account is {age.TotalDays:N1} days old (threshold {days} days).",
            new Dictionary<string, string>
            {
                ["accountAgeDays"] = age.TotalDays.ToString("N1"),
                ["thresholdDays"] = days.ToString()
            }));
    }
}

public sealed class BehaviourDeviationRule : IRiskRule
{
    public string Code => RuleCodes.BehaviourDeviation;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var baseline = context.Profile.MedianTransactionAmount.Amount > 0
            ? context.Profile.MedianTransactionAmount.Amount
            : context.Profile.AverageTransactionAmount.Amount;

        if (baseline <= 0 || context.Profile.LifetimeTransactionCount < 5)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var multiplier = context.ParametersFor(Code).GetDecimal("multiplier", 8m);
        var amount = context.Transaction.Amount.Amount;
        if (amount < baseline * multiplier)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var ratio = Math.Round(amount / baseline, 1);
        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            context.ParametersFor(Code).Score,
            $"Amount is {ratio}x the customer's typical spend of {baseline:N2}.",
            new Dictionary<string, string>
            {
                ["amount"] = amount.ToString("N2"),
                ["baseline"] = baseline.ToString("N2"),
                ["ratio"] = ratio.ToString("N1")
            }));
    }
}

public sealed class RapidCountryChangeRule : IRiskRule
{
    public string Code => RuleCodes.RapidCountryChange;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var limit = context.ParametersFor(Code).GetInt("distinctCountries", 2);
        var distinct = context.Velocity.DistinctCountriesLast30Minutes;
        var includesCurrent = distinct;
        if (includesCurrent < limit)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            context.ParametersFor(Code).Score,
            $"{distinct} distinct countries observed in 30 minutes.",
            new Dictionary<string, string>
            {
                ["distinctCountries30m"] = distinct.ToString(),
                ["currentCountry"] = context.Transaction.Country
            }));
    }
}

public sealed class RepeatedDeclineRule : IRiskRule
{
    public string Code => RuleCodes.RepeatedDecline;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var limit = context.ParametersFor(Code).GetInt("failures", 3);
        var failures = context.Velocity.FailedTransactionsLast15Minutes;
        if (failures < limit)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            context.ParametersFor(Code).Score,
            $"{failures} failed transactions in the last 15 minutes.",
            new Dictionary<string, string> { ["failures15m"] = failures.ToString() }));
    }
}

public sealed class RoundAmountRule : IRiskRule
{
    public string Code => RuleCodes.RoundAmount;

    public ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var minimum = context.ParametersFor(Code).GetDecimal("minimumAmount", 1000m);
        var amount = context.Transaction.Amount.Amount;
        if (amount < minimum)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        var isRound = amount == decimal.Truncate(amount) && amount % 100m == 0;
        if (!isRound)
        {
            return ValueTask.FromResult(RiskRuleResult.NotTriggered(Code));
        }

        return ValueTask.FromResult(RiskRuleResult.Hit(
            Code,
            context.ParametersFor(Code).Score,
            $"High-value round amount {context.Transaction.Amount} can indicate testing or cash-out.",
            new Dictionary<string, string> { ["amount"] = context.Transaction.Amount.ToString() }));
    }
}
