// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    using System.Runtime.CompilerServices;
    using IQToolkit.Data;
    using IQToolkit.Data.Common;

    public class TestBase
    {
        protected class TestFailureException : Exception
        {
            internal TestFailureException(string message)
                : base(message)
            {
            }
        }

        protected static DbEntityProvider _provider;
        private static readonly object gate = new object();
        private static bool _executeQueries;
        private static string _baseLinePath;
        private static string _newBaseLinePath;
        private static Dictionary<string, string> _baselines;
        private static Dictionary<string, string> _newBaselines;

        protected TestBase()
        {
        }

        protected static void InitBase(DbEntityProvider provider, bool executeQueries = false, string baseLineFileName = null)
        {
            lock (gate)
            {
                _provider = provider;
                _executeQueries = executeQueries;
                _baseLinePath = baseLineFileName;
                _newBaseLinePath = string.IsNullOrEmpty(baseLineFileName) ? null : Path.ChangeExtension(baseLineFileName, "new");
            }
        }

        protected static void CleanupBase()
        {
            SaveNewBaseLines();
        }

        private static string GetBaseLine(string key)
        {
            LoadBaseLines();

            if (_baselines != null)
            {
                string text;
                if (_baselines.TryGetValue(key, out text))
                {
                    return text;
                }
            }

            return null;
        }

        private static void SetNewBaseLine(string key, string text)
        {
            lock (gate)
            {
                if (_newBaselines == null)
                {
                    _newBaselines = new Dictionary<string, string>();
                }

                _newBaselines[key] = text;
            }
        }

        private static void LoadBaseLines()
        {
            lock (gate)
            {
                if (!string.IsNullOrEmpty(_baseLinePath) && File.Exists(_baseLinePath))
                {
                    XDocument doc = XDocument.Load(_baseLinePath);
                    _baselines = doc.Root.Elements("baseline").ToDictionary(e => (string)e.Attribute("key"), e => e.Value);
                }
            }
        }

        private static void SaveNewBaseLines()
        {
            lock (gate)
            {
                if (_newBaselines != null && !string.IsNullOrEmpty(_newBaseLinePath))
                {
                    using (var writer = new XmlTextWriter(_newBaseLinePath, Encoding.UTF8))
                    {
                        writer.Formatting = Formatting.Indented;
                        writer.Indentation = 2;
                        writer.WriteStartDocument();
                        writer.WriteStartElement("baselines");

                        foreach (var baseLineName in _newBaselines.Keys.OrderBy(x => x))
                        {
                            var baseLineText = _newBaselines[baseLineName];
                            writer.WriteStartElement("baseline");
                            writer.WriteAttributeString("key", baseLineName);
                            writer.WriteWhitespace("\r\n");
                            writer.WriteString(baseLineText);
                            writer.WriteEndElement();
                        }

                        writer.WriteEndDocument();
                    }
                }
            }
        }

        protected static void TestQuery(IQueryable query, [CallerMemberName] string testName = null)
        {
            TestQuery((EntityProvider)query.Provider, query.Expression, testName, false);
        }

        protected static void TestQuery(Expression<Func<object>> query, [CallerMemberName] string testName = null)
        {
            TestQuery(_provider, query.Body, testName, false);
        }

        protected static void TestQueryFails(IQueryable query, [CallerMemberName] string testName = null)
        {
            TestQuery((EntityProvider)query.Provider, query.Expression, testName, true);
        }

        protected static void TestQueryFails(Expression<Func<object>> query, [CallerMemberName] string testName = null)
        {
            TestQuery(_provider, query.Body, testName, true);
        }

        protected static void TestQuery(EntityProvider pro, Expression query, string testName, bool expectedToFail)
        {
            if (query.NodeType == ExpressionType.Convert && query.Type == typeof(object))
            {
                query = ((UnaryExpression)query).Operand; // remove box
            }

            if (pro.Log != null)
            {
                DbExpressionWriter.Write(pro.Log, pro.Language, query);
                pro.Log.WriteLine();
                pro.Log.WriteLine("==>");
            }

            string queryText = pro.GetQueryText(query);
            SetNewBaseLine(testName, queryText);

            if (_executeQueries)
            {
                object result = pro.Execute(query);
                IEnumerable seq = result as IEnumerable;
                if (seq != null)
                {
                    // iterate results
                    foreach (var item in seq)
                    {
                    }
                }
                else
                {
                    IDisposable disposable = result as IDisposable;
                    if (disposable != null)
                        disposable.Dispose();
                }
            }
            else if (pro.Log != null)
            {
                var text = pro.GetQueryText(query);
                pro.Log.WriteLine(text);
                pro.Log.WriteLine();
            }

            string expected = GetBaseLine(testName);
            if (expected != null)
            {
                string trimActual = TrimExtraWhiteSpace(queryText).Trim();
                string trimExpected = TrimExtraWhiteSpace(expected).Trim();
                Assert.AreEqual(trimExpected, trimActual);
            }
        }

        private static string TrimExtraWhiteSpace(string s)
        {
            StringBuilder sb = new StringBuilder();
            bool lastWasWhiteSpace = false;
            foreach (char c in s)
            {
                bool isWS = char.IsWhiteSpace(c);
                if (!isWS || !lastWasWhiteSpace)
                {
                    if (isWS)
                        sb.Append(' ');
                    else
                        sb.Append(c);
                    lastWasWhiteSpace = isWS;
                }
            }
            return sb.ToString();
        }

        private static void WriteDifferences(string s1, string s2)
        {
            int start = 0;
            bool same = true;
            for (int i = 0, n = Math.Min(s1.Length, s2.Length); i < n; i++)
            {
                bool matches = s1[i] == s2[i];
                if (matches != same)
                {
                    if (i > start)
                    {
                        Console.ForegroundColor = same ? ConsoleColor.Gray : ConsoleColor.White;
                        Console.Write(s1.Substring(start, i - start));
                    }
                    start = i;
                    same = matches;
                }
            }
            if (start < s1.Length)
            {
                Console.ForegroundColor = same ? ConsoleColor.Gray : ConsoleColor.White;
                Console.Write(s1.Substring(start));
            }
            Console.WriteLine();
        }

        protected static void AssertEqual(string expected, string actual)
        {
            Assert.AreEqual(expected, actual);
        }

        protected static void AssertTrue(bool truth, string message)
        {
            Assert.IsTrue(truth, message);
        }

        protected static void AssertValue(object expected, object actual)
        {
            Assert.AreEqual(expected, actual);
        }

        protected static void AssertValue(double expected, double actual, double epsilon)
        {
            Assert.IsTrue(actual >= expected - epsilon && actual <= expected + epsilon);
        }

        protected static void AssertNotValue(object notExpected, object actual)
        {
            Assert.AreNotEqual(notExpected, actual);
        }

        protected static void AssertTrue(bool value)
        {
            Assert.IsTrue(value);
        }

        protected static void AssertFalse(bool value)
        {
            Assert.IsFalse(value);
        }

        protected static bool ExecSilent(string commandText)
        {
            try
            {
                _provider.ExecuteCommand(commandText);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected static double RunTimedTest(int iterations, Action<int> action)
        {
            action(0); // throw out the first one  (makes sure code is loaded)

            var timer = new System.Diagnostics.Stopwatch();
            
            timer.Start();
            
            for (int i = 1; i <= iterations; i++)
            {
                action(i);
            }
            
            timer.Stop();

            return timer.Elapsed.TotalSeconds / iterations;
        }
    }
}