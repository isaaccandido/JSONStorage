using Isaac.FileStorage;
using System;

namespace Isaac.Storage.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            // Working example
            var str = new FileStorageEngine("test");

            var t = new Transaction()
            {
                Name = "CPFL",
                Description = "Conta do mês de dezembro",
                Completed = false,
                ActualValue = 133.45M,
                Deleted = false,
                InsertionDate = DateTime.Now,
                Interest = 3.1M,
                LastUpdate = DateTime.Now,
                DueDate = DateTime.Now,
                PaymentDate = DateTime.Now,
                OriginalValue = 130.00M,
                ID = 0,
                TransType = TransactionType.Expense,
                Category = new Category()
                {
                    Name = "Contas Fixas",
                    ID = 0,
                    SubCategory = new SubCategory()
                    {
                        Name = "Energia",
                        ID = 0,
                        ParentID = 0
                    },
                },
                ResponsibleUser = new SystemUser()
                {
                    Name = "John",
                    Description = "The best vendedor",
                    FullName = "John Cooper",
                    ID = 32
                },
                SourceWallet = new Wallet()
                {
                    Name = "NuBank",
                    CreationDate = DateTime.Now,
                    Description = "Roxinho",
                    CurrentBalance = 32198.65M,
                    LastModificationDate = DateTime.Now
                },
                Supplier = new Supplier()
                {
                    Name = "CPFL Paulista",
                    ID = 9
                },
            };

            str.Insert(t.Name, t);

            var cpfl = str.Get<Transaction>(t.Name);

            var ks = str.GetAllKeys();
            cpfl = cpfl;
<<<<<<< Updated upstream

            str.Delete(t.Name);

            str = str;
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
=======
>>>>>>> Stashed changes
        }
    }
}
