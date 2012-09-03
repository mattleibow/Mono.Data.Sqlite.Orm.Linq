namespace IQToolkit.Data
{
	using System;
	using System.Collections.Generic;
	using System.Data.Common;

	using Mono.Data.Sqlite;

	using IQToolkit.Data.Common;

	public class SQLiteQueryProvider : DbEntityProvider
    {
        Dictionary<QueryCommand, SqliteCommand> commandCache = new Dictionary<QueryCommand, SqliteCommand>();

		public SQLiteQueryProvider(SqliteConnection connection, QueryMapping mapping, EntityPolicy policy)
            : base(connection, QueryLanguage.Default, mapping, policy)
        {
        }

        public static string GetConnectionString(string databaseFile)
        {
            return string.Format("Data Source={0};", databaseFile);
        }

        public static string GetConnectionString(string databaseFile, string password)
        {
            return string.Format("Data Source={0};Password={1};", databaseFile, password);
        }

        public static string GetConnectionString(string databaseFile, bool failIfMissing)
        {
            return string.Format("Data Source={0};FailIfMissing={1};", databaseFile, failIfMissing ? bool.TrueString : bool.FalseString);
        }

        public static string GetConnectionString(string databaseFile, string password, bool failIfMissing)
        {
            return string.Format("Data Source={0};Password={1};FailIfMissing={2};", databaseFile, password, failIfMissing ? bool.TrueString : bool.FalseString);
        }

		public override DbEntityProvider New(DbConnection connection, QueryMapping mapping, EntityPolicy policy)
        {
            return new SQLiteQueryProvider((SqliteConnection)connection, mapping, policy);
        }

        protected override QueryExecutor CreateExecutor()
        {
            return new Executor(this);
        }

        new class Executor : DbEntityProvider.Executor
        {
            SQLiteQueryProvider provider;

            public Executor(SQLiteQueryProvider provider)
                : base(provider)
            {
                this.provider = provider;
            }

            protected override DbCommand GetCommand(QueryCommand query, object[] paramValues)
            {
                SqliteCommand cmd;
                if (!this.provider.commandCache.TryGetValue(query, out cmd))
                {
                    cmd = (SqliteCommand)this.provider.Connection.CreateCommand();
                    cmd.CommandText = query.CommandText;
                    this.SetParameterValues(query, cmd, paramValues);
                    cmd.Prepare();
                    this.provider.commandCache.Add(query, cmd);
                    if (this.provider.Transaction != null)
                    {
                        cmd = (SqliteCommand)cmd.Clone();
                        cmd.Transaction = (SqliteTransaction)this.provider.Transaction;
                    }
                }
                else
                {
                    cmd = (SqliteCommand)cmd.Clone();
                    cmd.Transaction = (SqliteTransaction)this.provider.Transaction;
                    this.SetParameterValues(query, cmd, paramValues);
                }
                return cmd;
            }

            protected override void AddParameter(DbCommand command, QueryParameter parameter, object value)
            {
				DbQueryType qt = parameter.QueryType;
                if (qt == null)
                    qt = this.provider.Language.TypeSystem.GetColumnType(parameter.Type);
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
        }
    }
}
