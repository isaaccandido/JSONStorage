using Isaac.FileStorage;
using System;

namespace Isaac.Storage.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            var storage = new Core("C:\\sblevers\\");

            test t = new()
            {
                CPF = "0",
                Nome = "Isaac"
            };

            storage.Insert("321", t);

            var specifickey = storage.Get<test>("321");

            var keys = storage.GetAllKeys();
        }
    }

    public class test
    {
        public string Nome { get; set; }
        public string CPF { get; set; }
    }
}
