﻿using System;
using System.Collections.Generic;
using System.Linq;
using As.Shared;
using Microsoft.AspNetCore.Http;

namespace App4Answers.Models.A4Amodels.Base
{
    public class ViewModelBase : IViewModel
    {
        protected ViewModelBase()
        {
            typeContainer = TypeContainer.GetTypeContainer(GetType());
        }
        public IEnumerable<String> GetColumnNames()
        {
            return typeContainer.Properties.Select(x => x.pi.Name);
        }

        public int GetFieldCount()
        {
            return typeContainer.Properties.Count();
        }

        protected TypeContainer typeContainer;

        protected TypeContainer modelTypeContainer;

        public String PrimaryKeyName { get; set; }



        public IEnumerable<ViewModelRow> GetRows(int numColumns)
        {
            List<ViewModelRow> rows = new List<ViewModelRow>();
            int cnt = 0;
            foreach (var row in typeContainer.Properties.GroupBy(x => x.index / numColumns))
            {
                rows.Add(new ViewModelRow(this, cnt++, row.ToList()));
            }
            return rows;
        }

        public virtual String GetPrimaryKey()
        {
            return (String)GetValue(PrimaryKeyName);
        }

        public Object GetValue(string name)
        {
            return typeContainer.GetProperty(name).GetValue(this);
        }

        public void SetValue(string name, Object val)
        {
            typeContainer.GetProperty(name).SetValue(this, val);

        }

        public IEnumerable<String> GetForeignKeyValues(string name)
        {
            foreach (var c in foreignKeys)
            {
                if (c.foreignKey.ChildFieldName == name)
                    return c.values;
            }
            return Enumerable.Empty<string>();
        }

        private IEnumerable<(ForeignKey foreignKey, IEnumerable<string> values)> foreignKeys = null;
        public T AddForeignKeys<T>(IEnumerable<(ForeignKey foreignKey, IEnumerable<string> values)> foreignKeys) where T:ViewModelBase
        {
            this.foreignKeys = foreignKeys;
            return (T)this;
        }

      


        public ModelNames.ObjectTypes ObjectTypes { get; set; }
        public ModelNames.Verb Verb { get; set; }
        public ModelNames.ActionNames ActionNames { get; set; }
    }

    public class ViewModelBase<T> : ViewModelBase
    {

        protected ViewModelBase()
        {
            modelTypeContainer = TypeContainer.GetTypeContainer(typeof(T));

            // figure out our primary key based on model PK
            var pkName = modelTypeContainer.Properties.FirstOrDefault(x => x.IsPrimaryKey);
            if (pkName == null)
                throw new Exception($"Primary key not found on model table - {modelTypeContainer.Name}");

            var mypk = typeContainer.Properties.FirstOrDefault(x => x.Name == pkName.Name);

            if (mypk == null)
                throw new Exception(
                    $"Primary key not found on view-model table - {typeContainer.Name} - view-model requires same PK as underlying model - {pkName.Name}");

            mypk.IsPrimaryKey = true;

            PrimaryKeyName = mypk.Name;

        }

        protected ViewModelBase(T modelSource) :this()
        {
            var tc = TypeContainer.GetTypeContainer(typeof(T));
            foreach (var c in typeContainer.Properties)
            {
                var modelField = tc.GetProperty(c.Name);
                if (modelField == null)
                    throw new Exception(
                        $"Model field - {c.Name} - from ViewModel - {this.typeContainer.UnderlyingType.Name} - is not found on type of object - {modelSource.GetType()}");
                c.SetValue(this,modelField.GetValue(modelSource));
            }
        }

        protected ViewModelBase(IFormCollection form) : this()
        {
            foreach (var c in typeContainer.Properties)
            {
                if (form.ContainsKey(c.Name))
                    c.SetValue(this, form[c.Name].ToString());
            }
        }

        public T ModelClassFromViewModel()
        {
            var newType = TypeContainer.GetTypeContainer(typeof(T));
            T newOne = newType.CreateInstance<T>();

            foreach (var c in typeContainer.ProtobufProperties)
            {
                var modelProperty = newType.GetProperty(c.Name);
                if (modelProperty == null)
                    throw new Exception($"Viewmodel property - {c.Name} is not found on model - {typeof(T).Name}.");

                modelProperty.SetValue(newOne,c.GetValue(this));
            }
            return newOne;
        }









    }
}
