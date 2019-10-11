using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientProject
{
    class Program
    {

        static void Main(string[] args) {
            Console.WriteLine("\tClient");

            Client terminal = new Client(Console.ReadLine, Console.WriteLine);
            terminal.Run();
        }
    }
}
