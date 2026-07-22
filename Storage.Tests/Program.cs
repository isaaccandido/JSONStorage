using System;
using System.Linq;
using Isaac.FileStorage.Lib;

namespace FileStorage.Tests;

internal static class Program
{
    private static void Main(string[] args)
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
            Id = 0,
            TransType = TransactionType.Expense,
            Category = new Category()
            {
                Name = "Contas Fixas",
                Id = 0,
                SubCategory = new SubCategory()
                {
                    Name = "Energia",
                    Id = 0,
                    ParentId = 0
                },
            },
            ResponsibleUser = new SystemUser()
            {
                Name = "John",
                Description = "The best vendedor",
                FullName = "John Cooper",
                Id = 32
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
                Id = 9
            },
        };

        str.Insert(t.Name, t);

        var wrongConversion = str.Get<string>(t.Name);

        var cpfl = str.Get<Transaction>(t.Name);

        var ks = str.GetAllKeys().First();

        var data = str.Get<Transaction>(ks);
    }
}