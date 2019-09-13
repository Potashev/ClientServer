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

namespace ServerProject {
    public class Server {

        const string SERVER_IP = "192.168.1.106";   // TODO: брать ip терминала либо методом, либо вводить при запуске

        static object locker = new object();    // TODO: попробовать перенести locker в SendNeibs

        // списох узлов, которые подключаются по топологии
        List<ConnectionInfo> Connections = new List<ConnectionInfo>();
        // список всех узлов, подключившихся к серву для топологии
        //static List<Client> Clients = new List<Client>();// TODO: разобраться, почему требует static для списка
        // список узлов-соседей, который отправляется каждому клиенту по топологии (у каждого клиента свой список)
        //List<Unit> Neibs = new List<Unit>();

        public void Run(bool createTopology) {

            if (createTopology) {


                ConnectAllForTopology();

                //AcceptForTopology();
                //CreateTopology();

            }
            //Console.WriteLine("Прием пакетов");
            // прослушку запускаю раньше топологии, тк она отправляется не всем сразу,
            // а каждому, сразу после ввода, если клиентов несколько, начнутся проблемы

            //Thread acceptThread = new Thread(AcceptConnections);
            //acceptThread.Start();

            //CreateTopology();

            //Console.ReadLine();

        }
        private void ConnectAllForTopology(int clientsCount=1) {

            // TODO: возможно позже здесь формировать список клиентов и передавать в методы

            List<Client> сlients = new List<Client>();

            OpenConnectForTopology(clientsCount, сlients);

            CreateTopology(сlients);

            CloseConnectionForTopology(сlients);

            сlients.Clear();

        }

         void OpenConnectForTopology(int clientsCount, List<Client> clients) {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 999));
            serverSocket.Listen(1);
            //Console.WriteLine("Ввод числа подключений:");
            PrintMessage($"Ожидаемое число подключений: {clientsCount}");

            //int count = Convert.ToInt32(Console.ReadLine());
            //Console.WriteLine("Ожидание подключений..");
            PrintMessage("Ожидание подключений...");
            for (int i = 0; i < clientsCount; i++) {
                Socket socket = serverSocket.Accept();
                PrintMessage("Новое подключение");
                Client newClient = new Client(socket);
                clients.Add(newClient);
            }
            PrintMessage("Все подключились");

