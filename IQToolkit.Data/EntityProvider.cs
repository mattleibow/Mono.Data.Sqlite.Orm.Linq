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
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

using Mono.Data.Sqlite;

using IQToolkit.Data.Common;
using IQToolkit.Data.Mapping;

namespace IQToolkit.Data
{
	using System.Globalization;

	/// <summary>
	/// A LINQ IQueryable query provider that executes database queries over a SqliteConnection
	/// </summary>
	public class EntityProvider : IAsyncQueryProvider, IEntityProvider, IQueryProvider
	{
		public EntityProvider New(SqliteConnection connection, QueryMapping mapping, EntityPolicy policy)
		{
			return new EntityProvider(connection, mapping, policy);
		}

		public EntityProvider New(QueryMapping mapping)
		{
			var n = New(this.Connection, mapping, this.Policy);
			n.Log = this.Log;
			return n;
		}

		public EntityProvider New(EntityPolicy policy)
		{
			var n = New(this.Connection, this.Mapping, policy);
			n.Log = this.Log;
			return n;
		}

		public static EntityProvider From(string connectionString, string mappingId)
		{
			return From(connectionString, mappingId, EntityPolicy.Default);
		}

		public static EntityProvider From(string connectionString, string mappingId, EntityPolicy policy)
		{
			return From(null, connectionString, mappingId, policy);
		}

		public static EntityProvider From(string provider, string connectionString, string mappingId)
		{
			return From(provider, connectionString, mappingId, EntityPolicy.Default);
		}

		public static EntityProvider From(string provider, string connectionString, string mappingId, EntityPolicy policy)
		{
			return From(provider, connectionString, GetMapping(mappingId), policy);
		}

		public static EntityProvider From(string provider, string connectionString, QueryMapping mapping, EntityPolicy policy)
		{
			return From(typeof(EntityProvider), connectionString, mapping, policy);
		}

		public static EntityProvider From(
			Type providerType, string connectionString, QueryMapping mapping, EntityPolicy policy)
		{
			SqliteConnection connection = new SqliteConnection();

			// is the connection string just a filename?
			if (!connectionString.Contains('='))
			{
				connectionString = "Data Source=" + connectionString;
			}

			connection.ConnectionString = connectionString;

			return (EntityProvider)Activator.CreateInstance(providerType, new object[] { connection, mapping, policy });
		}

		private EntityPolicy policy;

		IQueryable<S> IQueryProvider.CreateQuery<S>(Expression expression)
		{
			return new Query<S>(this, expression);
		}

		IQueryable IQueryProvider.CreateQuery(Expression expression)
		{
			Type elementType = TypeHelper.GetElementType(expression.Type);
			try
			{
				return
					(IQueryable)
					Activator.CreateInstance(typeof(Query<>).MakeGenericType(elementType), new object[] { this, expression });
			}
			catch (TargetInvocationException tie)
			{
				throw tie.InnerException;
			}
		}

		S IQueryProvider.Execute<S>(Expression expression)
		{
			return (S)this.Execute(expression);
		}

		object IQueryProvider.Execute(Expression expression)
		{
			return this.Execute(expression);
		}

		public virtual Task<object> ExecuteAsync(Expression expression)
		{
			return Task.Run(() => this.Execute(expression));
		}

		public virtual Task<S> ExecuteAsync<S>(Expression expression)
		{
			return Task.Run<S>(() => (S)this.Execute(expression));
		}

		private QueryCache cache;

		private readonly Dictionary<MappingEntity, IEntityTable> tables;

		public EntityProvider(SqliteConnection connection, QueryMapping mapping, EntityPolicy policy)
		{
			if (mapping == null)
			{
				throw new InvalidOperationException("Mapping not specified");
			}
			if (policy == null)
			{
				throw new InvalidOperationException("Policy not specified");
			}
			this.Mapping = mapping;
			this.policy = policy;
			this.tables = new Dictionary<MappingEntity, IEntityTable>();
			ActionOpenedConnection = false;
			if (connection == null)
			{
				throw new InvalidOperationException("Connection not specified");
			}
			this.connection = connection;
		}

