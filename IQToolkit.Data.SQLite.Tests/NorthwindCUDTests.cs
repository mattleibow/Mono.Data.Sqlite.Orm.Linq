using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using IQToolkit;
using IQToolkit.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    [TestClass]
    public class NorthwindCUDTests : TestBase
    {
        private static Northwind db;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            // TODO: copy database to test directory and use it, instead of modifying original database
            var provider = DbEntityProvider.From(TestConstants.ConnectionString, TestConstants.MappingId);
            db = new Northwind(provider);
            InitBase(provider);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupBase();
        }

        [TestInitialize]
        public void InitializeTest()
        {
            this.CleaupDatabase();
        }

        [TestCleanup]
        public void CleanupTest()
        {
            this.CleaupDatabase();
        }

        private void CleaupDatabase()
        {
            ExecSilent("DELETE FROM Orders WHERE CustomerID LIKE 'XX%'");
            ExecSilent("DELETE FROM Customers WHERE CustomerID LIKE 'XX%'");
        }

        [TestMethod]
        public void TestInsertCustomerNoResult()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };
            var result = db.Customers.Insert(cust);
            AssertValue(1, result);  // returns 1 for success
        }

        [TestMethod]
        public void TestInsertCustomerNoResultAsync()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };
            var result = db.Customers.InsertAsync(cust).Result;
            AssertValue(1, result);  // returns 1 for success
        }

        [TestMethod]
        public void TestInsertCustomerWithResult()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };
            var result = db.Customers.Insert(cust, c => c.City);
            AssertValue(result, "Seattle");  // should be value we asked for
        }

        [TestMethod]
        public void TestInsertCustomerWithResultAsync()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };
            var result = db.Customers.InsertAsync(cust, c => c.City).Result;
            AssertValue(result, "Seattle");  // should be value we asked for
        }

        [TestMethod]
        public void TestBatchInsertCustomersNoResult()
        {
            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    });
            var results = db.Customers.Batch(custs, (u, c) => u.Insert(c));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchInsertCustomersNoResultAsync()
        {
            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });
            var results = db.Customers.BatchAsync(custs, (u, c) => u.Insert(c)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchInsertCustomersWithResult()
        {
            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });
            var results = db.Customers.Batch(custs, (u, c) => u.Insert(c, d => d.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Seattle")));
        }

        [TestMethod]
        public void TestBatchInsertCustomersWithResultAsync()
        {
            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });
            var results = db.Customers.BatchAsync(custs, (u, c) => u.Insert(c, d => d.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Seattle")));
        }

        [TestMethod]
#if ACCESS
        [Ignore] // OrderID column is not set to AutoNumber
#endif
        public void TestInsertOrderWithNoResult()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"
            var order = new Order
            {
                CustomerID = "XX1",
                OrderDate = DateTime.Today,
            };
            var result = db.Orders.Insert(order);
            AssertValue(1, result);
        }

        [TestMethod]
#if ACCESS
        [Ignore] // OrderID column is not set to AutoNumber
