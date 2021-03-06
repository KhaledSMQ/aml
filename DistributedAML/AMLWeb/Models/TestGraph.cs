﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace AMLWeb.Models
{
    [Description("this is description of sub-object class")]
    public class SubObject
    {
        public enum Blah
        {
            Blah1,
            Blah2,
        }

        [Description("this is test2 field")]
        public String TEst2 { get; set; }

        [Description("this is blah field")]
        public Blah Foo { get; set; }
    }

    [Description("my top level query")]
    public class Query
    {
        public Query()
        {
            foo = new List<SubObject>();
            foo.Add(new SubObject { Foo = SubObject.Blah.Blah1, TEst2 = "hallo flldldl" });
            foo.Add(new SubObject { Foo = SubObject.Blah.Blah1, TEst2 = "hallo flldldl" });
            foo.Add(new SubObject { Foo = SubObject.Blah.Blah1, TEst2 = "hallo flldldl" });

            MySubObject = new SubObject();

        }

        [Description("my foo list object")]
        public List<SubObject> foo { get; set; }


        public SubObject MySubObject { get; set; }

        public SubObject NullSubObject { get; set; }


    }


}
