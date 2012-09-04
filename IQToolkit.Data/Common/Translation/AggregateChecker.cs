﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace IQToolkit.Data.Common
{
	/// <summary>
	/// Determines if a SelectExpression contains any aggregate expressions
	/// </summary>
	internal class AggregateChecker : DbExpressionVisitor
	{
		private bool hasAggregate = false;

		private AggregateChecker()
		{
		}

		internal static bool HasAggregates(SelectExpression expression)
		{
			AggregateChecker checker = new AggregateChecker();
			checker.Visit(expression);
			return checker.hasAggregate;
		}

		protected override Expression VisitAggregate(AggregateExpression aggregate)
		{
			this.hasAggregate = true;
			return aggregate;
		}

		protected override Expression VisitSelect(SelectExpression select)
		{
			// only consider aggregates in these locations
			this.Visit(select.Where);
			this.VisitOrderBy(select.OrderBy);
			this.VisitColumnDeclarations(select.Columns);
			return select;
		}

		protected override Expression VisitSubquery(SubqueryExpression subquery)
		{
			// don't count aggregates in subqueries
			return subquery;
		}
	}
}