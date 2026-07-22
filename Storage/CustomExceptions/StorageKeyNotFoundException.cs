using System;

namespace Isaac.FileStorage.Lib.CustomExceptions;

public class StorageKeyNotFoundException(string message) : Exception
{
    public override string Message { get; } = message;

    public StorageKeyNotFoundException() : this("Key was not found.")
    {
    }
}