		public QueryMapping Mapping { get; private set; }

		public EntityPolicy Policy
		{
			get
			{
				return this.policy;
			}
			set
			{
				this.policy = value ?? EntityPolicy.Default;
			}
		}

		public TextWriter Log { get; set; }

		public IEntityTable GetTable(MappingEntity entity)
		{
			IEntityTable table;
			if (!this.tables.TryGetValue(entity, out table))
			{
				table = this.CreateTable(entity);
				this.tables.Add(entity, table);
			}
			return table;
		}

		private IEntityTable CreateTable(MappingEntity entity)
		{
			return
				(IEntityTable)
				Activator.CreateInstance(typeof(EntityTable<>).MakeGenericType(entity.ElementType), new object[] { this, entity });
		}

		public IEntityTable<T> GetTable<T>()
		{
			return GetTable<T>(null);
		}

		public IEntityTable<T> GetTable<T>(string tableId)
		{
			return (IEntityTable<T>)this.GetTable(typeof(T), tableId);
		}

		public IEntityTable GetTable(Type type)
		{
			return GetTable(type, null);
		}

		public IEntityTable GetTable(Type type, string tableId)
		{
			return this.GetTable(this.Mapping.GetEntity(type, tableId));
		}

		public bool CanBeEvaluatedLocally(Expression expression)
		{
			return this.Mapping.CanBeEvaluatedLocally(expression);
		}

		public bool CanBeParameter(Expression expression)
		{
			Type type = TypeHelper.GetNonNullableType(expression.Type);
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Object:
					if (expression.Type == typeof(Byte[]) || expression.Type == typeof(Char[]))
					{
						return true;
					}
					return false;
				default:
					return true;
			}
		}

		public Executor CreateExecutor()
		{
			return new Executor(this);
		}

		public class EntityTable<T> : Query<T>, IEntityTable<T>, IHaveMappingEntity
		{
			private readonly MappingEntity entity;

			private readonly EntityProvider provider;

			public EntityTable(EntityProvider provider, MappingEntity entity)
				: base(provider, typeof(IEntityTable<T>))
			{
				this.provider = provider;
				this.entity = entity;
			}

			public MappingEntity Entity
			{
				get
				{
					return this.entity;
				}
			}

			public new IEntityProvider Provider
			{
				get
				{
					return this.provider;
				}
			}

			public string TableId
			{
				get
				{
					return this.entity.TableId;
				}
			}

			public Type EntityType
			{
				get
				{
					return this.entity.EntityType;
				}
			}

			public T GetById(object id)
			{
				var dbProvider = this.Provider;
				if (dbProvider != null)
				{
					IEnumerable<object> keys = id as IEnumerable<object>;
					if (keys == null)
					{
						keys = new object[] { id };
					}
					Expression query = ((EntityProvider)dbProvider).Mapping.GetPrimaryKeyQuery(
						this.entity, this.Expression, keys.Select(v => Expression.Constant(v)).ToArray());
					return this.Provider.Execute<T>(query);
				}
				return default(T);
			}

			object IEntityTable.GetById(object id)
			{
				return this.GetById(id);
			}
		}

		public virtual string GetQueryText(Expression expression)
		{
			Expression plan = this.GetExecutionPlan(expression);
			var commands = CommandGatherer.Gather(plan).Select(c => c.CommandText).ToArray();
			return string.Join("\n\n", commands);
		}

		private class CommandGatherer : DbExpressionVisitor
		{
			private readonly List<QueryCommand> commands = new List<QueryCommand>();

			public static ReadOnlyCollection<QueryCommand> Gather(Expression expression)
			{
				var gatherer = new CommandGatherer();
				gatherer.Visit(expression);
				return gatherer.commands.AsReadOnly();
			}

