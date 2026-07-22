using System;

namespace Isaac.FileStorage.CustomExceptions;

public class EmptyKeyException(string message) : Exception
{
    public override string Message { get; } = message;

    public EmptyKeyException() : this("Key cannot be empty.")
    {
    }
}