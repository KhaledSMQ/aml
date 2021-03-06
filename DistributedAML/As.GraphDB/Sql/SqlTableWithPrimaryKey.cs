﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Tree.Xpath;
using As.Logger;
using As.Shared;
using Microsoft.Data.Sqlite;

namespace As.GraphDB.Sql
{
    public  class SqlTableWithPrimaryKey : SqlTableBase
    {
        private SqlitePropertiesAndCommands propertiesAndCommands;
        public SqlTableWithPrimaryKey(SqlitePropertiesAndCommands propertiesAndCommands):base(propertiesAndCommands.tableName)
        {
            this.propertiesAndCommands = propertiesAndCommands;

        }


        private string prefix = "";
        public SqlTableWithPrimaryKey SetUniqueIdPrefix(string s)
        {
            prefix = s;
            return this;

        }

        public SqlitePropertiesAndCommands PropertiesAndCommands => propertiesAndCommands;

        public string GetUniqueId(SqliteConnection conn,string prefix)
        {
            using (var cmd = conn.CreateCommand())
            {
                if (String.IsNullOrEmpty(prefix))
                    cmd.CommandText = $"select hex(randomblob(8));";
                else
                    cmd.CommandText = $"select  {prefix} || hex(randomblob(8));";
                var z = cmd.ExecuteScalar();
                return (string) z;
            }
            
        }

        public Object GetPrimaryKey(Object o)
        {
            return PropertiesAndCommands.GetPrimaryKey(o);
        }