			protected override Expression VisitConstant(ConstantExpression c)
			{
				QueryCommand qc = c.Value as QueryCommand;
				if (qc != null)
				{
					this.commands.Add(qc);
				}
				return c;
			}
		}

		public string GetQueryPlan(Expression expression)
		{
			Expression plan = this.GetExecutionPlan(expression);
			return DbExpressionWriter.WriteToString(plan);
		}

		private QueryTranslator CreateTranslator()
		{
			return new QueryTranslator(this.Mapping, this.policy);
		}

		public void DoConnected(Action action)
		{
			this.StartUsingConnection();
			try
			{
				action();
			}
			finally
			{
				this.StopUsingConnection();
			}
		}

		public void DoTransacted(Action action)
		{
			this.StartUsingConnection();
			try
			{
				if (this.Transaction == null)
				{
					var trans = this.Connection.BeginTransaction(this.Isolation);
					try
					{
						this.Transaction = trans;
						action();
						trans.Commit();
					}
					finally
					{
						this.Transaction = null;
						trans.Dispose();
					}
				}
				else
				{
					action();
				}
			}
			finally
			{
				this.StopUsingConnection();
			}
		}

		public int ExecuteCommand(string commandText)
		{
			if (this.Log != null)
			{
				this.Log.WriteLine(commandText);
			}
			this.StartUsingConnection();
			try
			{
				SqliteCommand cmd = this.Connection.CreateCommand();
				cmd.CommandText = commandText;
				return cmd.ExecuteNonQuery();
			}
			finally
			{
				this.StopUsingConnection();
			}
		}

		/// <summary>
		/// Execute the query expression (does translation, etc.)
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public virtual object Execute(Expression expression)
		{
			LambdaExpression lambda = expression as LambdaExpression;

			if (lambda == null && this.cache != null && expression.NodeType != ExpressionType.Constant)
			{
				return this.cache.Execute(expression);
			}

			Expression plan = this.GetExecutionPlan(expression);

			if (lambda != null)
			{
				// compile & return the execution plan so it can be used multiple times
				LambdaExpression fn = Expression.Lambda(lambda.Type, plan, lambda.Parameters);
#if NOREFEMIT
                    return ExpressionEvaluator.CreateDelegate(fn);
#else
				return fn.Compile();
#endif
			}
			else
			{
				// compile the execution plan and invoke it
				Expression<Func<object>> efn = Expression.Lambda<Func<object>>(Expression.Convert(plan, typeof(object)));
#if NOREFEMIT
                    return ExpressionEvaluator.Eval(efn, new object[] { });
#else
				Func<object> fn = efn.Compile();
				return fn();
#endif
			}
		}

		/// <summary>
		/// Convert the query expression into an execution plan
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public Expression GetExecutionPlan(Expression expression)
		{
			// strip off lambda for now
			LambdaExpression lambda = expression as LambdaExpression;
			if (lambda != null)
			{
				expression = lambda.Body;
			}

			QueryTranslator translator = this.CreateTranslator();

			// translate query into client & server parts
			Expression translation = translator.Translate(expression);

			var parameters = lambda != null ? lambda.Parameters : null;
			Expression provider = this.Find(expression, parameters, typeof(EntityProvider));
			if (provider == null)
			{
				Expression rootQueryable = this.Find(expression, parameters, typeof(IQueryable));
				provider = Expression.Property(rootQueryable, typeof(IQueryable).GetProperty("Provider"));
			}

			return translator.Police.BuildExecutionPlan(translation, provider);
		}

		private Expression Find(Expression expression, IList<ParameterExpression> parameters, Type type)
		{
			if (parameters != null)
			{
				Expression found = parameters.FirstOrDefault(p => type.IsAssignableFrom(p.Type));
				if (found != null)
				{
					return found;
				}
			}
			return TypedSubtreeFinder.Find(expression, type);
		}

