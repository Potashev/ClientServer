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

namespace ServerApp
{
    class Program
    {

        static object locker = new object();

        static string ServerIp = "192.168.1.105";
        // списох узлов, которые подключаются по топологии
        static List<ConnectionInfo> Connections = new List<ConnectionInfo>();
        // список всех узлов, подключившихся к серву для топологии
        static List<Client> Clients = new List<Client>();
        // список узлов-соседей, который отправляется каждому клиенту по топологии (у каждого клиента свой список)
        static List<Unit> Neibs = new List<Unit>();
        // связывает сокет с потоком (надо, если по топологии к серву несколько подключений)
        private class ConnectionInfo
        {
            public Socket Socket;
            public Thread Thread;
        }

        public class Client
        {
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

            // мб найти получше
            public string GetIp() {
                string ipAndPort = Convert.ToString(_socket.RemoteEndPoint);
                int index = ipAndPort.IndexOf(":");
                string ip = ipAndPort.Remove(index);
                return ip;
            }
        }

        // узел - задаем и отправляем при вводе топологии 
        [DataContract]
        public class Unit
        {
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

        [DataContract]
        public class DataPacket
        {
            [DataMember]
            public int _unitId;
            [DataMember]
            public int _number;
            [DataMember]
            public int _value;

            //static int packetCount = 1;
            //public DataPacket(int value) {
            //    _unitId = myId;
            //    _number = packetCount;
            //    _value = value;
            //    packetCount++;
            //}
        }

        static void AcceptForTopology() {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 999));
            serverSocket.Listen(1);
            Console.WriteLine("Ввод числа подключений:");
            int count = Convert.ToInt32(Console.ReadLine());
            Console.WriteLine("Ожидание подключений..");
            for (int i = 0; i < count; i++) {
                Socket socket = serverSocket.Accept();
                Console.WriteLine("Новое подключение");
                Client newClient = new Client(socket);
                Clients.Add(newClient);
            }
            Console.WriteLine("Все подключились\n");

            //serverSocket.Shutdown(SocketShutdown.Both);
            serverSocket.Close();

        }


        static void CreateTopology() {
            string ip;
            int id;

            Console.WriteLine("Список клиентов:");
            foreach (Client cl in Clients) {
                ip = Convert.ToString(cl._socket.RemoteEndPoint);
                id = cl._id;
                Console.WriteLine("ip:port = " + ip + " , id = " + id);
            }

            Console.WriteLine("\n*id=0 - сервер");
            foreach (Client client in Clients) {
                ip = Convert.ToString(client._socket.RemoteEndPoint);
                id = client._id;
                Console.WriteLine("Для узла " + ip + " (id " + id + ")");
                while (true) {
                    Console.Write("Id: ");
                    string strId = Console.ReadLine();
                    if (strId == "") {
                        Console.WriteLine("Конец списка");
                        break;
                    }
                    int neibId = Convert.ToInt32(strId);
                    if (neibId == 0) {
                        Unit NeibServer = new Unit(ServerIp, 1, 700);
                        Neibs.Add(NeibServer);
                        continue;
                    }
                    Console.Write("Value: ");
                    int neibPriority = Convert.ToInt32(Console.ReadLine());
                    foreach (Client cl in Clients) {     // позже при добавлении сервера-соседа ставить флаг не заходить в цикл
                        if (cl._id == neibId) {
                            string neibIp = cl.GetIp();
                            int neibAcceptPort = cl._acceptPort;
                            Unit newNeib = new Unit(neibIp, neibPriority, neibAcceptPort);
                            Neibs.Add(newNeib);
                            break;
                        }
                    }

                }
                byte[] buffer = new byte[1000];
                DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
                MemoryStream stream = new MemoryStream();
                jsonSerializer.WriteObject(stream, Neibs);
                Neibs.Clear();
                buffer = stream.GetBuffer();
                stream.Close();
                //Console.WriteLine("Передача клиенту..");

                lock (locker)
                {
                    client._socket.Send(buffer);


                    string clientId = Convert.ToString(client._id);
                    client._socket.Send(Encoding.ASCII.GetBytes(clientId));
                    Thread.Sleep(2000);
                    string clientAcceptPort = Convert.ToString(client._acceptPort);
                    client._socket.Send(Encoding.ASCII.GetBytes(clientAcceptPort));


                }
                //string clientId = Convert.ToString(client._id);
                //client._socket.Send(Encoding.ASCII.GetBytes(clientId));

                Console.WriteLine("Топология отправлена " + ip);
                //client._socket.Close();     // проверить

            }
            // проверить
            foreach (Client cl in Clients) {
                //cl._socket.Shutdown((SocketShutdown.Both));
                cl._socket.Close();
            }
            Clients.Clear();
        }

        // прослушка и создание новых подключений 
        static void AcceptConnections() {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 700));
            serverSocket.Listen(1);             // аргумент не влияет?
            while (true) {
                
                // Принимаем соединение
                Socket socket = serverSocket.Accept();
                ConnectionInfo connection = new ConnectionInfo();
                connection.Socket = socket;
                connection.Thread = new Thread(ProcessConnection);
                connection.Thread.IsBackground = true;
                connection.Thread.Start(connection);
                Connections.Add(connection);
            }
        }

        // сам процесс подключения, у каждого подключившегося свой процесс в своем потоке
        static void ProcessConnection(object state) {
            ConnectionInfo connection = (ConnectionInfo)state;
            byte[] buffer = new byte[255];
            try {
                while (true) {
                    int bytesRead = connection.Socket.Receive(buffer);
                    if (bytesRead > 0) {
                        try
                        {
                            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DataPacket));
                            DataPacket packet = new DataPacket();
                            byte[] streamBuffer = new byte[BytesInCollection(buffer)];
                            CopyFromTo(buffer, streamBuffer);
                            MemoryStream stream = new MemoryStream(streamBuffer);
                            stream.Position = 0;    // необязательно?
                            packet = (DataPacket)serializer.ReadObject(stream);
                            stream.Close();
                            Console.WriteLine("Узел: " + packet._unitId + ", пакет: " + packet._number + " , данные: " + packet._value);
                        }
                        catch(Exception ex)
                        {
                            continue;
                        }

                    }
                    else if (bytesRead == 0) return;
                }
            }
            catch (SocketException exc) {
                Console.WriteLine("Socket exception: " +
                    exc.SocketErrorCode);
            }
            catch (Exception exc) {
                Console.WriteLine("Exception: " + exc);
            }
            finally {
                connection.Socket.Close();
                lock (Connections) Connections.Remove(
                    connection);
            }
        }

        static int BytesInCollection(byte[] collection) {
            int count = 0;
            while (collection[count] != 0) {
                count++;
            }
            return count;
        }

        static public void CopyFromTo(byte[] bufferFrom, byte[] bufferTO) {
            for (int i = 0; i < bufferTO.Length; i++) {
                bufferTO[i] = bufferFrom[i];
            }
        }

        
        
        static void Main(string[] args) {
            Console.WriteLine("\tServer");

            Console.WriteLine("Ввод топологии ?");
            string str = Console.ReadLine();

            if (str == "+")
            {
                AcceptForTopology();
                CreateTopology();
            }
            Console.WriteLine("Прием пакетов");
            // прослушку запускаю раньше топологии, тк она отправляется не всем сразу,
            // а каждому, сразу после ввода, если клиентов несколько, начнутся проблемы
            Thread acceptThread = new Thread(AcceptConnections);
            acceptThread.Start();

            //CreateTopology();

            Console.ReadLine();
        }
    }
}
