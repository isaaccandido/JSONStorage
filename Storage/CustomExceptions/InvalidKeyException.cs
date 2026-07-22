using System;

namespace Isaac.FileStorage.CustomExceptions;

public class InvalidKeyException(string message) : Exception
{
    public override string Message { get; } = message;

    public InvalidKeyException() : this(
        "Key must not contain path traversal characters or resolve outside the storage directory."
    )
    {
    }
}