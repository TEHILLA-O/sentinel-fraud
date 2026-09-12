using Microsoft.AspNetCore.SignalR;

namespace Sentinel.Web.Realtime;

public sealed class LiveDecisionHub : Hub
{
    public const string Decisions = "decisions";
    public const string Alerts = "alerts";
}
