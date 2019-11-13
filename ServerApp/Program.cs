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
using ClientServerLib;

namespace ServerProject
{
    class Program
    {
        static void Main(string[] args) {
            Console.WriteLine("\tServerApp");


            Console.WriteLine("Запустить сервер в режиме формирования топологии?");
            Console.WriteLine("+ - Да, иное - Нет");
            bool createTopologyFlag = DefineTopologyFlag();
            int serverAcceptPort = 660;

            var server = new Server(Console.ReadLine, Console.WriteLine, serverAcceptPort);
            server.Run(createTopologyFlag);
        }

        static bool DefineTopologyFlag() {
            string input = Console.ReadLine();

            if (input == "+") {
                return true;
            }
            else {
                return false;
            }
        }
    }
}
