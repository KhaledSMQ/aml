﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AMLWorker.Sql;
using As.GraphQL;
using As.GraphQL.Interface;
using Fasterflect;
using Google.Protobuf;
using GraphQL;
using Microsoft.Data.Sqlite;

namespace AMLWorker
{
    public interface IA4ARepositoryQuery
    {
        A4AMessage GetMessageById(String id);
        IEnumerable<A4AMessage> SearchMessages(String search,Range range, Sort sort );
        IEnumerable<A4ACategory> Categories { get; set; }
        IEnumerable<A4AParty> Parties { get; set; }

    }

    public interface IA4AMutations
    {
        A4AMessage AddMessage(A4AMessage msg);
    }

    public class A4ARepositoryGraphDb : RepositoryGraphDbBase
    {
        private SqlitePropertiesAndCommands<A4AParty> partySql = new SqlitePropertiesAndCommands<A4AParty>();
        private SqlitePropertiesAndCommands<A4ACategory> categorySql = new SqlitePropertiesAndCommands<A4ACategory>();
        private SqlitePropertiesAndCommands<A4AMessage> messageSql = new SqlitePropertiesAndCommands<A4AMessage>();


        public A4ARepositoryGraphDb(SqliteConnection conn,
            SqlitePropertiesAndCommands<A4AParty> partySql,
            SqlitePropertiesAndCommands<A4ACategory> categorySql,
            SqlitePropertiesAndCommands<A4AMessage> messageSql) : base(conn,typeof(IA4ARepositoryQuery),typeof(IA4AMutations))
        {
            this.partySql = partySql;
            this.categorySql = categorySql;
            this.messageSql = messageSql;
        }

   

        public override bool SupportField(object parentObject, string fieldName)
        {
            if (parentObject is A4ARepositoryQuery && fieldName == "Messages" || fieldName == "Categories" ||
                fieldName == "Parties")
                return true;
            return false;
        }

        public override IEnumerable<object> ResolveFieldValue(object parentObject, string fieldName,
            Dictionary<string, object> argumentValues)
        {
            if (parentObject is AmlRepositoryQuery && fieldName == "Parties")
            {
                foreach (var c in SqlTableHelper.SelectData(conn, partySql, GetRange(argumentValues),
                    GetSort(argumentValues)))
                {
                    yield return c;
                }
            }
        }
    }
}