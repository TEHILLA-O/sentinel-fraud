namespace Sentinel.SharedKernel;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string name) where T : class =>
        value ?? throw new ArgumentNullException(name);

    public static string AgainstNullOrWhiteSpace(string? value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value;
    }

    public static T AgainstDefault<T>(T value, string name) where T : struct, IEquatable<T>
    {
        if (value.Equals(default))
        {
            throw new ArgumentException($"{name} must be provided.", name);
        }

        return value;
    }
}
