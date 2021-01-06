using Isaac.FileStorage;
using System;
using System.Linq;

namespace Isaac.Storage.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            ExemploDoRafael();

            //var storage = new Core("C:\\sblevers\\");

            ////test t = new()
            ////{
            ////    CPF = "0",
            ////    Nome = "Isaac"
            ////};

            ////storage.Insert("321", t);

            ////var specifickey = storage.Get<test>("321");

            ////var keys = storage.GetAllKeys();


            //// Insert known data
            //var jfk = new test() { Name = "John F. Kennedy" };
            //storage.Insert<test>("us.ny.jk", jfk);
            //// retrieve all data
            //var all = storage.GetAllKeys().Select(k => storage.Get<test>(k));
        }

        static void ExemploDoRafael()
        {
            var files = new Core("C:\\sblevers\\");


            // Insert known data
            var jfk = new Airports() { Name = "John F. Kennedy" };
            files.Insert<Airports>("us.ny.jk", jfk);

            var jfk2 = files.Get<Airports>("us.ny.jk");

            var allKeys = files.GetAllKeys().ToArray();


            // retrieve all data
            var all = files.GetAllKeys().Select(k => files.Get<Airports>(k)).ToArray();
        }
    }

    public class Airports
    {
        public string Name { get; set; }
    }

    public class test
    {
        public string Name { get; set; }
        public string CPF { get; set; }
    }
}
