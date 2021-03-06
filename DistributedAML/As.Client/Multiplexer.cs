﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using As.Comms;
using As.Shared;
using CsvHelper;

namespace As.Client
{
    public class Multiplexer
    {
        Dictionary<int,List<Object>> objs = new Dictionary<int, List<Object>>();
        private int partitions;
        public Multiplexer(int partitions)
        {
            if (partitions <= 0)
                throw new Exception($"Number of partitions must be greater than zero");
            this.partitions = partitions;

        }

        public static void FromCsv<T>(string file, int buckets, Func<T, string> onAddMultiplexer,
            Action<(int, IEnumerable<T>)> onFinished)
        {
            CsvReader rdr = new CsvReader(new StreamReader(file));

            rdr.Configuration.HeaderValidated = null;

            rdr.Configuration.MissingFieldFound = null;

            var mp = new Multiplexer(buckets);

            rdr.GetRecords<T>().Do(x => mp.Add(onAddMultiplexer(x), x));

            foreach (var g in mp.GetPartitions<T>())
            {
                onFinished(g);
            }
        }

        public static void FromCsvMultiplePartitions<T>(string file, int buckets, Func<T, IEnumerable<string>> onAddMultiplexer,
            Action<(int, IEnumerable<T>)> onFinished)
        {
            CsvReader rdr = new CsvReader(new StreamReader(file));

            rdr.Configuration.HeaderValidated = null;

            rdr.Configuration.MissingFieldFound = null;

            var mp = new Multiplexer(buckets);

            rdr.GetRecords<T>().Do(x => mp.Add(onAddMultiplexer(x), x));

            foreach (var g in mp.GetPartitions<T>())
            {
                onFinished(g);
            }
        }


        public static void FromList<T>(IEnumerable<T> o, int paritions, Func<T, string> onAddMultiplexer,
            Action<(int, IEnumerable<T>)> onFinished)
        {
            var mp = new Multiplexer(paritions);
            foreach (var z in o)
            {
                mp.Add(onAddMultiplexer(z),z);
            }

            foreach (var g in mp.GetPartitions<T>())
            {
                onFinished(g);
            }
        }


        public static void FromListMultipleBuckets<T>(IEnumerable<T> o, int buckets, Func<T, IEnumerable<string>> onAddMultiplexer,
            Action<(int, IEnumerable<T>)> onFinished)
        {
            var mp = new Multiplexer(buckets);
            foreach (var z in o)
            {
                mp.Add(onAddMultiplexer(z), z);
            }

            foreach (var g in mp.GetPartitions<T>())
            {
                onFinished(g);
            }
        }

        public void AddList<T>(IEnumerable<T> lst, Func<T, IEnumerable<string>> onAddMultiplexer)
        {
            foreach (var z in lst)
            {
                Add(onAddMultiplexer(z),z);
            }
        }


        public void Add(String key, Object o)
        {
            var bucket = Math.Abs(MurMurHash3.Hash(new MemoryStream(Encoding.UTF8.GetBytes(key)))) % partitions;
            List<Object> vals = null;
            if (!objs.TryGetValue(bucket,out vals))
                objs[bucket] = vals = new List<Object>();
            vals.Add(o);
        }

        private void Add(IEnumerable<String> key, Object o)
        {
            var buckets = new List<int>();
            foreach (var z in key)
            {
                var bucket = Math.Abs(MurMurHash3.Hash(new MemoryStream(Encoding.UTF8.GetBytes(z)))) % partitions;
                if (buckets.Contains(bucket) == false)
                    buckets.Add(bucket);
            }
            foreach (var z in buckets)
            {
                List<Object> vals = null;
                if (!objs.TryGetValue(z, out vals))
                    objs[z] = vals = new List<Object>();
                vals.Add(o);
            }
        }

        public  IEnumerable<(int, IEnumerable<T>)> GetPartitions<T>()
        {
            foreach (var c in objs.Keys)
            {
                yield return (c,objs[c].Cast<T>());
            }            
        }
    }
}
