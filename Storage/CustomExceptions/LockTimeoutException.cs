using System;

namespace Isaac.FileStorage.CustomExceptions;

public class LockTimeoutException(string? message) : Exception
{
    private const string DefaultMessage =
        "Timed out waiting to acquire an exclusive lock for this key. Another thread or process may be holding it.";

    public override string Message { get; } = message ?? DefaultMessage;

    public LockTimeoutException() : this(DefaultMessage)
    {
    }
}