#endif
        public void TestInsertOrderWithGeneratedIDResult()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"
            var order = new Order
            {
                CustomerID = "XX1",
                OrderDate = DateTime.Today,
            };
            var result = db.Orders.Insert(order, o => o.OrderID);
            AssertNotValue(1, result);
        }

        [TestMethod]
        public void TestUpdateCustomerNoResult()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.Update(cust);
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpdateCustomerNoResultAsync()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.UpdateAsync(cust).Result;
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpdateCustomerWithResult()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.Update(cust, null, c => c.City);
            AssertValue("Portland", result);
        }

        [TestMethod]
        public void TestUpdateCustomerWithResultAsync()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.UpdateAsync(cust, null, c => c.City).Result;
            AssertValue("Portland", result);
        }

        [TestMethod]
        public void TestUpdateCustomerWithUpdateCheckThatDoesNotSucceed()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.Update(cust, d => d.City == "Detroit");
            AssertValue(0, result); // 0 for failure
        }

        [TestMethod]
        public void TestUpdateCustomerWithUpdateCheckThatDoesNotSucceedAsync()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.UpdateAsync(cust, d => d.City == "Detroit").Result;
            AssertValue(0, result); // 0 for failure
        }

        [TestMethod]
        public void TestUpdateCustomerWithUpdateCheckThatSucceeds()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.Update(cust, d => d.City == "Seattle");
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpdateCustomerWithUpdateCheckThatSucceedsAsync()
        {
            this.TestInsertCustomerNoResult(); // create customer "XX1"

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.UpdateAsync(cust, d => d.City == "Seattle").Result;
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestBatchUpdateCustomer()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });

            var results = db.Customers.Batch(custs, (u, c) => u.Update(c));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpdateCustomerAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.Update(c)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpdateCustomerWithUpdateCheck()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var pairs = Enumerable.Range(1, n).Select(
                i => new
                {
                    original = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    },
                    current = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    }
                });

            var results = db.Customers.Batch(pairs, (u, x) => u.Update(x.current, d => d.City == x.original.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpdateCustomerWithUpdateCheckAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var pairs = Enumerable.Range(1, n).Select(
                i => new
                {
                    original = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    },
                    current = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    }
                });

            var results = db.Customers.BatchAsync(pairs, (u, x) => u.Update(x.current, d => d.City == x.original.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpdateCustomerWithResult()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    });

            var results = db.Customers.Batch(custs, (u, c) => u.Update(c, null, d => d.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Portland")));
        }

        [TestMethod]
        public void TestBatchUpdateCustomerWithResultAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.Update(c, null, d => d.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Portland")));
        }

        [TestMethod]
        public void TestBatchUpdateCustomerWithUpdateCheckAndResult()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var pairs = Enumerable.Range(1, n).Select(
                i => new
                {
                    original = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    },
                    current = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    }
                });

            var results = db.Customers.Batch(pairs, (u, x) => u.Update(x.current, d => d.City == x.original.City, d => d.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Portland")));
        }

        [TestMethod]
        public void TestBatchUpdateCustomerWithUpdateCheckAndResultAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var pairs = Enumerable.Range(1, n).Select(
                i => new
                {
                    original = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    },
                    current = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    }
                });

            var results = db.Customers.BatchAsync(pairs, (u, x) => u.Update(x.current, d => d.City == x.original.City, d => d.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Portland")));
        }

        [TestMethod]
        public void TestUpsertNewCustomerNoResult()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdate(cust);
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpsertNewCustomerNoResultAsync()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdateAsync(cust).Result;
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpsertExistingCustomerNoResult()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdate(cust);
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpsertExistingCustomerNoResultAsync()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdateAsync(cust).Result;
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpsertNewCustomerWithResult()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdate(cust, null, d => d.City);
            AssertValue("Seattle", result);
        }

        [TestMethod]
        public void TestUpsertNewCustomerWithResultAsync()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdateAsync(cust, null, d => d.City).Result;
            AssertValue("Seattle", result);
        }

        [TestMethod]
        public void TestUpsertExistingCustomerWithResult()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdate(cust, null, d => d.City);
            AssertValue("Portland", result);
        }

        [TestMethod]
        public void TestUpsertExistingCustomerWithResultAsync()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdateAsync(cust, null, d => d.City).Result;
            AssertValue("Portland", result);
        }

        [TestMethod]
        public void TestUpsertNewCustomerWithUpdateCheck()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdate(cust, d => d.City == "Portland");
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpsertNewCustomerWithUpdateCheckAsync()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdateAsync(cust, d => d.City == "Portland").Result;
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpsertExistingCustomerWithUpdateCheck()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdate(cust, d => d.City == "Seattle");
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestUpsertExistingCustomerWithUpdateCheckAsync()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Portland", // moved to Portland!
                Country = "USA"
            };

            var result = db.Customers.InsertOrUpdateAsync(cust, d => d.City == "Seattle").Result;
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestBatchUpsertNewCustomersNoResult()
        {
            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.Batch(custs, (u, c) => u.InsertOrUpdate(c));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpsertNewCustomersNoResultAsync()
        {
            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.InsertOrUpdate(c)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpsertExistingCustomersNoResult()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.Batch(custs, (u, c) => u.InsertOrUpdate(c));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpsertExistingCustomersNoResultAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.InsertOrUpdate(c)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpsertNewCustomersWithResult()
        {
            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.Batch(custs, (u, c) => u.InsertOrUpdate(c, null, d => d.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Portland")));
        }

        [TestMethod]
        public void TestBatchUpsertNewCustomersWithResultAsync()
        {
            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.InsertOrUpdate(c, null, d => d.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Portland")));
        }

        [TestMethod]
        public void TestBatchUpsertExistingCustomersWithResult()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.Batch(custs, (u, c) => u.InsertOrUpdate(c, null, d => d.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Portland")));
        }

        [TestMethod]
        public void TestBatchUpsertExistingCustomersWithResultAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.InsertOrUpdate(c, null, d => d.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, "Portland")));
        }

        [TestMethod]
        public void TestBatchUpsertNewCustomersWithUpdateCheck()
        {
            int n = 10;
            var pairs = Enumerable.Range(1, n).Select(
                i => new
                {
                    original = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    },
                    current = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    }
                });

            var results = db.Customers.Batch(pairs, (u, x) => u.InsertOrUpdate(x.current, d => d.City == x.original.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpsertNewCustomersWithUpdateCheckAsync()
        {
            int n = 10;
            var pairs = Enumerable.Range(1, n).Select(
                i => new
                {
                    original = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    },
                    current = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    }
                });

            var results = db.Customers.BatchAsync(pairs, (u, x) => u.InsertOrUpdate(x.current, d => d.City == x.original.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpsertExistingCustomersWithUpdateCheck()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var pairs = Enumerable.Range(1, n).Select(
                i => new
                {
                    original = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    },
                    current = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    }
                });

            var results = db.Customers.Batch(pairs, (u, x) => u.InsertOrUpdate(x.current, d => d.City == x.original.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchUpsertExistingCustomersWithUpdateCheckAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var pairs = Enumerable.Range(1, n).Select(
                i => new
                {
                    original = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Seattle",
                        Country = "USA"
                    },
                    current = new Customer
                    {
                        CustomerID = "XX" + i,
                        CompanyName = "Company" + i,
                        ContactName = "Contact" + i,
                        City = "Portland",
                        Country = "USA"
                    }
                });

            var results = db.Customers.BatchAsync(pairs, (u, x) => u.InsertOrUpdate(x.current, d => d.City == x.original.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestDeleteCustomer()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle", 
                Country = "USA"
            };

            var result = db.Customers.Delete(cust);
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestDeleteCustomerAsync()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };

            var result = db.Customers.DeleteAsync(cust).Result;
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestDeleteCustomerForNonExistingCustomer()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX2",
                CompanyName = "Company2",
                ContactName = "Contact2",
                City = "Seattle",
                Country = "USA"
            };

            var result = db.Customers.Delete(cust);
            AssertValue(0, result);
        }

        [TestMethod]
        public void TestDeleteCustomerForNonExistingCustomerAsync()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX2",
                CompanyName = "Company2",
                ContactName = "Contact2",
                City = "Seattle",
                Country = "USA"
            };

            var result = db.Customers.DeleteAsync(cust).Result;
            AssertValue(0, result);
        }

        [TestMethod]
        public void TestDeleteCustomerWithDeleteCheckThatSucceeds()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };

            var result = db.Customers.Delete(cust, d => d.City == "Seattle");
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestDeleteCustomerWithDeleteCheckThatSucceedsAsync()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };

            var result = db.Customers.DeleteAsync(cust, d => d.City == "Seattle").Result;
            AssertValue(1, result);
        }

        [TestMethod]
        public void TestDeleteCustomerWithDeleteCheckThatDoesNotSucceed()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };

            var result = db.Customers.Delete(cust, d => d.City == "Portland");
            AssertValue(0, result);
        }

        [TestMethod]
        public void TestDeleteCustomerWithDeleteCheckThatDoesNotSucceedAsync()
        {
            this.TestInsertCustomerNoResult();

            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };

            var result = db.Customers.DeleteAsync(cust, d => d.City == "Portland").Result;
            AssertValue(0, result);
        }

        [TestMethod]
        public void TestBatchDeleteCustomers()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });

            var results = db.Customers.Batch(custs, (u, c) => u.Delete(c));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchDeleteCustomersAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.Delete(c)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchDeleteCustomersWithDeleteCheck()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });

            var results = db.Customers.Batch(custs, (u, c) => u.Delete(c, d => d.City == c.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchDeleteCustomersWithDeleteCheckAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Seattle",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.Delete(c, d => d.City == c.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 1)));
        }

        [TestMethod]
        public void TestBatchDeleteCustomersWithDeleteCheckThatDoesNotSucceed()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.Batch(custs, (u, c) => u.Delete(c, d => d.City == c.City));
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 0)));
        }

        [TestMethod]
        public void TestBatchDeleteCustomersWithDeleteCheckThatDoesNotSucceedAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            int n = 10;
            var custs = Enumerable.Range(1, n).Select(
                i => new Customer
                {
                    CustomerID = "XX" + i,
                    CompanyName = "Company" + i,
                    ContactName = "Contact" + i,
                    City = "Portland",
                    Country = "USA"
                });

            var results = db.Customers.BatchAsync(custs, (u, c) => u.Delete(c, d => d.City == c.City)).Result;
            AssertValue(n, results.Count());
            AssertTrue(results.All(r => object.Equals(r, 0)));
        }

        [TestMethod]
        public void TestDeleteWhere()
        {
            this.TestBatchInsertCustomersNoResult();

            var result = db.Customers.Delete(c => c.CustomerID.StartsWith("XX"));
            AssertValue(10, result);
        }

        [TestMethod]
        public void TestDeleteWhereAsync()
        {
            this.TestBatchInsertCustomersNoResult();

            var result = db.Customers.DeleteAsync(c => c.CustomerID.StartsWith("XX")).Result;
            AssertValue(10, result);
        }

        [TestMethod]
        public void TestSessionIdentityCache()
        {
            NorthwindSession ns = new NorthwindSession(_provider);

            // both objects should be the same instance
            var cust = ns.Customers.Single(c => c.CustomerID == "ALFKI");
            var cust2 = ns.Customers.Single(c => c.CustomerID == "ALFKI");

            AssertNotValue(null, cust);
            AssertNotValue(null, cust2);
            AssertValue(cust, cust2);
        }

        [TestMethod]
        public void TestSessionProviderNotIdentityCached()
        {
            NorthwindSession ns = new NorthwindSession(_provider);
            Northwind db2 = new Northwind(ns.Session.Provider);

            // both objects should be different instances
            var cust = ns.Customers.Single(c => c.CustomerID == "ALFKI");
            var cust2 = ns.Customers.ProviderTable.Single(c => c.CustomerID == "ALFKI");

            AssertNotValue(null, cust);
            AssertNotValue(null, cust2);
            AssertValue(cust.CustomerID, cust2.CustomerID);
            AssertNotValue(cust, cust2);
        }

        [TestMethod]
        public void TestSessionSubmitActionOnModify()
        {
            var cust = new Customer
                {
                    CustomerID = "XX1",
                    CompanyName = "Company1",
                    ContactName = "Contact1",
                    City = "Seattle",
                    Country = "USA"
                };

            db.Customers.Insert(cust);

            var ns = new NorthwindSession(_provider);
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            // fetch the previously inserted customer
            cust = ns.Customers.Single(c => c.CustomerID == "XX1");
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            cust.ContactName = "Contact Modified";
            AssertValue(SubmitAction.Update, ns.Customers.GetSubmitAction(cust));

            ns.SubmitChanges();
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            // prove actually modified by fetching through provider
            var cust2 = db.Customers.Single(c => c.CustomerID == "XX1");
            AssertValue("Contact Modified", cust2.ContactName);

            // ready to be submitted again!
            cust.City = "SeattleX";
            AssertValue(SubmitAction.Update, ns.Customers.GetSubmitAction(cust));
        }

        [TestMethod]
        public void TestSessionSubmitActionOnInsert()
        {
            NorthwindSession ns = new NorthwindSession(_provider);
            var cust = new Customer
                {
                    CustomerID = "XX1",
                    CompanyName = "Company1",
                    ContactName = "Contact1",
                    City = "Seattle",
                    Country = "USA"
                };
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            ns.Customers.InsertOnSubmit(cust);
            AssertValue(SubmitAction.Insert, ns.Customers.GetSubmitAction(cust));

            ns.SubmitChanges();
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            cust.City = "SeattleX";
            AssertValue(SubmitAction.Update, ns.Customers.GetSubmitAction(cust));
        }

        [TestMethod]
        public void TestSessionSubmitActionOnInsertOrUpdate()
        {
            NorthwindSession ns = new NorthwindSession(_provider);
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            ns.Customers.InsertOrUpdateOnSubmit(cust);
            AssertValue(SubmitAction.InsertOrUpdate, ns.Customers.GetSubmitAction(cust));

            ns.SubmitChanges();
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            cust.City = "SeattleX";
            AssertValue(SubmitAction.Update, ns.Customers.GetSubmitAction(cust));
        }

        [TestMethod]
        public void TestSessionSubmitActionOnUpdate()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };
            db.Customers.Insert(cust);

            NorthwindSession ns = new NorthwindSession(_provider);
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            ns.Customers.UpdateOnSubmit(cust);
            AssertValue(SubmitAction.Update, ns.Customers.GetSubmitAction(cust));

            ns.SubmitChanges();
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            cust.City = "SeattleX";
            AssertValue(SubmitAction.Update, ns.Customers.GetSubmitAction(cust));
        }

        [TestMethod]
        public void TestSessionSubmitActionOnDelete()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };
            db.Customers.Insert(cust);

            NorthwindSession ns = new NorthwindSession(_provider);
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            ns.Customers.DeleteOnSubmit(cust);
            AssertValue(SubmitAction.Delete, ns.Customers.GetSubmitAction(cust));

            ns.SubmitChanges();
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            // modifications after delete don't trigger updates
            cust.City = "SeattleX";
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));
        }

        [TestMethod]
        public void TestDeleteThenInsertSamePK()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };

            var cust2 = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company2",
                ContactName = "Contact2",
                City = "Chicago",
                Country = "USA"
            };

            db.Customers.Insert(cust);

            NorthwindSession ns = new NorthwindSession(_provider);
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust2));

            ns.Customers.DeleteOnSubmit(cust);
            AssertValue(SubmitAction.Delete, ns.Customers.GetSubmitAction(cust));
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust2));

            ns.Customers.InsertOnSubmit(cust2);
            AssertValue(SubmitAction.Delete, ns.Customers.GetSubmitAction(cust));
            AssertValue(SubmitAction.Insert, ns.Customers.GetSubmitAction(cust2));

            ns.SubmitChanges();
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust2));

            // modifications after delete don't trigger updates
            cust.City = "SeattleX";
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            // modifications after insert do trigger updates
            cust2.City = "ChicagoX";
            AssertValue(SubmitAction.Update, ns.Customers.GetSubmitAction(cust2));
        }

        [TestMethod]
        public void TestInsertThenDeleteSamePK()
        {
            var cust = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company1",
                ContactName = "Contact1",
                City = "Seattle",
                Country = "USA"
            };

            var cust2 = new Customer
            {
                CustomerID = "XX1",
                CompanyName = "Company2",
                ContactName = "Contact2",
                City = "Chicago",
                Country = "USA"
            };

            db.Customers.Insert(cust);

            NorthwindSession ns = new NorthwindSession(_provider);
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust2));

            ns.Customers.InsertOnSubmit(cust2);
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));
            AssertValue(SubmitAction.Insert, ns.Customers.GetSubmitAction(cust2));

            ns.Customers.DeleteOnSubmit(cust);
            AssertValue(SubmitAction.Delete, ns.Customers.GetSubmitAction(cust));
            AssertValue(SubmitAction.Insert, ns.Customers.GetSubmitAction(cust2));

            ns.SubmitChanges();
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust2));

            // modifications after delete don't trigger updates
            cust.City = "SeattleX";
            AssertValue(SubmitAction.None, ns.Customers.GetSubmitAction(cust));

            // modifications after insert do trigger updates
            cust2.City = "ChicagoX";
            AssertValue(SubmitAction.Update, ns.Customers.GetSubmitAction(cust2));
        }
    }
}
