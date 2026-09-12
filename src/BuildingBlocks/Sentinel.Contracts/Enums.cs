namespace Sentinel.Contracts;

public enum Channel
{
    POS = 1,
    ATM = 2,
    WEB = 3,
    MOBILE = 4,
    TRANSFER = 5
}

public enum RiskDecision
{
    Approve = 1,
    Review = 2,
    Block = 3
}

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum CaseStatus
{
    Open = 1,
    Assigned = 2,
    Investigating = 3,
    Escalated = 4,
    ConfirmedFraud = 5,
    FalsePositive = 6,
    Closed = 7
}

public enum MerchantCategory
{
    Grocery = 1,
    Fuel = 2,
    Restaurant = 3,
    Travel = 4,
    Electronics = 5,
    Clothing = 6,
    Healthcare = 7,
    Utilities = 8,
    Gambling = 9,
    Crypto = 10,
    MoneyTransfer = 11,
    Adult = 12,
    Other = 99
}

public enum FeedbackType
{
    FraudConfirmed = 1,
    FalsePositiveConfirmed = 2,
    CaseClosed = 3
}

public static class ChannelExtensions
{
    public static bool RequiresPhysicalPresence(this Channel channel) =>
        channel is Channel.POS or Channel.ATM;
}
