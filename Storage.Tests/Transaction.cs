using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Isaac.Storage.Tests
{
    public enum TransactionType
    {
        Income,
        Expense
    }

    public class Transaction
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int ID { get; set; }
        public TransactionType TransType { get; set; }
        public SystemUser ResponsibleUser { get; set; }
        public decimal OriginalValue { get; set; }
        public decimal ActualValue { get; set; }
        public decimal Interest { get; set; }
        public DateTime InsertionDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool Completed { get; set; }
        public bool Deleted { get; set; }
        public Wallet SourceWallet { get; set; }
        public Supplier Supplier { get; set; }
        public Category Category { get; set; }

        public override string ToString()
        {
            return $"Name: {Name} - Value: {OriginalValue}";
        }
    }

    public class Category
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public SubCategory SubCategory { get; set; }

        public override string ToString()
        {
            return $"Name: {Name} - ID: {ID}";
        }
    }

    public class SubCategory
    {
        public string Name { get; set; }
        public int ParentID { get; set; }
        public int ID { get; set; }

        public override string ToString()
        {
            return $"Name: {Name} - ParentID: {ParentID} - ID: {ID}";
        }
    }

    public class Supplier
    {
        public string Name { get; set; }
        public int ID { get; set; }

        public override string ToString()
        {
            return $"Name: {Name} - ID: {ID}";
        }
    }

    public class Wallet
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime LastModificationDate { get; set; }
        public decimal CurrentBalance { get; set; }

        public override string ToString()
        {
            return $"Wallet: {Name} - Balance: {CurrentBalance}";
        }
    }

    public class SystemUser
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int ID { get; set; }
        public string FullName { get; set; }

        public override string ToString()
        {
            return $"Name: {Name} - ID: {ID}";
        }
    }
}
