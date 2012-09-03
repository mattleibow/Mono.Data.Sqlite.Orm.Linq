// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    using IQToolkit.Data;
    using IQToolkit.Data.Mapping;

    [TestClass]
    public partial class NorthwindTranslationTests : TestBase
    {
        private static Northwind db;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            var provider = DbEntityProvider.From(TestConstants.ConnectionString, TestConstants.MappingId);
            var baseLineFileName = "NorthwindTranslation_" + provider.GetType().Name + ".base";

            db = new Northwind(provider);

            InitBase(provider, executeQueries: false, baseLineFileName: baseLineFileName);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupBase();
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhere()
        {
            TestQuery(db.Customers.Where(c => c.City == "London"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhereTrue()
        {
            TestQuery(db.Customers.Where(c => true));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhereFalse()
        {
            TestQuery(db.Customers.Where(c => false));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCompareEntityEqual()
        {
            Customer alfki = new Customer { CustomerID = "ALFKI" };
            TestQuery(
                db.Customers.Where(c => c == alfki)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCompareEntityNotEqual()
        {
            Customer alfki = new Customer { CustomerID = "ALFKI" };
            TestQuery(
                db.Customers.Where(c => c != alfki)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCompareConstructedEqual()
        {
            TestQuery(
                db.Customers.Where(c => new { x = c.City } == new { x = "London" })
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCompareConstructedMultiValueEqual()
        {
            TestQuery(
                db.Customers.Where(c => new { x = c.City, y = c.Country } == new { x = "London", y = "UK" })
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCompareConstructedMultiValueNotEqual()
        {
            TestQuery(
                db.Customers.Where(c => new { x = c.City, y = c.Country } != new { x = "London", y = "UK" })
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCompareConstructed()
        {
            TestQuery(
                db.Customers.Where(c => new { x = c.City } == new { x = "London" })
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectScalar()
        {
            TestQuery(db.Customers.Select(c => c.City));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAnonymousOne()
        {
            TestQuery(db.Customers.Select(c => new { c.City }));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAnonymousTwo()
        {
            TestQuery(db.Customers.Select(c => new { c.City, c.Phone }));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAnonymousThree()
        {
            TestQuery(db.Customers.Select(c => new { c.City, c.Phone, c.Country }));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectCustomerTable()
        {
            TestQuery(db.Customers);
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectCustomerIdentity()
        {
            TestQuery(db.Customers.Select(c => c));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAnonymousWithObject()
        {
            TestQuery(db.Customers.Select(c => new { c.City, c }));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAnonymousNested()
        {
            TestQuery(db.Customers.Select(c => new { c.City, Country = new { c.Country } }));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAnonymousEmpty()
        {
            TestQuery(db.Customers.Select(c => new { }));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAnonymousLiteral()
        {
            TestQuery(db.Customers.Select(c => new { X = 10 }));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectConstantInt()
        {
            TestQuery(db.Customers.Select(c => 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectConstantNullString()
        {
            TestQuery(db.Customers.Select(c => (string)null));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectLocal()
        {
            int x = 10;
            TestQuery(db.Customers.Select(c => x));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectNestedCollection()
        {
            TestQuery(
                from c in db.Customers
                where c.City == "London"
                select db.Orders.Where(o => o.CustomerID == c.CustomerID && o.OrderDate.Year == 1997).Select(o => o.OrderID)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectNestedCollectionInAnonymousType()
        {
            TestQuery(
                from c in db.Customers
                where c.CustomerID == "ALFKI"
                select new { Foos = db.Orders.Where(o => o.CustomerID == c.CustomerID && o.OrderDate.Year == 1997).Select(o => o.OrderID) }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestJoinCustomerOrders()
        {
            TestQuery(
                from c in db.Customers
                join o in db.Orders on c.CustomerID equals o.CustomerID
                select new { c.ContactName, o.OrderID }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestJoinMultiKey()
        {
            TestQuery(
                from c in db.Customers
                join o in db.Orders on new { a = c.CustomerID, b = c.CustomerID } equals new { a = o.CustomerID, b = o.CustomerID }
                select new { c, o }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestJoinIntoCustomersOrders()
        {
            TestQuery(
                from c in db.Customers
                join o in db.Orders on c.CustomerID equals o.CustomerID into ords
                select new { cust = c, ords = ords.ToList() }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestJoinIntoCustomersOrdersCount()
        {
            TestQuery(
                from c in db.Customers
                join o in db.Orders on c.CustomerID equals o.CustomerID into ords
                select new { cust = c, ords = ords.Count() }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestJoinIntoDefaultIfEmpty()
        {
            TestQuery(
                from c in db.Customers
                join o in db.Orders on c.CustomerID equals o.CustomerID into ords
                from o in ords.DefaultIfEmpty()
                select new { c, o }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectManyCustomerOrders()
        {
            TestQuery(
                from c in db.Customers
                from o in db.Orders
                where c.CustomerID == o.CustomerID
                select new { c.ContactName, o.OrderID }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMultipleJoinsWithJoinConditionsInWhere()
        {
            // this should reduce to inner joins
            TestQuery(
                from c in db.Customers
                from o in db.Orders
                from d in db.OrderDetails
                where o.CustomerID == c.CustomerID && o.OrderID == d.OrderID
                where c.CustomerID == "ALFKI"
                select d.ProductID
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMultipleJoinsWithMissingJoinCondition()
        {
            // this should force a naked cross join
            TestQuery(
                from c in db.Customers
                from o in db.Orders
                from d in db.OrderDetails
                where o.CustomerID == c.CustomerID /*&& o.OrderID == d.OrderID*/
                where c.CustomerID == "ALFKI"
                select d.ProductID
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderBy()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.CustomerID)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderBySelect()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.CustomerID).Select(c => c.ContactName)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByOrderBy()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.CustomerID).OrderBy(c => c.Country).Select(c => c.City)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByThenBy()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.CustomerID).ThenBy(c => c.Country).Select(c => c.City)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByDescending()
        {
            TestQuery(
                db.Customers.OrderByDescending(c => c.CustomerID).Select(c => c.City)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByDescendingThenBy()
        {
            TestQuery(
                db.Customers.OrderByDescending(c => c.CustomerID).ThenBy(c => c.Country).Select(c => c.City)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByDescendingThenByDescending()
        {
            TestQuery(
                db.Customers.OrderByDescending(c => c.CustomerID).ThenByDescending(c => c.Country).Select(c => c.City)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByJoin()
        {
            TestQuery(
                from c in db.Customers.OrderBy(c => c.CustomerID)
                join o in db.Orders.OrderBy(o => o.OrderID) on c.CustomerID equals o.CustomerID
                select new { c.CustomerID, o.OrderID }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderBySelectMany()
        {
            TestQuery(
                from c in db.Customers.OrderBy(c => c.CustomerID)
                from o in db.Orders.OrderBy(o => o.OrderID)
                where c.CustomerID == o.CustomerID
                select new { c.ContactName, o.OrderID }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupBy()
        {
            TestQuery(
                db.Customers.GroupBy(c => c.City)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupBySelectMany()
        {
            TestQuery(
                db.Customers.GroupBy(c => c.City).SelectMany(g => g)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupBySum()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID).Select(g => g.Sum(o => o.OrderID))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByCount()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID).Select(g => g.Count())
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByLongCount()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID).Select(g => g.LongCount())
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupBySumMinMaxAvg()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID).Select(g =>
                    new
                    {
                        Sum = g.Sum(o => o.OrderID),
                        Min = g.Min(o => o.OrderID),
                        Max = g.Max(o => o.OrderID),
                        Avg = g.Average(o => o.OrderID)
                    })
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByWithResultSelector()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID, (k, g) =>
                    new
                    {
                        Sum = g.Sum(o => o.OrderID),
                        Min = g.Min(o => o.OrderID),
                        Max = g.Max(o => o.OrderID),
                        Avg = g.Average(o => o.OrderID)
                    })
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByWithElementSelectorSum()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID, o => o.OrderID).Select(g => g.Sum())
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByWithElementSelector()
        {
            // note: groups are retrieved through a separately execute subquery per row
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID, o => o.OrderID)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByWithElementSelectorSumMax()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID, o => o.OrderID).Select(g => new { Sum = g.Sum(), Max = g.Max() })
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByWithAnonymousElement()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID, o => new { o.OrderID }).Select(g => g.Sum(x => x.OrderID))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByWithTwoPartKey()
        {
            TestQuery(
                db.Orders.GroupBy(o => new { o.CustomerID, o.OrderDate }).Select(g => g.Sum(o => o.OrderID))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByGroupBy()
        {
            // note: order-by is lost when group-by is applied (the sequence of groups is not ordered)
            TestQuery(
                db.Orders.OrderBy(o => o.OrderID).GroupBy(o => o.CustomerID).Select(g => g.Sum(o => o.OrderID))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByGroupBySelectMany()
        {
            // note: order-by is preserved within grouped sub-collections
            TestQuery(
                db.Orders.OrderBy(o => o.OrderID).GroupBy(o => o.CustomerID).SelectMany(g => g)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSumWithNoArg()
        {
            TestQuery(
                () => db.Orders.Select(o => o.OrderID).Sum()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSumWithArg()
        {
            TestQuery(
                () => db.Orders.Sum(o => o.OrderID)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCountWithNoPredicate()
        {
            TestQuery(
                () => db.Orders.Count()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCountWithPredicate()
        {
            TestQuery(
                () => db.Orders.Count(o => o.CustomerID == "ALFKI")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinct()
        {
            TestQuery(
                db.Customers.Distinct()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctScalar()
        {
            TestQuery(
                db.Customers.Select(c => c.City).Distinct()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOrderByDistinct()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.CustomerID).Select(c => c.City).Distinct()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctOrderBy()
        {
            TestQuery(
                db.Customers.Select(c => c.City).Distinct().OrderBy(c => c)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctGroupBy()
        {
            TestQuery(
                db.Orders.Distinct().GroupBy(o => o.CustomerID)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestGroupByDistinct()
        {
            TestQuery(
                db.Orders.GroupBy(o => o.CustomerID).Distinct()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctCount()
        {
            TestQuery(
                () => db.Customers.Distinct().Count()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectDistinctCount()
        {
            // cannot do: SELECT COUNT(DISTINCT some-colum) FROM some-table
            // because COUNT(DISTINCT some-column) does not count nulls
            TestQuery(
                () => db.Customers.Select(c => c.City).Distinct().Count()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectSelectDistinctCount()
        {
            TestQuery(
                () => db.Customers.Select(c => c.City).Select(c => c).Distinct().Count()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctCountPredicate()
        {
            TestQuery(
                () => db.Customers.Distinct().Count(c => c.CustomerID == "ALFKI")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctSumWithArg()
        {
            TestQuery(
                () => db.Orders.Distinct().Sum(o => o.OrderID)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectDistinctSum()
        {
            TestQuery(
                () => db.Orders.Select(o => o.OrderID).Distinct().Sum()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestTake()
        {
            TestQuery(
                db.Orders.Take(5)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestTakeDistinct()
        {
            // distinct must be forced to apply after top has been computed
            TestQuery(
                db.Orders.Take(5).Distinct()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctTake()
        {
            // top must be forced to apply after distinct has been computed
            TestQuery(
                db.Orders.Distinct().Take(5)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctTakeCount()
        {
            TestQuery(
                () => db.Orders.Distinct().Take(5).Count()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestTakeDistinctCount()
        {
            TestQuery(
                () => db.Orders.Take(5).Distinct().Count()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLCE
        [Ignore]
#endif
        public void TestSkip()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Skip(5)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLCE
        [Ignore]
#endif
        public void TestTakeSkip()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Take(10).Skip(5)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLCE
        [Ignore]
#endif
        public void TestDistinctSkip()
        {
            TestQuery(
                db.Customers.Distinct().OrderBy(c => c.ContactName).Skip(5)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSkipTake()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Skip(5).Take(10)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDistinctSkipTake()
        {
            TestQuery(
                db.Customers.Distinct().OrderBy(c => c.ContactName).Skip(5).Take(10)
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLCE
        [Ignore]
#endif
        public void TestSkipDistinct()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Skip(5).Distinct()
                );
        }

        //[ExcludeProvider("Access")]
        [TestMethod]
        [TestCategory("Translation")]
        public void TestSkipTakeDistinct()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Skip(5).Take(10).Distinct()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLCE
        [Ignore]
#endif
        public void TestTakeSkipDistinct()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Take(10).Skip(5).Distinct()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestFirst()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).First()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestFirstPredicate()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).First(c => c.City == "London")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhereFirst()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).Where(c => c.City == "London").First()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestFirstOrDefault()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).FirstOrDefault()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestFirstOrDefaultPredicate()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).FirstOrDefault(c => c.City == "London")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhereFirstOrDefault()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).Where(c => c.City == "London").FirstOrDefault()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestReverse()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Reverse()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestReverseReverse()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Reverse().Reverse()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestReverseWhereReverse()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Reverse().Where(c => c.City == "London").Reverse()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestReverseTakeReverse()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Reverse().Take(5).Reverse()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestReverseWhereTakeReverse()
        {
            TestQuery(
                db.Customers.OrderBy(c => c.ContactName).Reverse().Where(c => c.City == "London").Take(5).Reverse()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestLast()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).Last()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestLastPredicate()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).Last(c => c.City == "London")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhereLast()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).Where(c => c.City == "London").Last()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestLastOrDefault()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).LastOrDefault()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestLastOrDefaultPredicate()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).LastOrDefault(c => c.City == "London")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhereLastOrDefault()
        {
            TestQuery(
                () => db.Customers.OrderBy(c => c.ContactName).Where(c => c.City == "London").LastOrDefault()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSingle()
        {
            TestQueryFails(
                () => db.Customers.Single()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSinglePredicate()
        {
            TestQuery(
                () => db.Customers.Single(c => c.CustomerID == "ALFKI")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhereSingle()
        {
            TestQuery(
                () => db.Customers.Where(c => c.CustomerID == "ALFKI").Single()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSingleOrDefault()
        {
            TestQueryFails(
                () => db.Customers.SingleOrDefault()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSingleOrDefaultPredicate()
        {
            TestQuery(
                () => db.Customers.SingleOrDefault(c => c.CustomerID == "ALFKI")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestWhereSingleOrDefault()
        {
            TestQuery(
                () => db.Customers.Where(c => c.CustomerID == "ALFKI").SingleOrDefault()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestAnyWithSubquery()
        {
            TestQuery(
                db.Customers.Where(c => db.Orders.Where(o => o.CustomerID == c.CustomerID).Any(o => o.OrderDate.Year == 1997))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestAnyWithSubqueryNoPredicate()
        {
            TestQuery(
                db.Customers.Where(c => db.Orders.Where(o => o.CustomerID == c.CustomerID).Any())
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestAnyWithLocalCollection()
        {
            string[] ids = new[] { "ABCDE", "ALFKI" };
            TestQuery(
                db.Customers.Where(c => ids.Any(id => c.CustomerID == id))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestAnyTopLevel()
        {
            TestQuery(
                () => db.Customers.Any()
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestAllWithSubquery()
        {
            TestQuery(
                db.Customers.Where(c => db.Orders.Where(o => o.CustomerID == c.CustomerID).All(o => o.OrderDate.Year == 1997))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestAllWithLocalCollection()
        {
            string[] patterns = new[] { "a", "e" };

            TestQuery(
                db.Customers.Where(c => patterns.All(p => c.ContactName.Contains(p)))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestAllTopLevel()
        {
            TestQuery(
                () => db.Customers.All(c => c.ContactName.StartsWith("a"))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestContainsWithSubquery()
        {
            TestQuery(
                db.Customers.Where(c => db.Orders.Select(o => o.CustomerID).Contains(c.CustomerID))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestContainsWithLocalCollection()
        {
            string[] ids = new[] { "ABCDE", "ALFKI" };
            TestQuery(
                db.Customers.Where(c => ids.Contains(c.CustomerID))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestContainsTopLevel()
        {
            TestQuery(
                () => db.Customers.Select(c => c.CustomerID).Contains("ALFKI")
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCoalesce()
        {
            TestQuery(db.Customers.Where(c => (c.City ?? "Seattle") == "Seattle"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCoalesce2()
        {
            TestQuery(db.Customers.Where(c => (c.City ?? c.Country ?? "Seattle") == "Seattle"));
        }

        // framework function tests
        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringLength()
        {
            TestQuery(db.Customers.Where(c => c.City.Length == 7));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringStartsWithLiteral()
        {
            TestQuery(db.Customers.Where(c => c.ContactName.StartsWith("M")));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringStartsWithColumn()
        {
            TestQuery(db.Customers.Where(c => c.ContactName.StartsWith(c.ContactName)));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringEndsWithLiteral()
        {
            TestQuery(db.Customers.Where(c => c.ContactName.EndsWith("s")));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringEndsWithColumn()
        {
            TestQuery(db.Customers.Where(c => c.ContactName.EndsWith(c.ContactName)));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringContainsLiteral()
        {
            TestQuery(db.Customers.Where(c => c.ContactName.Contains("and")));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringContainsColumn()
        {
            TestQuery(db.Customers.Where(c => c.ContactName.Contains(c.ContactName)));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringConcatImplicit2Args()
        {
            TestQuery(db.Customers.Where(c => c.ContactName + "X" == "X"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringConcatExplicit2Args()
        {
            TestQuery(db.Customers.Where(c => string.Concat(c.ContactName, "X") == "X"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringConcatExplicit3Args()
        {
            TestQuery(db.Customers.Where(c => string.Concat(c.ContactName, "X", c.Country) == "X"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringConcatExplicitNArgs()
        {
            TestQuery(db.Customers.Where(c => string.Concat(new string[] { c.ContactName, "X", c.Country }) == "X"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringIsNullOrEmpty()
        {
            TestQuery(db.Customers.Where(c => string.IsNullOrEmpty(c.City)));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringToUpper()
        {
            TestQuery(db.Customers.Where(c => c.City.ToUpper() == "SEATTLE"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringToLower()
        {
            TestQuery(db.Customers.Where(c => c.City.ToLower() == "seattle"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringSubstring()
        {
            TestQuery(db.Customers.Where(c => c.City.Substring(0, 4) == "Seat"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringSubstringNoLength()
        {
            TestQuery(db.Customers.Where(c => c.City.Substring(4) == "tle"));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if SQLITE
        [Ignore]
#endif
        public void TestStringIndexOf()
        {
            TestQuery(db.Customers.Where(c => c.City.IndexOf("tt") == 4));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if SQLITE
        [Ignore]
#endif
        public void TestStringIndexOfChar()
        {
            TestQuery(db.Customers.Where(c => c.City.IndexOf('t') == 4));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringTrim()
        {
            TestQuery(db.Customers.Where(c => c.City.Trim() == "Seattle"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringToString()
        {
            // just to prove this is a no op
            TestQuery(db.Customers.Where(c => c.City.ToString() == "Seattle"));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestStringReplace()
        {
            TestQuery(db.Customers.Where(c => c.City.Replace("ea", "ae") == "Saettle"));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestStringReplaceChars()
        {
            TestQuery(db.Customers.Where(c => c.City.Replace("e", "y") == "Syattly"));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestStringRemove()
        {
            TestQuery(db.Customers.Where(c => c.City.Remove(1, 2) == "Sttle"));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestStringRemoveNoCount()
        {
            TestQuery(db.Customers.Where(c => c.City.Remove(4) == "Seat"));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeConstructYMD()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate == new DateTime(o.OrderDate.Year, 1, 1)));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeConstructYMDHMS()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate == new DateTime(o.OrderDate.Year, 1, 1, 10, 25, 55)));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeDay()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.Day == 5));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeMonth()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.Month == 12));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeYear()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.Year == 1997));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeHour()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.Hour == 6));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeMinute()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.Minute == 32));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeSecond()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.Second == 47));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestDateTimeMillisecond()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.Millisecond == 200));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestDateTimeDayOfYear()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.DayOfYear == 360));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDateTimeDayOfWeek()
        {
            TestQuery(db.Orders.Where(o => o.OrderDate.DayOfWeek == DayOfWeek.Friday));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathAbs()
        {
            TestQuery(db.Orders.Where(o => Math.Abs(o.OrderID) == 10));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathAtan()
        {
            TestQuery(db.Orders.Where(o => Math.Atan(o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathCos()
        {
            TestQuery(db.Orders.Where(o => Math.Cos(o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathSin()
        {
            TestQuery(db.Orders.Where(o => Math.Sin(o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathTan()
        {
            TestQuery(db.Orders.Where(o => Math.Tan(o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathExp()
        {
            TestQuery(db.Orders.Where(o => Math.Exp(o.OrderID < 1000 ? 1 : 2) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathLog()
        {
            TestQuery(db.Orders.Where(o => Math.Log(o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathSqrt()
        {
            TestQuery(db.Orders.Where(o => Math.Sqrt(o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathPow()
        {
            TestQuery(db.Orders.Where(o => Math.Pow(o.OrderID < 1000 ? 1 : 2, 3) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestMathRoundDefault()
        {
            TestQuery(db.Orders.Where(o => Math.Round((decimal)o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLCE
        [Ignore]
#endif
        public void TestMathAcos()
        {
            TestQuery(db.Orders.Where(o => Math.Acos(1.0/o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLCE
        [Ignore]
#endif
        public void TestMathAsin()
        {
            TestQuery(db.Orders.Where(o => Math.Asin(1.0/o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestMathAtan2()
        {
            TestQuery(db.Orders.Where(o => Math.Atan2(1.0/o.OrderID, 3) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLITE
        [Ignore]
#endif
        public void TestMathLog10()
        {
            TestQuery(db.Orders.Where(o => Math.Log10(o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLITE
        [Ignore]
#endif
        public void TestMathCeiling()
        {
            TestQuery(db.Orders.Where(o => Math.Ceiling((double)o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestMathRoundToPlace()
        {
            TestQuery(db.Orders.Where(o => Math.Round((decimal)o.OrderID, 2) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLITE
        [Ignore]
#endif
        public void TestMathFloor()
        {
            TestQuery(db.Orders.Where(o => Math.Floor((double)o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if SQLITE
        [Ignore]
#endif
        public void TestMathTruncate()
        {
            TestQuery(db.Orders.Where(o => Math.Truncate((double)o.OrderID) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareToLT()
        {
            TestQuery(db.Customers.Where(c => c.City.CompareTo("Seattle") < 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareToLE()
        {
            TestQuery(db.Customers.Where(c => c.City.CompareTo("Seattle") <= 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareToGT()
        {
            TestQuery(db.Customers.Where(c => c.City.CompareTo("Seattle") > 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareToGE()
        {
            TestQuery(db.Customers.Where(c => c.City.CompareTo("Seattle") >= 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareToEQ()
        {
            TestQuery(db.Customers.Where(c => c.City.CompareTo("Seattle") == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareToNE()
        {
            TestQuery(db.Customers.Where(c => c.City.CompareTo("Seattle") != 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareLT()
        {
            TestQuery(db.Customers.Where(c => string.Compare(c.City, "Seattle") < 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareLE()
        {
            TestQuery(db.Customers.Where(c => string.Compare(c.City, "Seattle") <= 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareGT()
        {
            TestQuery(db.Customers.Where(c => string.Compare(c.City, "Seattle") > 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareGE()
        {
            TestQuery(db.Customers.Where(c => string.Compare(c.City, "Seattle") >= 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareEQ()
        {
            TestQuery(db.Customers.Where(c => string.Compare(c.City, "Seattle") == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestStringCompareNE()
        {
            TestQuery(db.Customers.Where(c => string.Compare(c.City, "Seattle") != 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntCompareTo()
        {
            // prove that x.CompareTo(y) works for types other than string
            TestQuery(db.Orders.Where(o => o.OrderID.CompareTo(1000) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDecimalCompare()
        {
            // prove that type.Compare(x,y) works with decimal
            TestQuery(db.Orders.Where(o => decimal.Compare((decimal)o.OrderID, 0.0m) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDecimalAdd()
        {
            TestQuery(db.Orders.Where(o => decimal.Add(o.OrderID, 0.0m) == 0.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDecimalSubtract()
        {
            TestQuery(db.Orders.Where(o => decimal.Subtract(o.OrderID, 0.0m) == 0.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDecimalMultiply()
        {
            TestQuery(db.Orders.Where(o => decimal.Multiply(o.OrderID, 1.0m) == 1.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDecimalDivide()
        {
            TestQuery(db.Orders.Where(o => decimal.Divide(o.OrderID, 1.0m) == 1.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if SQLCE
        [Ignore]
#endif
        public void TestDecimalRemainder()
        {
            TestQuery(db.Orders.Where(o => decimal.Remainder(o.OrderID, 1.0m) == 0.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDecimalNegate()
        {
            TestQuery(db.Orders.Where(o => decimal.Negate(o.OrderID) == 1.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDecimalRoundDefault()
        {
            TestQuery(db.Orders.Where(o => decimal.Round(o.OrderID) == 0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS
        [Ignore]
#endif
        public void TestDecimalRoundPlaces()
        {
            TestQuery(db.Orders.Where(o => decimal.Round(o.OrderID, 2) == 0.00m));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if SQLITE
        [Ignore]
#endif
        public void TestDecimalTruncate()
        {
            TestQuery(db.Orders.Where(o => decimal.Truncate(o.OrderID) == 0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLITE
        [Ignore]
#endif
        public void TestDecimalCeiling()
        {
            TestQuery(db.Orders.Where(o => decimal.Ceiling(o.OrderID) == 0.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
#if ACCESS || SQLITE
        [Ignore]
#endif
        public void TestDecimalFloor()
        {
            TestQuery(db.Orders.Where(o => decimal.Floor(o.OrderID) == 0.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestDecimalLT()
        {
            // prove that decimals are treated normally with respect to normal comparison operators
            TestQuery(db.Orders.Where(o => ((decimal)o.OrderID) < 0.0m));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntLessThan()
        {
            TestQuery(db.Orders.Where(o => o.OrderID < 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntLessThanOrEqual()
        {
            TestQuery(db.Orders.Where(o => o.OrderID <= 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntGreaterThan()
        {
            TestQuery(db.Orders.Where(o => o.OrderID > 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntGreaterThanOrEqual()
        {
            TestQuery(db.Orders.Where(o => o.OrderID >= 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntEqual()
        {
            TestQuery(db.Orders.Where(o => o.OrderID == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntNotEqual()
        {
            TestQuery(db.Orders.Where(o => o.OrderID != 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntAdd()
        {
            TestQuery(db.Orders.Where(o => o.OrderID + 0 == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntSubtract()
        {
            TestQuery(db.Orders.Where(o => o.OrderID - 0 == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntMultiply()
        {
            TestQuery(db.Orders.Where(o => o.OrderID * 1 == 1));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntDivide()
        {
            TestQuery(db.Orders.Where(o => o.OrderID / 1 == 1));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntModulo()
        {
            TestQuery(db.Orders.Where(o => o.OrderID % 1 == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntLeftShift()
        {
            TestQuery(db.Orders.Where(o => o.OrderID << 1 == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntRightShift()
        {
            TestQuery(db.Orders.Where(o => o.OrderID >> 1 == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntBitwiseAnd()
        {
            TestQuery(db.Orders.Where(o => (o.OrderID & 1) == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntBitwiseOr()
        {
            TestQuery(db.Orders.Where(o => (o.OrderID | 1) == 1));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntBitwiseExclusiveOr()
        {
            TestQuery(db.Orders.Where(o => (o.OrderID ^ 1) == 1));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntBitwiseNot()
        {
            TestQuery(db.Orders.Where(o => ~o.OrderID == 0));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestIntNegate()
        {
            TestQuery(db.Orders.Where(o => -o.OrderID == -1));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestAnd()
        {
            TestQuery(db.Orders.Where(o => o.OrderID > 0 && o.OrderID < 2000));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestOr()
        {
            TestQuery(db.Orders.Where(o => o.OrderID < 5 || o.OrderID > 10));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestNot()
        {
            TestQuery(db.Orders.Where(o => !(o.OrderID == 0)));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestEqualNull()
        {
            TestQuery(db.Customers.Where(c => c.City == null));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestEqualNullReverse()
        {
            TestQuery(db.Customers.Where(c => null == c.City));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestConditional()
        {
            TestQuery(db.Orders.Where(o => (o.CustomerID == "ALFKI" ? 1000 : 0) == 1000));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestConditional2()
        {
            TestQuery(db.Orders.Where(o => (o.CustomerID == "ALFKI" ? 1000 : o.CustomerID == "ABCDE" ? 2000 : 0) == 1000));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestConditionalTestIsValue()
        {
            TestQuery(db.Orders.Where(o => (((bool)(object)o.OrderID) ? 100 : 200) == 100));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestConditionalResultsArePredicates()
        {
            TestQuery(db.Orders.Where(o => (o.CustomerID == "ALFKI" ? o.OrderID < 10 : o.OrderID > 10)));
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectManyJoined()
        {
            TestQuery(
                from c in db.Customers
                from o in db.Orders.Where(o => o.CustomerID == c.CustomerID)
                select new { c.ContactName, o.OrderDate }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectManyJoinedDefaultIfEmpty()
        {
            TestQuery(
                from c in db.Customers
                from o in db.Orders.Where(o => o.CustomerID == c.CustomerID).DefaultIfEmpty()
                select new { c.ContactName, o.OrderDate }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectWhereAssociation()
        {
            TestQuery(
                from o in db.Orders
                where o.Customer.City == "Seattle"
                select o
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectWhereAssociations()
        {
            TestQuery(
                from o in db.Orders
                where o.Customer.City == "Seattle" && o.Customer.Phone != "555 555 5555"
                select o
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectWhereAssociationTwice()
        {
            TestQuery(
                from o in db.Orders
                where o.Customer.City == "Seattle" && o.Customer.Phone != "555 555 5555"
                select o
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAssociation()
        {
            TestQuery(
                from o in db.Orders
                select o.Customer
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAssociations()
        {
            TestQuery(
                from o in db.Orders
                select new { A = o.Customer, B = o.Customer }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSelectAssociationsWhereAssociations()
        {
            TestQuery(
                from o in db.Orders
                where o.Customer.City == "Seattle"
                where o.Customer.Phone != "555 555 5555"
                select new { A = o.Customer, B = o.Customer }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCustomersIncludeOrders()
        {
            var policy = new EntityPolicy();
            policy.IncludeWith<Customer>(c => c.Orders);
            Northwind nw = new Northwind(_provider.New(policy));

            TestQuery(
                nw.Customers
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCustomersIncludeOrdersDeferred()
        {
            var policy = new EntityPolicy();
            policy.IncludeWith<Customer>(c => c.Orders, true);
            Northwind nw = new Northwind(_provider.New(policy));

            TestQuery(
                nw.Customers
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCustomersIncludeOrdersViaConstructorOnly()
        {
            var mapping = new AttributeMapping(typeof(NorthwindX));
            var policy = new EntityPolicy();
            policy.IncludeWith<CustomerX>(c => c.Orders);
            NorthwindX nw = new NorthwindX(_provider.New(policy).New(mapping));

            TestQuery(
                nw.Customers
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCustomersWhereIncludeOrders()
        {
            var policy = new EntityPolicy();
            policy.IncludeWith<Customer>(c => c.Orders);
            Northwind nw = new Northwind(_provider.New(policy));

            TestQuery(
                from c in nw.Customers
                where c.City == "London"
                select c
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCustomersIncludeOrdersAndDetails()
        {
            var policy = new EntityPolicy();
            policy.IncludeWith<Customer>(c => c.Orders);
            policy.IncludeWith<Order>(o => o.Details);
            Northwind nw = new Northwind(_provider.New(policy));

            TestQuery(
                nw.Customers
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCustomersWhereIncludeOrdersAndDetails()
        {
            var policy = new EntityPolicy();
            policy.IncludeWith<Customer>(c => c.Orders);
            policy.IncludeWith<Order>(o => o.Details);
            Northwind nw = new Northwind(_provider.New(policy));

            TestQuery(
                from c in nw.Customers
                where c.City == "London"
                select c
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestInterfaceElementTypeAsGenericConstraint()
        {
            TestQuery(
                GetById(db.Products, 5)
                );
        }

        private static IQueryable<T> GetById<T>(IQueryable<T> query, int id) where T : IEntity
        {
            return query.Where(x => x.ID == id);
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestXmlMappingSelectCustomers()
        {
            var nw = new Northwind(_provider.New(XmlMapping.FromXml(File.ReadAllText(@"Northwind.xml"))));

            TestQuery(
                from c in db.Customers
                where c.City == "London"
                select c.ContactName
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestSingletonAssociationWithMemberAccess()
        {
            TestQuery(
                from o in db.Orders
                where o.Customer.City == "Seattle"
                where o.Customer.Phone != "555 555 5555"
                select new { A = o.Customer, B = o.Customer.City }
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCompareDateTimesWithDifferentNullability()
        {
            TestQuery(
                from o in db.Orders
                where o.OrderDate < DateTime.Today && ((DateTime?)o.OrderDate) < DateTime.Today
                select o
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestContainsWithEmptyLocalList()
        {
            var ids = new string[0];
            TestQuery(
                from c in db.Customers
                where ids.Contains(c.CustomerID)
                select c
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestContainsWithSubQuery()
        {
            var custsInLondon = db.Customers.Where(c => c.City == "London").Select(c => c.CustomerID);

            TestQuery(
                from c in db.Customers
                where custsInLondon.Contains(c.CustomerID)
                select c
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestCombineQueriesDeepNesting()
        {
            var custs = db.Customers.Where(c => c.ContactName.StartsWith("xxx"));
            var ords = db.Orders.Where(o => custs.Any(c => c.CustomerID == o.CustomerID));
            TestQuery(
                db.OrderDetails.Where(d => ords.Any(o => o.OrderID == d.OrderID))
                );
        }

        [TestMethod]
        [TestCategory("Translation")]
        public void TestLetWithSubquery()
        {
            TestQuery(
                from customer in db.Customers
                let orders =
                    from order in db.Orders
                    where order.CustomerID == customer.CustomerID
                    select order
                select new
                {
                    Customer = customer,
                    OrdersCount = orders.Count(),
                }
                );
        }
    }
}
