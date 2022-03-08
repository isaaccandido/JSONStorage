using System;

namespace Isaac.FileStorage
{
    public class EmptyKeyException : Exception
    {
        public override string Message { get; }
        public EmptyKeyException() : base() => Message = "Key cannot be empty.";
        public EmptyKeyException(string message) => this.Message = message;
    }
}