		public static QueryMapping GetMapping(string mappingId)
		{
			if (mappingId != null)
			{
				Type type = FindLoadedType(mappingId);
				if (type != null)
				{
					return new AttributeMapping(type);
				}
			}
			return new ImplicitMapping();
		}

		private static Type FindLoadedType(string typeName)
		{
			foreach (var assem in AppDomain.CurrentDomain.GetAssemblies())
			{
				var type = assem.GetType(typeName, false, true);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}

		private bool ActionOpenedConnection { get; set; }

		private Dictionary<QueryCommand, SqliteCommand> commandCache = new Dictionary<QueryCommand, SqliteCommand>();

		private readonly SqliteConnection connection;

		private DbTransaction transaction;

		private IsolationLevel isolation = IsolationLevel.ReadCommitted;

		private int nConnectedActions = 0;

		public SqliteConnection Connection
		{
			get
			{
				return this.connection;
			}
		}

		public DbTransaction Transaction
		{
			get
			{
				return this.transaction;
			}
			set
			{
				if (value != null && value.Connection != this.connection)
				{
					throw new InvalidOperationException("Transaction does not match connection.");
				}
				this.transaction = value;
			}
		}

		public IsolationLevel Isolation
		{
			get
			{
				return this.isolation;
			}
			set
			{
				this.isolation = value;
			}
		}

		private void StartUsingConnection()
		{
			if (this.connection.State == ConnectionState.Closed)
			{
				this.connection.Open();
				this.ActionOpenedConnection = true;
			}
			this.nConnectedActions++;
		}

		private void StopUsingConnection()
		{
			System.Diagnostics.Debug.Assert(this.nConnectedActions > 0);
			this.nConnectedActions--;
			if (this.nConnectedActions == 0 && this.ActionOpenedConnection)
			{
				this.connection.Close();
				this.ActionOpenedConnection = false;
			}
		}

		public sealed class Executor
		{
			public Executor(EntityProvider provider)
			{
				this.Provider = provider;
			}

			public EntityProvider Provider { get; private set; }

			public int RowsAffected { get; private set; }

			private bool ActionOpenedConnection
			{
				get
				{
					return this.Provider.ActionOpenedConnection;
				}
			}

			private void StartUsingConnection()
			{
				this.Provider.StartUsingConnection();
			}

			private void StopUsingConnection()
			{
				this.Provider.StopUsingConnection();
			}

			public object Convert(object value, Type type)
			{
				if (value == null)
				{
					return TypeHelper.GetDefault(type);
				}
				type = TypeHelper.GetNonNullableType(type);
				Type vtype = value.GetType();
				if (type != vtype)
				{
					if (type.IsEnum)
					{
						if (vtype == typeof(string))
						{
							return Enum.Parse(type, (string)value, true);
						}
						else
						{
							Type utype = Enum.GetUnderlyingType(type);
							if (utype != vtype)
							{
								value = System.Convert.ChangeType(value, utype, CultureInfo.InvariantCulture);
							}
							return Enum.ToObject(type, value);
						}
					}
					return System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
				}
				return value;
			}

			public IEnumerable<T> Execute<T>(
				QueryCommand command, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues)
			{
				this.LogCommand(command, paramValues);
				this.StartUsingConnection();
				try
				{
					SqliteCommand cmd = this.GetCommand(command, paramValues);
					DbDataReader reader = this.ExecuteReader(cmd);
					var result = Project(reader, fnProjector, entity, true);
					if (this.Provider.ActionOpenedConnection)
					{
						result = result.ToList();
					}
					else
					{
						result = new EnumerateOnce<T>(result);
					}
					return result;
				}
				finally
				{
					this.StopUsingConnection();
				}
			}

			private DbDataReader ExecuteReader(SqliteCommand command)
			{
				return command.ExecuteReader();
			}

			private IEnumerable<T> Project<T>(
				DbDataReader reader, Func<FieldReader, T> fnProjector, MappingEntity entity, bool closeReader)
			{
				var freader = new FieldReader(this, reader);
				try
				{
					while (reader.Read())
					{
						yield return fnProjector(freader);
					}
				}
				finally
				{
					if (closeReader)
					{
						reader.Close();
					}
				}
			}

			public int ExecuteCommand(QueryCommand query, object[] paramValues)
			{
				this.LogCommand(query, paramValues);
				this.StartUsingConnection();
				try
				{
					SqliteCommand cmd = this.GetCommand(query, paramValues);
					this.RowsAffected = cmd.ExecuteNonQuery();
					return this.RowsAffected;
				}
				finally
				{
					this.StopUsingConnection();
				}
			}

			public IEnumerable<int> ExecuteBatch(
				QueryCommand query, IEnumerable<object[]> paramSets, int batchSize, bool stream)
			{
				this.StartUsingConnection();
				try
				{
					var result = this.ExecuteBatch(query, paramSets);
					if (!stream || this.ActionOpenedConnection)
					{
						return result.ToList();
					}
					else
					{
						return new EnumerateOnce<int>(result);
					}
				}
				finally
				{
					this.StopUsingConnection();
				}
			}

			private IEnumerable<int> ExecuteBatch(QueryCommand query, IEnumerable<object[]> paramSets)
			{
				this.LogCommand(query, null);
				SqliteCommand cmd = this.GetCommand(query, null);
				foreach (var paramValues in paramSets)
				{
					this.LogParameters(query, paramValues);
					this.LogMessage("");
					this.SetParameterValues(query, cmd, paramValues);
					this.RowsAffected = cmd.ExecuteNonQuery();
					yield return this.RowsAffected;
				}
			}

			public IEnumerable<T> ExecuteBatch<T>(
				QueryCommand query,
				IEnumerable<object[]> paramSets,
				Func<FieldReader, T> fnProjector,
				MappingEntity entity,
				int batchSize,
				bool stream)
			{
				this.StartUsingConnection();
				try
				{
					var result = this.ExecuteBatch(query, paramSets, fnProjector, entity);
					if (!stream || this.ActionOpenedConnection)
					{
						return result.ToList();
					}
					else
					{
						return new EnumerateOnce<T>(result);
					}
				}
				finally
				{
					this.StopUsingConnection();
				}
			}

			private IEnumerable<T> ExecuteBatch<T>(
				QueryCommand query, IEnumerable<object[]> paramSets, Func<FieldReader, T> fnProjector, MappingEntity entity)
			{
				this.LogCommand(query, null);
				SqliteCommand cmd = this.GetCommand(query, null);
				cmd.Prepare();
				foreach (var paramValues in paramSets)
				{
					this.LogParameters(query, paramValues);
					this.LogMessage("");
					this.SetParameterValues(query, cmd, paramValues);
					var reader = this.ExecuteReader(cmd);
					var freader = new FieldReader(this, reader);
					try
					{
						if (reader.HasRows)
						{
							reader.Read();
							yield return fnProjector(freader);
						}
						else
						{
							yield return default(T);
						}
					}
					finally
					{
						reader.Close();
					}
				}
			}

			public IEnumerable<T> ExecuteDeferred<T>(
				QueryCommand query, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues)
			{
				this.LogCommand(query, paramValues);
				this.StartUsingConnection();
				try
				{
					SqliteCommand cmd = this.GetCommand(query, paramValues);
					var reader = this.ExecuteReader(cmd);
					var freader = new FieldReader(this, reader);
					try
					{
						while (reader.Read())
						{
							yield return fnProjector(freader);
						}
					}
					finally
					{
						reader.Close();
					}
				}
				finally
				{
					this.StopUsingConnection();
				}
			}

			/// <summary>
			/// Get an ADO command object initialized with the command-text and parameters
			/// </summary>
			private SqliteCommand GetCommand(QueryCommand query, object[] paramValues)
			{
				SqliteCommand cmd;
				if (!this.Provider.commandCache.TryGetValue(query, out cmd))
				{
					cmd = (SqliteCommand)this.Provider.Connection.CreateCommand();
					cmd.CommandText = query.CommandText;
					this.SetParameterValues(query, cmd, paramValues);
					cmd.Prepare();
					this.Provider.commandCache.Add(query, cmd);
					if (this.Provider.Transaction != null)
					{
						cmd = (SqliteCommand)cmd.Clone();
						cmd.Transaction = (SqliteTransaction)this.Provider.Transaction;
					}
				}
				else
				{
					cmd = (SqliteCommand)cmd.Clone();
					cmd.Transaction = (SqliteTransaction)this.Provider.Transaction;
					this.SetParameterValues(query, cmd, paramValues);
				}
				return cmd;
			}

			private void SetParameterValues(QueryCommand query, SqliteCommand command, object[] paramValues)
			{
				if (query.Parameters.Count > 0 && command.Parameters.Count == 0)
				{
					for (int i = 0, n = query.Parameters.Count; i < n; i++)
					{
						this.AddParameter(command, query.Parameters[i], paramValues != null ? paramValues[i] : null);
					}
				}
				else if (paramValues != null)
				{
					for (int i = 0, n = command.Parameters.Count; i < n; i++)
					{
						DbParameter p = command.Parameters[i];
						if (p.Direction == System.Data.ParameterDirection.Input
						    || p.Direction == System.Data.ParameterDirection.InputOutput)
						{
							p.Value = paramValues[i] ?? DBNull.Value;
						}
					}
				}
			}

			private void AddParameter(SqliteCommand command, QueryParameter parameter, object value)
			{
				DbQueryType qt = parameter.QueryType;
				if (qt == null)
				{
					qt = DbTypeSystem.GetColumnType(parameter.Type);
				}
				var p = ((SqliteCommand)command).Parameters.Add(parameter.Name, ((DbQueryType)qt).DbType, qt.Length);
				if (qt.Length != 0)
				{
					p.Size = qt.Length;
				}
				else if (qt.Scale != 0)
				{
					p.Size = qt.Scale;
				}
				p.Value = value ?? DBNull.Value;
			}

			private void GetParameterValues(SqliteCommand command, object[] paramValues)
			{
				if (paramValues != null)
				{
					for (int i = 0, n = command.Parameters.Count; i < n; i++)
					{
						if (command.Parameters[i].Direction != System.Data.ParameterDirection.Input)
						{
							object value = command.Parameters[i].Value;
							if (value == DBNull.Value)
							{
								value = null;
							}
							paramValues[i] = value;
						}
					}
				}
			}

			private void LogMessage(string message)
			{
				if (this.Provider.Log != null)
				{
					this.Provider.Log.WriteLine(message);
				}
			}

			/// <summary>
			/// Write a command & parameters to the log
			/// </summary>
			/// <param name="command"></param>
			/// <param name="paramValues"></param>
			private void LogCommand(QueryCommand command, object[] paramValues)
			{
				if (this.Provider.Log != null)
				{
					this.Provider.Log.WriteLine(command.CommandText);
					if (paramValues != null)
					{
						this.LogParameters(command, paramValues);
					}
					this.Provider.Log.WriteLine();
				}
			}

			private void LogParameters(QueryCommand command, object[] paramValues)
			{
				if (this.Provider.Log != null && paramValues != null)
				{
					for (int i = 0, n = command.Parameters.Count; i < n; i++)
					{
						var p = command.Parameters[i];
						var v = paramValues[i];

						if (v == null || v == DBNull.Value)
						{
							this.Provider.Log.WriteLine("-- {0} = NULL", p.Name);
						}
						else
						{
							this.Provider.Log.WriteLine("-- {0} = [{1}]", p.Name, v);
						}
					}
				}
			}
		}
	}
}