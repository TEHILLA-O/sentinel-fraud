namespace Sentinel.SharedKernel;

public static class SentinelRoles
{
    public const string FraudAnalyst = "FraudAnalyst";
    public const string SeniorAnalyst = "SeniorAnalyst";
    public const string RiskManager = "RiskManager";
    public const string Administrator = "Administrator";
    public const string Auditor = "Auditor";

    public static readonly IReadOnlyList<string> All =
    [
        FraudAnalyst,
        SeniorAnalyst,
        RiskManager,
        Administrator,
        Auditor
    ];
}

public static class SentinelPolicies
{
    public const string ReviewCases = "ReviewCases";
    public const string EscalateCases = "EscalateCases";
    public const string ModifyRiskConfiguration = "ModifyRiskConfiguration";
    public const string AdministerSystem = "AdministerSystem";
    public const string ReadOnlyAudit = "ReadOnlyAudit";
    public const string ViewCases = "ViewCases";
}

public static class CorrelationHeaders
{
    public const string CorrelationId = "X-Correlation-Id";
}
