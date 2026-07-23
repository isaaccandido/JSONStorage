using System;

namespace Isaac.FileStorage.CustomExceptions;

public class InvalidKeyException(string? message) : Exception
{
    private const string DefaultMessage =
        "Key must not contain path traversal characters or resolve outside the storage directory.";

    public override string Message { get; } = message ?? DefaultMessage;

    public InvalidKeyException() : this(DefaultMessage)
    {
    }
}
