using System;

namespace Isaac.FileStorage
{
    public class KeyNotFoundException : Exception
    {
        public override string Message { get; }
        public KeyNotFoundException() : base() => Message = "Key was not found."; 
        public KeyNotFoundException(string message) => this.Message = message;
    }
}
