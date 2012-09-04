// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace IQToolkit.Data
{
    using Common;
    using Mapping;

    using Mono.Data.Sqlite;

	public class DbEntityProvider : EntityProvider
    {
		Dictionary<QueryCommand, SqliteCommand> commandCache = new Dictionary<QueryCommand, SqliteCommand>();
		
		private readonly DbConnection connection;
        DbTransaction transaction;
        IsolationLevel isolation = IsolationLevel.ReadCommitted;

        int nConnectedActions = 0;

		public DbEntityProvider(DbConnection connection, QueryMapping mapping, EntityPolicy policy)
			: base(QueryLanguage.Default, mapping, policy)
        {
	        ActionOpenedConnection = false;
	        if (connection == null)
                throw new InvalidOperationException("Connection not specified");
            this.connection = connection;
        }

        public virtual DbConnection Connection
        {
            get { return this.connection; }
        }

        public virtual DbTransaction Transaction
        {
            get { return this.transaction; }
            set
            {
                if (value != null && value.Connection != this.connection)
                    throw new InvalidOperationException("Transaction does not match connection.");
                this.transaction = value;
            }
        }

        public IsolationLevel Isolation
        {
            get { return this.isolation; }
            set { this.isolation = value; }
        }

        public virtual DbEntityProvider New(DbConnection connection, QueryMapping mapping, EntityPolicy policy)
        {
			return new DbEntityProvider(connection, mapping, policy); 
        }

        public virtual DbEntityProvider New(DbConnection connection)
        {
            var n = New(connection, this.Mapping, this.Policy);
            n.Log = this.Log;
            return n;
        }

        public virtual DbEntityProvider New(QueryMapping mapping)
        {
            var n = New(this.Connection, mapping, this.Policy);
            n.Log = this.Log;
            return n;
        }

		public virtual DbEntityProvider New(EntityPolicy policy)
        {
            var n = New(this.Connection, this.Mapping, policy);
            n.Log = this.Log;
            return n;
        }

        public static DbEntityProvider FromApplicationSettings()
        {
            var provider = System.Configuration.ConfigurationSettings.AppSettings["Provider"];
            var connection = System.Configuration.ConfigurationSettings.AppSettings["Connection"];
            var mapping = System.Configuration.ConfigurationSettings.AppSettings["Mapping"];
            return From(provider, connection, mapping);
        }

        public static DbEntityProvider From(string connectionString, string mappingId)
        {
			return From(connectionString, mappingId, EntityPolicy.Default);
        }

        public static DbEntityProvider From(string connectionString, string mappingId, EntityPolicy policy)
        {
            return From(null, connectionString, mappingId, policy);
        }

        public static DbEntityProvider From(string connectionString, QueryMapping mapping, EntityPolicy policy)
        {
            return From((string)null, connectionString, mapping, policy);
        }

        public static DbEntityProvider From(string provider, string connectionString, string mappingId)
        {
            return From(provider, connectionString, mappingId, EntityPolicy.Default);
        }

        public static DbEntityProvider From(string provider, string connectionString, string mappingId, EntityPolicy policy)
        {
            return From(provider, connectionString, GetMapping(mappingId), policy);
        }

        public static DbEntityProvider From(string provider, string connectionString, QueryMapping mapping, EntityPolicy policy)
        {
			return From(typeof(DbEntityProvider), connectionString, mapping, policy);
        }

        public static DbEntityProvider From(Type providerType, string connectionString, QueryMapping mapping, EntityPolicy policy)
        {
			SqliteConnection connection = new SqliteConnection();

            // is the connection string just a filename?
			if (!connectionString.Contains('='))
			{
				connectionString = "Data Source=" + connectionString;
			}
			
	        connection.ConnectionString = connectionString;

            return (DbEntityProvider)Activator.CreateInstance(providerType, new object[] { connection, mapping, policy });
        }

		protected bool ActionOpenedConnection { get; private set; }

		protected void StartUsingConnection()
        {
            if (this.connection.State == ConnectionState.Closed)
            {
                this.connection.Open();
                this.ActionOpenedConnection = true;
            }
            this.nConnectedActions++;
        }

        protected void StopUsingConnection()
        {
            System.Diagnostics.Debug.Assert(this.nConnectedActions > 0);
            this.nConnectedActions--;
            if (this.nConnectedActions == 0 && this.ActionOpenedConnection)
            {
                this.connection.Close();
                this.ActionOpenedConnection = false;
            }
        }

        public override void DoConnected(Action action)
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

        public override void DoTransacted(Action action)
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

        public override int ExecuteCommand(string commandText)
        {
            if (this.Log != null)
            {
                this.Log.WriteLine(commandText);
            }
            this.StartUsingConnection();
            try
            {
                DbCommand cmd = this.Connection.CreateCommand();
                cmd.CommandText = commandText;
                return cmd.ExecuteNonQuery();
            }
            finally
            {
                this.StopUsingConnection();
            }
        }

		public override QueryExecutor CreateExecutor()
        {
            return new Executor(this);
        }

        public class Executor : QueryExecutor
        {
			public Executor(DbEntityProvider provider)
			{
				this.Provider = provider;
			}

	        int rowsAffected;

	        public DbEntityProvider Provider { get; private set; }

	        public override int RowsAffected
            {
                get { return this.rowsAffected; }
            }

            protected virtual bool BufferResultRows
            {
                get { return false; }
            }

            protected bool ActionOpenedConnection
            {
                get { return this.Provider.ActionOpenedConnection; }
            }

            protected void StartUsingConnection()
            {
                this.Provider.StartUsingConnection();
            }

            protected void StopUsingConnection()
            {
                this.Provider.StopUsingConnection();
            }

            public override object Convert(object value, Type type)
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
                            return Enum.Parse(type, (string)value);
                        }
                        else
                        {
                            Type utype = Enum.GetUnderlyingType(type);
                            if (utype != vtype)
                            {
                                value = System.Convert.ChangeType(value, utype);
                            }
                            return Enum.ToObject(type, value);
                        }
                    }
                    return System.Convert.ChangeType(value, type);
                }
                return value;
            }

            public override IEnumerable<T> Execute<T>(QueryCommand command, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues)
            {
                this.LogCommand(command, paramValues);
                this.StartUsingConnection();
                try
                {
                    DbCommand cmd = this.GetCommand(command, paramValues);
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

            protected virtual DbDataReader ExecuteReader(DbCommand command)
            {
                var reader = command.ExecuteReader();
                if (this.BufferResultRows)
                {
                    // use data table to buffer results
                    var ds = new DataSet();
                    ds.EnforceConstraints = false;
                    var table = new DataTable();
                    ds.Tables.Add(table);
                    ds.EnforceConstraints = false;
                    table.Load(reader);
                    reader = table.CreateDataReader();
                }
                return reader;
            }

            protected virtual IEnumerable<T> Project<T>(DbDataReader reader, Func<FieldReader, T> fnProjector, MappingEntity entity, bool closeReader)
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

            public override int ExecuteCommand(QueryCommand query, object[] paramValues)
            {
                this.LogCommand(query, paramValues);
                this.StartUsingConnection();
                try
                {
                    DbCommand cmd = this.GetCommand(query, paramValues);
                    this.rowsAffected = cmd.ExecuteNonQuery();
                    return this.rowsAffected;
                }
                finally
                {
                    this.StopUsingConnection();
                }
            }

            public override IEnumerable<int> ExecuteBatch(QueryCommand query, IEnumerable<object[]> paramSets, int batchSize, bool stream)
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
                DbCommand cmd = this.GetCommand(query, null);
                foreach (var paramValues in paramSets)
                {
                    this.LogParameters(query, paramValues);
                    this.LogMessage("");
                    this.SetParameterValues(query, cmd, paramValues);
                    this.rowsAffected = cmd.ExecuteNonQuery();
                    yield return this.rowsAffected;
                }
            }

            public override IEnumerable<T> ExecuteBatch<T>(QueryCommand query, IEnumerable<object[]> paramSets, Func<FieldReader, T> fnProjector, MappingEntity entity, int batchSize, bool stream)
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

            private IEnumerable<T> ExecuteBatch<T>(QueryCommand query, IEnumerable<object[]> paramSets, Func<FieldReader, T> fnProjector, MappingEntity entity)
            {
                this.LogCommand(query, null);
                DbCommand cmd = this.GetCommand(query, null);
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

            public override IEnumerable<T> ExecuteDeferred<T>(QueryCommand query, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues)
            {
                this.LogCommand(query, paramValues);
                this.StartUsingConnection();
                try
                {
                    DbCommand cmd = this.GetCommand(query, paramValues);
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
            protected virtual DbCommand GetCommand(QueryCommand query, object[] paramValues)
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

            protected virtual void SetParameterValues(QueryCommand query, DbCommand command, object[] paramValues)
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

            protected virtual void AddParameter(DbCommand command, QueryParameter parameter, object value)
            {
				DbQueryType qt = parameter.QueryType;
				if (qt == null)
					qt = DbTypeSystem.GetColumnType(parameter.Type);
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

            protected virtual void GetParameterValues(DbCommand command, object[] paramValues)
            {
                if (paramValues != null)
                {
                    for (int i = 0, n = command.Parameters.Count; i < n; i++)
                    {
                        if (command.Parameters[i].Direction != System.Data.ParameterDirection.Input)
                        {
                            object value = command.Parameters[i].Value;
                            if (value == DBNull.Value)
                                value = null;
                            paramValues[i] = value;
                        }
                    }
                }
            }

            protected virtual void LogMessage(string message)
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
            protected virtual void LogCommand(QueryCommand command, object[] paramValues)
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

            protected virtual void LogParameters(QueryCommand command, object[] paramValues)
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
