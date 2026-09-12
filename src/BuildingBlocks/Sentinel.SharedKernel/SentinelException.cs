namespace Sentinel.SharedKernel;

public class SentinelException : Exception
{
    public SentinelException(string message) : base(message)
    {
    }

    public SentinelException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class PoisonMessageException : SentinelException
{
    public PoisonMessageException(string message) : base(message)
    {
    }

    public PoisonMessageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class TransientInfrastructureException : SentinelException
{
    public TransientInfrastructureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
