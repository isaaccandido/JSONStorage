using System;
using Xunit;

namespace FileStorage.UnitTest;

public class TransactionRoundTripTests
{
    [Fact]
    public void Insert_ComplexNestedObjectGraph_RoundTripsCorrectly()
    {
        using var block = new TestBlock();

        var insertionDate = new DateTime(2024, 12, 1, 10, 0, 0, DateTimeKind.Utc);

        var transaction = new Transaction
        {
            Name = "CPFL",
            Description = "Conta do mês de dezembro",
            Completed = false,
            Deleted = false,
            ActualValue = 133.45M,
            OriginalValue = 130.00M,
            Interest = 3.1M,
            InsertionDate = insertionDate,
            DueDate = insertionDate,
            PaymentDate = insertionDate,
            LastUpdate = insertionDate,
            Id = 0,
            TransType = TransactionType.Expense,
            Category = new Category
            {
                Name = "Contas Fixas",
                Id = 0,
                SubCategory = new SubCategory
                {
                    Name = "Energia",
                    Id = 0,
                    ParentId = 0
                }
            },
            ResponsibleUser = new SystemUser
            {
                Name = "John",
                Description = "The best vendedor",
                FullName = "John Cooper",
                Id = 32
            },
            SourceWallet = new Wallet
            {
                Name = "NuBank",
                CreationDate = insertionDate,
                Description = "Roxinho",
                CurrentBalance = 32198.65M,
                LastModificationDate = insertionDate
            },
            Supplier = new Supplier
            {
                Name = "CPFL Paulista",
                Id = 9
            }
        };

        block.Db.Insert(transaction.Name, transaction);

        var retrieved = block.Db.Get<Transaction>(transaction.Name);

        Assert.Equal(transaction.Name, retrieved.Name);
        Assert.Equal(transaction.Description, retrieved.Description);
        Assert.Equal(transaction.ActualValue, retrieved.ActualValue);
        Assert.Equal(transaction.OriginalValue, retrieved.OriginalValue);
        Assert.Equal(transaction.Interest, retrieved.Interest);
        Assert.Equal(transaction.TransType, retrieved.TransType);
        // BSON has no DateTimeKind concept, so Newtonsoft round-trips a UTC DateTime as local
        // time; compare the actual instant rather than the raw Kind/offset representation.
        Assert.Equal(transaction.InsertionDate.ToUniversalTime(), retrieved.InsertionDate.ToUniversalTime());

        Assert.Equal("Contas Fixas", retrieved.Category.Name);
        Assert.Equal("Energia", retrieved.Category.SubCategory.Name);

        Assert.Equal("John Cooper", retrieved.ResponsibleUser.FullName);

        Assert.Equal("NuBank", retrieved.SourceWallet.Name);
        Assert.Equal(32198.65M, retrieved.SourceWallet.CurrentBalance);

        Assert.Equal("CPFL Paulista", retrieved.Supplier.Name);
        Assert.Equal(9, retrieved.Supplier.Id);
    }

    [Fact]
    public void Get_ComplexObjectAsWrongType_ThrowsInvalidOperationException()
    {
        using var block = new TestBlock();

        block.Db.Insert("mismatched", new Transaction { Name = "CPFL" });

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Get<string>("mismatched"));

        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void GetAllKeys_AfterInsertingComplexObject_IncludesItsKey()
    {
        using var block = new TestBlock();

        block.Db.Insert("CPFL", new Transaction { Name = "CPFL" });

        Assert.Contains("CPFL", block.Db.GetAllKeys());
    }
}
