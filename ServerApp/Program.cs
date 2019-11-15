using System;
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
            server.Start(createTopologyFlag);
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
