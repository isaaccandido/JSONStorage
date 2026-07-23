using System;

namespace Isaac.FileStorage.CustomExceptions;

public class EmptyKeyException(string? message) : Exception
{
    private const string DefaultMessage = "Key cannot be empty.";

    public override string Message { get; } = message ?? DefaultMessage;

    public EmptyKeyException() : this(DefaultMessage)
    {
    }
}
