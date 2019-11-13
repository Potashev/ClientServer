using System;
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

            //TODO: вводить IP сервера вручную

            var client = new Client(Console.ReadLine, Console.WriteLine, generationTime, checkLostNodeTime);
            client.Run();
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
