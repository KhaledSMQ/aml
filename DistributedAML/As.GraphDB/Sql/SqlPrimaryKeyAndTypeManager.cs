﻿using System;
using System.Collections.Generic;
using System.Text;

namespace As.GraphDB.Sql
{
    public class SqlPrimaryKeyAndTypeManager
    {
        public SqlPrimaryKeyAndTypeManager()
        {

        }

        Dictionary<Type,(string,Enum)> prefixMap = new Dictionary<Type, (string,Enum)>();
        Dictionary<string,(Type,Enum)> typeMap = new Dictionary<string, (Type, Enum)>();

        public SqlPrimaryKeyAndTypeManager AddPrimaryKeyPrefixAndEnum(Type t, string prefix,Enum foo)
        {
            prefixMap[t] = (prefix, foo);

            typeMap[prefix] = (t, foo);
            return this;
        }

        public Type GetTypeFromId(string id)
        {
            foreach (var c in typeMap.Keys)
            {
                if (id.StartsWith(c))
                    return typeMap[c].Item1;
            }
            throw new Exception($"Type for id - {id} not found.");
        }

        public T GetEnumFromId<T>(string id) 
        {
            foreach (var c in typeMap.Keys)
            {
                if (id.StartsWith(c))
                    return (T)(object) (typeMap[c].Item2);
            }
            throw new Exception($"Type for id - {id} not found.");
        }



    }
}
