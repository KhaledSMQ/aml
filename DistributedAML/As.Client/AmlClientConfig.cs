﻿using System;
using System.Collections.Generic;

namespace As.Client
{
    public class Client
    {
        public String Name { get; set; }
        public String DataDirectory { get; set; }
    }

    public class MyClients
    {
        public MyClients()
        {
            Clients = new List<Client>();
        }
        public List<Client> Clients { get; set; }
    }
}