        public  bool TableExists<T>(SqliteConnection conn)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type = 'table' AND name = '{TableName}';";
                using (var rdr = cmd.ExecuteReader())
                {
                    return rdr.HasRows;
                }
            }
        }

        public int GetCount(SqliteConnection conn)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT count(*) from {TableName};";

                var foo = cmd.ExecuteScalar();

                return Convert.ToInt32(foo);
            }
        }

        public int GetCount(SqliteConnection conn,string whereClause)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT count(*) from {TableName} where {whereClause};";

                var foo = cmd.ExecuteScalar();

                return Convert.ToInt32(foo);
            }
        }

        String whereClauseFromPredicate(SqlPredicate[] foo)
        {
            StringBuilder b = new StringBuilder();
            foreach (var c in foo)
            {
                b.Append($"{c.propertyName} like '{c.propertyValue}'");
                b.Append(" and ");
            }

            b.Remove(b.Length - " and ".Length, " and ".Length);
            return b.ToString();
        }


        public int GetCount(SqliteConnection conn, params SqlPredicate[] p)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT count(*) from {TableName} where {whereClauseFromPredicate(p)};";

                var foo = cmd.ExecuteScalar();

                return Convert.ToInt32(foo);
            }
        }



        public int CreateTable(SqliteConnection conn)
        {
            return ExecuteCommandLog(conn, propertiesAndCommands.CreateTableCommand());
        }

        public  void UpdateTableStructure(SqliteConnection conn)
        {
            List<(string,string)> columns = new List<(string, string)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info(\"{propertiesAndCommands.tableName}\");";
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var name = (string) rdr["name"];
                        var type = (string) rdr["type"];
                        columns.Add((name,type));
                    }
                    
                }
            }

            foreach (var c in propertiesAndCommands.SqlFields())
            {
                if (columns.All(x => x.Item1 != c.Name))
                { 
                    if (SqlitePropertiesAndCommands.ConvertPropertyType(c.PropertyType) == "text")
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            L.Trace($"Adding new column - {c.Name} to table {propertiesAndCommands.tableName}");
                            cmd.CommandText = propertiesAndCommands.AddColumnCommand(c.Name, ColumnType.String);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            L.Trace($"Updating value of {c.Name} in table {propertiesAndCommands.tableName}");
                            string value = c.PropertyType.IsEnum ? Enum.GetNames(c.PropertyType)[0] : "";
                            cmd.CommandText = propertiesAndCommands.UpdateColumnValuesCommandStr(c.Name, value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else if (SqlitePropertiesAndCommands.ConvertPropertyType(c.PropertyType) == "numeric")
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            L.Trace($"Adding new column - {c.Name} to table {propertiesAndCommands.tableName}");
                            cmd.CommandText = propertiesAndCommands.AddColumnCommand(c.Name, ColumnType.Numeric);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            L.Trace($"Updating value of {c.Name} in table {propertiesAndCommands.tableName}");
                            cmd.CommandText = propertiesAndCommands.UpdateColumnValuesCommandNumeric(c.Name, 0);
                            cmd.ExecuteNonQuery();
                        }

                    }
                    else
                    {
                        throw new Exception($"Unexpected proeprty type to create new Sqlite column: {c.PropertyType} - name of field - {c.Name}");
                    }

                }
            }            
        }
        public int InsertOrReplace<T>(SqliteConnection connection, IEnumerable<T> objs,bool orReplace=false)
        {
            L.Trace($"Starting insert rows - {objs.Count()} objects on {propertiesAndCommands.tableName}");
            int cnt = 0;

            String insertCommand = propertiesAndCommands.InsertOrReplaceCommand(orReplace);

            L.Trace(insertCommand);

            var txn = connection.BeginTransaction();
            {
                foreach (var c in objs)
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = insertCommand;
                        foreach (var g in propertiesAndCommands.SqlFields())
                        {
                            // get new id on primary key unless we are replacing
                            if (g.Underlying.IsPrimaryKey)
                            {
                                var pk = (string) g.GetValue(c);
                                if (String.IsNullOrEmpty((string) pk))
                                {
                                    g.SetValue(c, GetUniqueId(connection, prefix));
                                }
                            }

                            var toset = g.GetValue(c);

                            if (g.Underlying.IsForeignKey && String.IsNullOrEmpty((string)toset))
                            {
                                toset = DBNull.Value;
                            }

                            cmd.Parameters.AddWithValue(g.Underlying.Name, toset);
                        }

                        try
                        {
                            var res = cmd.ExecuteNonQuery();
                            cnt++;
                        }
                        catch (Exception e)
                        {
                            L.Trace(e.Message);
                            L.Trace(cmd.CommandText);
                        }

                        if (cnt % 100000 == 0)
                        {
                            L.Trace($"{cnt} objects committed on {propertiesAndCommands.tableName}");

                            txn.Commit();
                            txn = connection.BeginTransaction();
                        }
                    }
                }
            }
            txn.Commit();
            L.Trace($"{cnt} objects finished on {propertiesAndCommands.tableName}");
            return cnt;
        }


        public T SelectOne<T>(SqliteConnection connection, string propertyName, Object value,bool assertEmpty=true,bool assertMultiple=true)
        {
            using (var queryCmd = connection.CreateCommand())
            {
                if (value is string || value is Enum)
                    queryCmd.CommandText =
                        $"select * from {propertiesAndCommands.tableName} where {propertyName} = '{value.ToString()}'";
                else
                    queryCmd.CommandText =
                        $"select * from {propertiesAndCommands.tableName} where {propertyName} = {value}";

                using (var data = queryCmd.ExecuteReader())
                {
                    var p = new DataRecordHelper<T>(propertiesAndCommands, data);
                    if (data.Read())
                    {
                        var obj = p.GetObject();

                        if (data.Read() && assertMultiple)
                            throw new Exception(
                                $"{propertiesAndCommands.tableName} has more than one entry where {propertyName} equals {value}");

                        return obj;
                    }
                    else
                    {
                        if (assertEmpty)
                            throw new Exception(
                                $"{propertiesAndCommands.tableName} does not contain entry where {propertyName} equals {value}");
                        return default(T);
                    }
                }
            }

        }



        public  IEnumerable<DataRecordHelper<T>> SelectData<T>(SqliteConnection connection,String whereClause,Range range=null,Sort sort=null)
        {
            string selectCommand;
            if (String.IsNullOrEmpty(whereClause))
                selectCommand = $"{propertiesAndCommands.SelectCommand()} where {propertiesAndCommands.RangeClause(range)} {propertiesAndCommands.SortClause(sort)};";
            else
                selectCommand = $"{propertiesAndCommands.SelectCommand()} where {whereClause} and {propertiesAndCommands.RangeClause(range)} {propertiesAndCommands.SortClause(sort)};";

            using (var queryCmd = connection.CreateCommand())
            {
                queryCmd.CommandText = selectCommand;

                using (var data = queryCmd.ExecuteReader())
                {
                    var p = new DataRecordHelper<T>(propertiesAndCommands,data);

                    while (data.Read())
                    {
                        yield return p.IncrementCount(); 
                    }
                }

            }
        }

        public IEnumerable<string> SelectPrimaryKeyValues(SqliteConnection conn)
        {
            string selectCommand = $"{propertiesAndCommands.SelectPrimaryKeyValues()}";

            using (var queryCmd = conn.CreateCommand())
            {
                queryCmd.CommandText = selectCommand;
                using (var data = queryCmd.ExecuteReader())
                {
                    while (data.Read())
                    {
                        yield return (string)data[0];
                    }
                }
            }
        }

        public  int Delete<T>(SqliteConnection connection,String id)
        {
            using (var txn = connection.BeginTransaction())
            {
                using (var deleteCmd = connection.CreateCommand())
                {
                    deleteCmd.CommandText = propertiesAndCommands.DeleteCommand(id);
                    deleteCmd.ExecuteNonQuery();
                }
                txn.Commit();
            }
            return 1;

        }

        public Object SelectDataByPrimaryKey(SqliteConnection connection, String primaryKey)
        {
            string selectCommand = $"{propertiesAndCommands.SelectCommandByPrimaryKey(primaryKey)}";
            using (var queryCmd = connection.CreateCommand())
            {
                queryCmd.CommandText = selectCommand;

                using (var data = queryCmd.ExecuteReader())
                {
                    var p = new DataRecordHelper(propertiesAndCommands, data);

                    if (data.Read())
                    {
                        var z = new DataRecordHelper(propertiesAndCommands, data);
                        return z.GetObject();
                    }
                }

            }

            return null;
        }

        public  T SelectDataByPrimaryKey<T>(SqliteConnection connection, Object primaryKey)
        {
            string selectCommand = $"{propertiesAndCommands.SelectCommandByPrimaryKey(primaryKey)}";
            using (var queryCmd = connection.CreateCommand())
            {
                queryCmd.CommandText = selectCommand;

                using (var data = queryCmd.ExecuteReader())
                {
                    var p = new DataRecordHelper<T>(propertiesAndCommands, data);

                    if (data.Read())
                    {
                        var z = new DataRecordHelper<T>(propertiesAndCommands, data);
                        return z.GetObject();
                    }
                }

            }
            return default(T);
        }

        public  IEnumerable<(string, bool)> QueryPrimaryKey<T>(SqliteConnection connection, IEnumerable<string> primaryKeys)
        {
            using (var txn = connection.BeginTransaction())
            {
                String queryCommand = propertiesAndCommands.QueryIdCommand();

                foreach (var c in primaryKeys)
                {
                    using (var queryCmd = connection.CreateCommand())
                    {
                        queryCmd.CommandText = queryCommand;

                        queryCmd.Parameters.AddWithValue("$primaryKey", c);
                        int cnt = Convert.ToInt32(queryCmd.ExecuteScalar());
                        yield return (c, cnt > 0);
                    }
                }
                txn.Commit();
            }
        }
    }
}
