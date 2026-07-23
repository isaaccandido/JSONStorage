using System;

namespace Isaac.FileStorage.CustomExceptions;

public class StorageKeyNotFoundException(string? message) : Exception
{
    private const string DefaultMessage = "Key was not found.";

    public override string Message { get; } = message ?? DefaultMessage;

    public StorageKeyNotFoundException() : this(DefaultMessage)
    {
    }
}
