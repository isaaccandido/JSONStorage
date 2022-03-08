using Isaac.FileStorage;
using System;
using System.Linq;

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

            var ks = str.GetAllKeys().First();

            var data = str.Get<Transaction>(ks);
        }
    }
}
