using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.Contracts.Json;

var mode = GetArg("--mode") ?? "mixed";
var rate = int.TryParse(GetArg("--rate"), out var parsedRate) ? parsedRate : 5;
var count = int.TryParse(GetArg("--count"), out var parsedCount) ? parsedCount : 0;
var endpoint = GetArg("--endpoint") ?? "http://localhost:8081/api/v1/transactions";

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
var delay = TimeSpan.FromMilliseconds(Math.Max(10, 1000d / Math.Max(1, rate)));
var produced = 0;
Console.WriteLine($"Sentinel generator mode={mode} rate={rate}/s endpoint={endpoint}");

while (count == 0 || produced < count)
{
    var tx = ScenarioFactory.Create(mode, produced);
    var response = await http.PostAsync(endpoint, new StringContent(SentinelJson.Serialize(tx), System.Text.Encoding.UTF8, "application/json"));
    Console.WriteLine($"{tx.TransactionId} {tx.Amount:N2} {tx.Currency} {tx.Country} {tx.Channel} -> {(int)response.StatusCode}");
    produced++;
    await Task.Delay(delay);
}

static string? GetArg(string name)
{
    var args = Environment.GetCommandLineArgs();
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

internal static class ScenarioFactory
{
    private static readonly Random Random = Random.Shared;

    public static TransactionReceivedV1 Create(string mode, int index)
    {
        return mode.ToLowerInvariant() switch
        {
            "normal" => Grocery(index),
            "suspicious" => Suspicious(index),
            "fraud-burst" => Burst(index),
            _ => (index % 11) switch
            {
                0 => HighValue(index),
                1 => Foreign(index),
                2 => UnknownDevice(index),
                3 => ImpossibleTravel(index),
                4 => Burst(index),
                5 => Online(index),
                _ => Grocery(index)
            }
        };
    }

    private static TransactionReceivedV1 Grocery(int index) => Base(
        index,
        amount: 12m + Random.Next(3, 90),
        country: "GB",
        city: "London",
        channel: Channel.POS,
        cardPresent: true,
        device: "D-UK-4411",
        merchant: "M-TESCO",
        category: MerchantCategory.Grocery);

    private static TransactionReceivedV1 Online(int index) => Base(
        index,
        amount: 35m + Random.Next(10, 180),
        country: "GB",
        city: "London",
        channel: Channel.WEB,
        cardPresent: false,
        device: "D-UK-4411",
        merchant: "M-AMAZON",
        category: MerchantCategory.Electronics);

    private static TransactionReceivedV1 HighValue(int index) => Base(
        index,
        amount: 4850m,
        country: "US",
        city: "New York",
        channel: Channel.WEB,
        cardPresent: false,
        device: "D-UNKNOWN",
        merchant: "M-LUX",
        category: MerchantCategory.Electronics,
        account: "A-91828");

    private static TransactionReceivedV1 Foreign(int index) => Base(
        index,
        amount: 420m,
        country: "DE",
        city: "Berlin",
        channel: Channel.POS,
        cardPresent: true,
        device: "D-UK-4411",
        merchant: "M-KADEWE",
        category: MerchantCategory.Clothing);

    private static TransactionReceivedV1 UnknownDevice(int index) => Base(
        index,
        amount: 260m,
        country: "GB",
        city: "London",
        channel: Channel.MOBILE,
        cardPresent: false,
        device: $"D-NEW-{index}",
        merchant: "M-PRET",
        category: MerchantCategory.Restaurant);

    private static TransactionReceivedV1 ImpossibleTravel(int index) => Base(
        index,
        amount: 95m,
        country: "US",
        city: "New York",
        channel: Channel.ATM,
        cardPresent: true,
        device: "D-UK-4411",
        merchant: "M-ATM-NY",
        category: MerchantCategory.Other);

    private static TransactionReceivedV1 Burst(int index) => Base(
        index,
        amount: 75m + index,
        country: "US",
        city: "Chicago",
        channel: Channel.WEB,
        cardPresent: false,
        device: "D-BOT",
        merchant: $"M-BURST-{index % 4}",
        category: MerchantCategory.MoneyTransfer,
        account: "A-91828");

    private static TransactionReceivedV1 Suspicious(int index) => HighValue(index);

    private static TransactionReceivedV1 Base(
        int index,
        decimal amount,
        string country,
        string city,
        Channel channel,
        bool cardPresent,
        string device,
        string merchant,
        MerchantCategory category,
        string account = "A-91828")
    {
        var now = DateTimeOffset.UtcNow;
        return new TransactionReceivedV1
        {
            EventId = Guid.CreateVersion7(),
            CorrelationId = Guid.CreateVersion7().ToString("N"),
            OccurredAt = now,
            TransactionId = $"TX-{now:HHmmss}-{index:0000}",
            AccountId = account,
            CustomerId = account == "A-91828" ? "C-4411" : "C-2201",
            Amount = amount,
            Currency = "GBP",
            MerchantId = merchant,
            MerchantCategory = category,
            Country = country,
            City = city,
            Timestamp = now,
            CardPresent = cardPresent,
            Channel = channel,
            DeviceId = device,
            IpAddress = "203.0.113.10"
        };
    }
}
