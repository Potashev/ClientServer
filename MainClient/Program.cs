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
            Console.WriteLine("\tClient!))");
            //Console.Write("Время генерации: ");
            //InputGenerationTime(out int time);

            //Console.Write("Время отправки: ");
            //string sendingTimeStr = Console.ReadLine();
            //if (sendingTimeStr == "") {
            //    sendingTimeStr = "0";
            //}
            //int sendingTime = int.Parse(sendingTimeStr);
            //Client terminal = new Client(Console.ReadLine, Console.WriteLine, time, 10000, sendingTime);
            Client terminal = new Client(Console.ReadLine, Console.WriteLine);
            terminal.Run();
        }

        static void InputGenerationTime(out int time) {
            time = 0;
            while (true) {
                if (int.TryParse(Console.ReadLine(), out int inputTime)) {
                    time = inputTime;
                    break;
                }
                else {
                    Console.Write("Неверный ввод, повторите попытку:");
                }
            }
        }
    }
}
