using System;
using System.Threading;
using ClientServerLib;

namespace ClientProject
{
    class Program
    {

        static void Main(string[] args) {
            Console.WriteLine("\tClientApp");

            Console.WriteLine("Введите время генерации пакета данных (мс):");
            int generationTime = InputTimeValue();
            Console.WriteLine("Введите периодичность проверки узлов топологии, \nсоединение с которыми было потеряно (мс):");
            int checkLostNodeTime = InputTimeValue();

            var client = new Client(Console.ReadLine, Console.WriteLine, checkLostNodeTime);
            client.StartAsync();

            while (true) {
                GenerationData(generationTime, out int data);
                client.AddNewData(data);
            }
        }

        static void GenerationData(int generationTime, out int datavalue) {
            Random rand = new Random();
            Thread.Sleep(generationTime);
            datavalue = rand.Next(1, 1000);
        }

        static int InputTimeValue() {
            while (true) {
                if (int.TryParse(Console.ReadLine(), out int inputValue)&&(inputValue>0)) {
                    return inputValue;
                }
                else {
                    Console.WriteLine("Неверный ввод, повторите попытку.");
                }
            }
            
        }
    }
}
