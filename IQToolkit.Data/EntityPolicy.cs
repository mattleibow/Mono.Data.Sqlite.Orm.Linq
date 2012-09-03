// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace IQToolkit.Data
{
    using Common;
    using Mapping;

	/// <summary>
	/// Defines query execution & materialization policies. 
	/// </summary>
    public class EntityPolicy
    {
        HashSet<MemberInfo> included = new HashSet<MemberInfo>();
        HashSet<MemberInfo> deferred = new HashSet<MemberInfo>();
        Dictionary<MemberInfo, List<LambdaExpression>> operations = new Dictionary<MemberInfo, List<LambdaExpression>>();

		public static readonly EntityPolicy Default = new EntityPolicy();

		public void Apply(LambdaExpression fnApply)
        {
            if (fnApply == null)
                throw new ArgumentNullException("fnApply");
            if (fnApply.Parameters.Count != 1)
                throw new ArgumentException("Apply function has wrong number of arguments.");
            this.AddOperation(TypeHelper.GetElementType(fnApply.Parameters[0].Type), fnApply);
        }

        public void Apply<TEntity>(Expression<Func<IEnumerable<TEntity>, IEnumerable<TEntity>>> fnApply)
        {
            Apply((LambdaExpression)fnApply);
        }

        public void Include(MemberInfo member)
        {
            Include(member, false);
        }

        public void Include(MemberInfo member, bool deferLoad)
        {
            this.included.Add(member);
            if (deferLoad)
                Defer(member);
        }

        public void IncludeWith(LambdaExpression fnMember)
        {
            IncludeWith(fnMember, false);
        }

        public void IncludeWith(LambdaExpression fnMember, bool deferLoad)
        {
            var rootMember = RootMemberFinder.Find(fnMember, fnMember.Parameters[0]);
            if (rootMember == null)
                throw new InvalidOperationException("Subquery does not originate with a member access");
            Include(rootMember.Member, deferLoad);
            if (rootMember != fnMember.Body)
            {
                AssociateWith(fnMember);
            }
        }

        public void IncludeWith<TEntity>(Expression<Func<TEntity, object>> fnMember)
        {
            IncludeWith((LambdaExpression)fnMember, false);
        }

        public void IncludeWith<TEntity>(Expression<Func<TEntity, object>> fnMember, bool deferLoad)
        {
            IncludeWith((LambdaExpression)fnMember, deferLoad);
        }

        private void Defer(MemberInfo member)
        {
            Type mType = TypeHelper.GetMemberType(member);
            if (mType.IsGenericType)
            {
                var gType = mType.GetGenericTypeDefinition();
                if (gType != typeof(IEnumerable<>)
                    && gType != typeof(IList<>)
                    && !typeof(IDeferLoadable).IsAssignableFrom(mType))
                {
                    throw new InvalidOperationException(string.Format("The member '{0}' cannot be deferred due to its type.", member));
                }
            }
            this.deferred.Add(member);
        }

        public void AssociateWith(LambdaExpression memberQuery)
        {
            var rootMember = RootMemberFinder.Find(memberQuery, memberQuery.Parameters[0]);
            if (rootMember == null)
                throw new InvalidOperationException("Subquery does not originate with a member access");
            if (rootMember != memberQuery.Body)
            {
                var memberParam = Expression.Parameter(rootMember.Type, "root");
                var newBody = ExpressionReplacer.Replace(memberQuery.Body, rootMember, memberParam);
                this.AddOperation(rootMember.Member, Expression.Lambda(newBody, memberParam));
            }
        }

        private void AddOperation(MemberInfo member, LambdaExpression operation)
        {
            List<LambdaExpression> memberOps;
            if (!this.operations.TryGetValue(member, out memberOps))
            {
                memberOps = new List<LambdaExpression>();
                this.operations.Add(member, memberOps);
            }
            memberOps.Add(operation);
        }

        public void AssociateWith<TEntity>(Expression<Func<TEntity, IEnumerable>> memberQuery)
        {
            AssociateWith((LambdaExpression)memberQuery);
        }

        class RootMemberFinder : ExpressionVisitor
        {
            MemberExpression found;
            ParameterExpression parameter;

            private RootMemberFinder(ParameterExpression parameter)
            {
                this.parameter = parameter;
            }

            public static MemberExpression Find(Expression query, ParameterExpression parameter)
            {
                var finder = new RootMemberFinder(parameter);
                finder.Visit(query);
                return finder.found;
            }

            protected override Expression VisitMethodCall(MethodCallExpression m)
            {
                if (m.Object != null)
                {
                    this.Visit(m.Object);
                }
                else if (m.Arguments.Count > 0)
                {
                    this.Visit(m.Arguments[0]);
                }
                return m;
            }

            protected override Expression VisitMemberAccess(MemberExpression m)
            {
                if (m.Expression == this.parameter)
                {
                    this.found = m;
                    return m;
                }
                else
                {
                    return base.VisitMemberAccess(m);
                }
            }
        }

		/// <summary>
		/// Determines if a relationship property is to be included in the results of the query
		/// </summary>
		/// <param name="member"></param>
		/// <returns></returns>
        public virtual bool IsIncluded(MemberInfo member)
        {
            return this.included.Contains(member);
        }

		/// <summary>
		/// Determines if a relationship property is included, but the query for the related data is 
		/// deferred until the property is first accessed.
		/// </summary>
		/// <param name="member"></param>
		/// <returns></returns>
        public virtual bool IsDeferLoaded(MemberInfo member)
        {
            return this.deferred.Contains(member);
        }

        public virtual QueryPolice CreatePolice(QueryTranslator translator)
        {
			return new QueryPolice(this, translator);
        }

		public class QueryPolice
		{
			EntityPolicy policy;
			QueryTranslator translator;

			public QueryPolice(EntityPolicy policy, QueryTranslator translator)
			{
				this.policy = policy;
				this.translator = translator;
			}

			public EntityPolicy Policy
			{
				get { return this.policy; }
			}

			public QueryTranslator Translator
			{
				get { return this.translator; }
			}

			public virtual Expression ApplyPolicy(Expression expression, MemberInfo member)
			{
				List<LambdaExpression> ops;
				if (this.policy.operations.TryGetValue(member, out ops))
				{
					var result = expression;
					foreach (var fnOp in ops)
					{
						var pop = PartialEvaluator.Eval(fnOp, this.Translator.Mapper.Mapping.CanBeEvaluatedLocally);
						result = this.Translator.Mapper.ApplyMapping(Expression.Invoke(pop, result));
					}
					var projection = (ProjectionExpression)result;
					if (projection.Type != expression.Type)
					{
						var fnAgg = Aggregator.GetAggregator(expression.Type, projection.Type);
						projection = new ProjectionExpression(projection.Select, projection.Projector, fnAgg);
					}
					return projection;
				}
				return expression;
			}

			/// <summary>
			/// Provides policy specific query translations.  This is where choices about inclusion of related objects and how
			/// heirarchies are materialized affect the definition of the queries.
			/// </summary>
			/// <param name="expression"></param>
			/// <returns></returns>
			public virtual Expression Translate(Expression expression)
			{
				// add included relationships to client projection
				var rewritten = RelationshipIncluder.Include(this.translator.Mapper, expression);
				if (rewritten != expression)
				{
					expression = rewritten;
					expression = UnusedColumnRemover.Remove(expression);
					expression = RedundantColumnRemover.Remove(expression);
					expression = RedundantSubqueryRemover.Remove(expression);
					expression = RedundantJoinRemover.Remove(expression);
				}

				// convert any singleton (1:1 or n:1) projections into server-side joins (cardinality is preserved)
				rewritten = SingletonProjectionRewriter.Rewrite(this.translator.Linguist.Language, expression);
				if (rewritten != expression)
				{
					expression = rewritten;
					expression = UnusedColumnRemover.Remove(expression);
					expression = RedundantColumnRemover.Remove(expression);
					expression = RedundantSubqueryRemover.Remove(expression);
					expression = RedundantJoinRemover.Remove(expression);
				}

				// convert projections into client-side joins
				rewritten = ClientJoinedProjectionRewriter.Rewrite(this.policy, this.translator.Linguist.Language, expression);
				if (rewritten != expression)
				{
					expression = rewritten;
					expression = UnusedColumnRemover.Remove(expression);
					expression = RedundantColumnRemover.Remove(expression);
					expression = RedundantSubqueryRemover.Remove(expression);
					expression = RedundantJoinRemover.Remove(expression);
				}

				return expression;
			}

			/// <summary>
			/// Converts a query into an execution plan.  The plan is an function that executes the query and builds the
			/// resulting objects.
			/// </summary>
			/// <param name="projection"></param>
			/// <param name="provider"></param>
			/// <returns></returns>
			public virtual Expression BuildExecutionPlan(Expression query, Expression provider)
			{
				return ExecutionBuilder.Build(this.translator.Linguist, this.policy, query, provider);
			}
		}
    }
}