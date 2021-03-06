﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using As.Client;
using As.Client.AS.Application;
using As.Comms;
using CsvHelper;
using As.Shared;

namespace AmlClient.Commands
{
    public class LoadFuzzyNames : AmlCommand
    {
        private IClientFactory factory;
        private MyRegistry reg;
        private String DataFile;
        private ClientServicePartitionValidator validator;
        public LoadFuzzyNames(ClientServicePartitionValidator validator,IClientFactory factory,MyRegistry reg)
        {
            this.factory = factory;
            this.validator = validator;
            this.reg = reg;
            Console.WriteLine($"Enter file name from '{reg.DataDirectory}\\input' or 'y' to use default ('Retail-Large.csv')");
            var s = Console.ReadLine().ToLower();
            if (s == "y")
                DataFile = $"{reg.DataDirectory}\\input\\Retail-Large.csv";
            else
            {
                DataFile = $"{reg.DataDirectory}\\input\\s";
            }

            if (File.Exists(DataFile) == false)
                throw new Exception($"File does not exist - {DataFile}");
        }
        public override void Run()
        {
            validator.ValidateServiceBucketIsConsistent(typeof(IFuzzyMatcher));

            var maxBuckets = factory.GetClientBuckets<IFuzzyMatcher>().Max();

            var multiplexer = new Multiplexer(maxBuckets + 1 /*number of buckets*/);

            CsvReader rdr = new CsvReader(new StreamReader(DataFile));

            var records = rdr.GetRecords<Party>();
            records.Take(1000000)
                .Do(x => multiplexer.Add(x.Name,
                    new FuzzyWordEntry
                    {
                        DocId = Int32.Parse(x.Id),
                        Phrase = x.Name
                    }));

            List<Task> tasks = new List<Task>();
            multiplexer.GetPartitions<FuzzyWordEntry>().Do(x =>
            {
                tasks.Add(new Task(() => factory.GetClient<IFuzzyMatcher>(x.Item1).AddEntry(x.Item2)));
                tasks.Last().Start();
            });

            Task.WaitAll(tasks.ToArray());
        }
    }
}