            //serverSocket.Shutdown(SocketShutdown.Both);
            serverSocket.Close();

        }

        //void Print

        void ShowClients (List<Client> clients){
            PrintMessage("Список клиентов:");
            string ip;
            int id;
            foreach (Client cl in clients) {
                ip = Convert.ToString(cl._socket.RemoteEndPoint);
                id = cl._id;
                PrintMessage($"ip:port = {ip}, id = {id}");
            }
            PrintMessage("\n*id=0 - сервер");
        }

        // TODO: проверрить и далее поменять на делегат
        int? GetNeibId() {
            PrintMessage("Id:");
            string strId = Console.ReadLine();
            if(strId == "") {
                return null;
            }
            else {
                int id = int.Parse(strId);
                return id;
            }
        }

        int GetNeibPriority() {
            PrintMessage("Priority:");
            int priority = int.Parse(Console.ReadLine());
            return priority;
        }

        List<Unit> GetNeibsForClient(Client client, List<Client> clients) {

            string clientIp = client.GetIpPort();
            int clientId = client._id;

            PrintMessage($"Для узла {clientIp} (id = {clientId}):");

            List<Unit> neibs = new List<Unit>();
            while (true) {

                // ввожу id соседа
                // ищу клиента с таким id
                // ввожу priority для соседа
                // получаю ip соседа
                //получаю port соседа
                // создаю юнит
                // добавляю unit в список

                int? id = GetNeibId();
                // проверка значения id на выход из цикла
                
                if(id == null) {
                    break;
                }

                foreach(Client cl in clients) {

                    if (id == 0) {
                        Unit NeibServer = new Unit(SERVER_IP, 1, 700);
                        neibs.Add(NeibServer);
                        continue;
                    }

                    if (cl._id == id) {
                       int priority = GetNeibPriority();
                        string ip = cl.GetIp();
                        int port = cl._acceptPort;
                        Unit n = new Unit(ip, priority, port);
                        neibs.Add(n);
                        break;
                    }
                }

            }

            return neibs;
        }

        void CreateTopology(List<Client> clients) {
            string ip;
            int id;


            // ОТОБРАЖЕНИЕ СПИСКА ПОДКЛЮЧЕННЫХ СОЕДИНЕНИЙ (ВСЕХ КЛИЕНТОВ)
            ShowClients(clients);

            // ФОРМИРОВАНИЕ И ОТПРАВКА СОСЕДЕЙ ДЛЯ КАЖДОГО СОЕДИНЕНИЯ
            foreach (Client client in clients) {

                ip = Convert.ToString(client._socket.RemoteEndPoint);
                id = client._id;

                // получение списка соседей для заданного ip(id)

                List<Unit> Neibs = GetNeibsForClient(client, clients);

                #region GetNeibs в моем случае
                //while (true) {
                //    Console.Write("Id: ");
                //    string strId = Console.ReadLine();
                //    if (strId == "") {
                //        Console.WriteLine("Конец списка");
                //        break;
                //    }
                //    int neibId = Convert.ToInt32(strId);
                //if (neibId == 0) {
                //    Unit NeibServer = new Unit(ServerIp, 1, 700);
                //    Neibs.Add(NeibServer);
                //    continue;
                //}
                //    Console.Write("Value: ");
                //    int neibPriority = Convert.ToInt32(Console.ReadLine());
                //    foreach (Client cl in Clients) {     // позже при добавлении сервера-соседа ставить флаг не заходить в цикл
                //        if (cl._id == neibId) {
                //            string neibIp = cl.GetIp();
                //            int neibAcceptPort = cl._acceptPort;
                //            Unit newNeib = new Unit(neibIp, neibPriority, neibAcceptPort);
                //            Neibs.Add(newNeib);
                //            break;
                //        }
                //    }

                //}
                #endregion

                // ПОДГОТОВКА СПИСКА СОСЕДЕЙ ДЛЯ ОТПРАВЛЕНИЯ КЛИЕНТУ

                //CreateByteBuffer()

                //byte[] buffer;
                ConvertNeibsForSending(Neibs, out byte[] buffer);
                Neibs.Clear();

                //byte[] buffer = new byte[1000];
                //DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
                //MemoryStream stream = new MemoryStream();
                //jsonSerializer.WriteObject(stream, Neibs);
                //Neibs.Clear();
                //buffer = stream.GetBuffer();
                //stream.Close();


                // ПЕРЕДАЧА КЛИЕНТУ

                SendNeibs(client, buffer);

                //lock (locker) {
                //    client._socket.Send(buffer);


                //    string clientId = Convert.ToString(client._id);
                //    client._socket.Send(Encoding.ASCII.GetBytes(clientId));
                //    Thread.Sleep(2000);
                //    string clientAcceptPort = Convert.ToString(client._acceptPort);
                //    client._socket.Send(Encoding.ASCII.GetBytes(clientAcceptPort));


                //}



                //string clientId = Convert.ToString(client._id);
                //client._socket.Send(Encoding.ASCII.GetBytes(clientId));

                PrintMessage("Топология отправлена " + ip);

                //client._socket.Close();     // проверить

            }


            // ЗАКРЫТИЕ СОЕДИНЕНИЙ С КЛИЕНТАМИ (перенести в closeconnect)
            // проверить
            //foreach (Client cl in Clients) {
            //    //cl._socket.Shutdown((SocketShutdown.Both));
            //    cl._socket.Close();
            //}
            //Clients.Clear();
        }

        void SendNeibs(Client client, byte[] buffer) {
            lock (locker) {
                client._socket.Send(buffer);


                string clientId = Convert.ToString(client._id);
                client._socket.Send(Encoding.ASCII.GetBytes(clientId));
                Thread.Sleep(2000);
                string clientAcceptPort = Convert.ToString(client._acceptPort);
                client._socket.Send(Encoding.ASCII.GetBytes(clientAcceptPort));
            }
        }

        void ConvertNeibsForSending(List<Unit> neibs, out byte[] buffer) {
            buffer = new byte[1000];
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
            MemoryStream stream = new MemoryStream();
            jsonSerializer.WriteObject(stream, neibs);
            //Neibs.Clear();
            buffer = stream.GetBuffer();
            stream.Close();
        }

        void CloseConnectionForTopology(List<Client> clients) {
            foreach (Client cl in clients) {
                //cl._socket.Shutdown((SocketShutdown.Both));
                cl._socket.Close();
            }
        }

        void PrintMessage(string message) {
            Console.WriteLine(message);
        }
    }

    // узел - задаем и отправляем при вводе топологии 
    [DataContract]
    public class Unit {
        [DataMember]
        public string ip;
        [DataMember]
        public int priority;        // пока >0 - отправка (с приоритетом), -1 - прием
        [DataMember]
        public int acceptPort;

        public Unit(string addr, int weight, int port) {
            ip = addr;
            priority = weight;
            acceptPort = port;
        }
    }

    // связывает сокет с потоком (надо, если по топологии к серву несколько подключений)
    class ConnectionInfo {
        public Socket Socket;
        public Thread Thread;
    }

    class Client {
        public Socket _socket;
        public int _id;
        // порт приема каждого узла (теперь можно на одном компе запускать несколько клиентов)
        public int _acceptPort;

        static int countClients = 1; // серв - id 0

        static int acceptPort = 660;

        public Client(Socket socket) {
            _socket = socket;
            _id = countClients;
            _acceptPort = acceptPort;
            countClients++;
            acceptPort++;
        }

        public string GetIpPort() {
            string ipPort = Convert.ToString(_socket.RemoteEndPoint);
            return ipPort;
        }

        // мб найти получше
        public string GetIp() {
            string ipAndPort = Convert.ToString(_socket.RemoteEndPoint);
            int index = ipAndPort.IndexOf(":");
            string ip = ipAndPort.Remove(index);
            return ip;
        }
    }
}
