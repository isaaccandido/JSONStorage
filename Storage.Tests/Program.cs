using Isaac.FileStorage;
using System;

namespace Storage.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            var w = new Core("C:\\sblevers\\");

            test t = new()
            {
                CPF = "0",
                Nome = "Joaquim"
            };

            w.Insert("321", t);

            var specifickey = w.Get<test>("321");

            var keys = w.GetAllKeys().

            keys = keys;
        }
    }

    public class test
    {
        public string Nome { get; set; }
        public string CPF { get; set; }
    }
}
