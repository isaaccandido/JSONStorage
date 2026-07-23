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
        var dueDate = new DateTime(2024, 12, 15, 10, 0, 0, DateTimeKind.Utc);
        var paymentDate = new DateTime(2024, 12, 10, 9, 0, 0, DateTimeKind.Utc);
        var lastUpdate = new DateTime(2024, 12, 20, 8, 0, 0, DateTimeKind.Utc);
        var walletCreationDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var walletLastModificationDate = new DateTime(2024, 12, 18, 14, 30, 0, DateTimeKind.Utc);

        var transaction = new Transaction
        {
            Name = "CPFL",
            Description = "Conta do mês de dezembro",
            Completed = true,
            Deleted = false,
            ActualValue = 133.45M,
            OriginalValue = 130.00M,
            Interest = 3.1M,
            InsertionDate = insertionDate,
            DueDate = dueDate,
            PaymentDate = paymentDate,
            LastUpdate = lastUpdate,
            Id = 501,
            TransType = TransactionType.Expense,
            Category = new Category
            {
                Name = "Contas Fixas",
                Id = 12,
                SubCategory = new SubCategory
                {
                    Name = "Energia",
                    Id = 34,
                    ParentId = 12
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
                CreationDate = walletCreationDate,
                Description = "Roxinho",
                CurrentBalance = 32198.65M,
                LastModificationDate = walletLastModificationDate
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
        Assert.Equal(transaction.Id, retrieved.Id);
        Assert.Equal(transaction.Completed, retrieved.Completed);
        Assert.Equal(transaction.Deleted, retrieved.Deleted);
        Assert.Equal(transaction.ActualValue, retrieved.ActualValue);
        Assert.Equal(transaction.OriginalValue, retrieved.OriginalValue);
        Assert.Equal(transaction.Interest, retrieved.Interest);
        Assert.Equal(transaction.TransType, retrieved.TransType);
        // BSON has no DateTimeKind concept, so Newtonsoft round-trips a UTC DateTime as local
        // time; compare the actual instant rather than the raw Kind/offset representation.
        Assert.Equal(transaction.InsertionDate.ToUniversalTime(), retrieved.InsertionDate.ToUniversalTime());
        Assert.Equal(transaction.DueDate.ToUniversalTime(), retrieved.DueDate.ToUniversalTime());
        Assert.Equal(transaction.PaymentDate.ToUniversalTime(), retrieved.PaymentDate.ToUniversalTime());
        Assert.Equal(transaction.LastUpdate.ToUniversalTime(), retrieved.LastUpdate.ToUniversalTime());

        Assert.Equal("Contas Fixas", retrieved.Category.Name);
        Assert.Equal(12, retrieved.Category.Id);
        Assert.Equal("Energia", retrieved.Category.SubCategory.Name);
        Assert.Equal(34, retrieved.Category.SubCategory.Id);
        Assert.Equal(12, retrieved.Category.SubCategory.ParentId);

        Assert.Equal("John", retrieved.ResponsibleUser.Name);
        Assert.Equal("The best vendedor", retrieved.ResponsibleUser.Description);
        Assert.Equal("John Cooper", retrieved.ResponsibleUser.FullName);
        Assert.Equal(32, retrieved.ResponsibleUser.Id);

        Assert.Equal("NuBank", retrieved.SourceWallet.Name);
        Assert.Equal("Roxinho", retrieved.SourceWallet.Description);
        Assert.Equal(walletCreationDate.ToUniversalTime(), retrieved.SourceWallet.CreationDate.ToUniversalTime());
        Assert.Equal(walletLastModificationDate.ToUniversalTime(), retrieved.SourceWallet.LastModificationDate.ToUniversalTime());
        Assert.Equal(32198.65M, retrieved.SourceWallet.CurrentBalance);

        Assert.Equal("CPFL Paulista", retrieved.Supplier.Name);
        Assert.Equal(9, retrieved.Supplier.Id);
    }

    [Fact]
    public void Insert_TransactionWithIncomeType_RoundTripsCorrectly()
    {
        using var block = new TestBlock();

        var transaction = new Transaction { Name = "Salary", TransType = TransactionType.Income };

        block.Db.Insert(transaction.Name, transaction);

        var retrieved = block.Db.Get<Transaction>(transaction.Name);

        Assert.Equal(TransactionType.Income, retrieved.TransType);
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
