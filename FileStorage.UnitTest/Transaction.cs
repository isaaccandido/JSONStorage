using System;

namespace FileStorage.UnitTest;

public enum TransactionType
{
    Income,
    Expense
}

public class Transaction
{
    public string Name { get; init; }
    public string Description { get; init; }
    public int Id { get; init; }
    public TransactionType TransType { get; init; }
    public SystemUser ResponsibleUser { get; init; }
    public decimal OriginalValue { get; init; }
    public decimal ActualValue { get; init; }
    public decimal Interest { get; init; }
    public DateTime InsertionDate { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime PaymentDate { get; init; }
    public DateTime LastUpdate { get; init; }
    public bool Completed { get; init; }
    public bool Deleted { get; init; }
    public Wallet SourceWallet { get; init; }
    public Supplier Supplier { get; init; }
    public Category Category { get; init; }

    public override string ToString()
    {
        return $"Name: {Name} - Value: {OriginalValue}";
    }
}

public class Category
{
    public string Name { get; init; }
    public int Id { get; init; }
    public SubCategory SubCategory { get; init; }

    public override string ToString()
    {
        return $"Name: {Name} - ID: {Id}";
    }
}

public class SubCategory
{
    public string Name { get; init; }
    public int ParentId { get; init; }
    public int Id { get; init; }

    public override string ToString()
    {
        return $"Name: {Name} - ParentID: {ParentId} - ID: {Id}";
    }
}

public class Supplier
{
    public string Name { get; init; }
    public int Id { get; init; }

    public override string ToString()
    {
        return $"Name: {Name} - ID: {Id}";
    }
}

public class Wallet
{
    public string Name { get; init; }
    public string Description { get; init; }
    public DateTime CreationDate { get; init; }
    public DateTime LastModificationDate { get; init; }
    public decimal CurrentBalance { get; init; }

    public override string ToString()
    {
        return $"Wallet: {Name} - Balance: {CurrentBalance}";
    }
}

public class SystemUser
{
    public string Name { get; init; }
    public string Description { get; init; }
    public int Id { get; init; }
    public string FullName { get; init; }

    public override string ToString()
    {
        return $"Name: {Name} - ID: {Id}";
    }
}
