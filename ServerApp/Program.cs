using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;

namespace ServerProject
{
    class Program
    {
        static void Main(string[] args) {
            Console.WriteLine("\tServer");


            Console.WriteLine("Ввод топологии ?");
            bool createTopology = false;
            string str = Console.ReadLine();

            if (str == "+")
            {
                createTopology = true;
            }

            Server terminal = new Server(Console.ReadLine, Console.WriteLine, 660, createTopology);

            terminal.Run();
        }
    }
}
