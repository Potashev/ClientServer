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

        const string SERVER_IP = "192.168.1.104";   // TODO: брать ip терминала либо методом, либо вводить при запуске
        //const string SERVER_IP = "172.20.10.2";
        const int ACCEPT_PORT = 700;

        static object locker = new object();    // TODO: попробовать перенести locker в SendNeibs

        public delegate string inputData();
        inputData input;
        public delegate void outputData(string message);
        outputData output;
        
        List<ConnectionInfo> Connections = new List<ConnectionInfo>();

        public Server(inputData userInput, outputData userOutput) {
            input = userInput;
            output = userOutput;
        }

        public void Run(bool createTopology) {

            if (createTopology) {
                PrintMessage("Формирование топологии");
                ConnectAllForTopology();
            }
            PrintMessage("Прием пакетов");
            // прослушку запускаю раньше топологии, тк она отправляется не всем сразу,
            // а каждому, сразу после ввода, если клиентов несколько, начнутся проблемы

            Thread acceptThread = new Thread(AcceptConnections);
            acceptThread.Start();
        }

        void AcceptConnections() {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, ACCEPT_PORT));
            serverSocket.Listen(1);             // аргумент не влияет?
            while (true) {
                Socket socket = serverSocket.Accept();
                ConnectionInfo connection = new ConnectionInfo();
                connection.Socket = socket;
                connection.Thread = new Thread(ProcessConnection);
                connection.Thread.IsBackground = true;
                connection.Thread.Start(connection);
                Connections.Add(connection);
            }
        }
        
        DataPacket GetDataFromBuffer(byte[] buffer) {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DataPacket));
            DataPacket packet = new DataPacket();
            byte[] streamBuffer = new byte[BytesInCollection(buffer)];
            CopyFromTo(buffer, streamBuffer);
            MemoryStream stream = new MemoryStream(streamBuffer);
            stream.Position = 0;    // необязательно?
            packet = (DataPacket)serializer.ReadObject(stream);
            stream.Close();
            return packet;
        }
        
        void ProcessConnection(object state) {
            ConnectionInfo connection = (ConnectionInfo)state;
            byte[] buffer = new byte[255];
            try {
                while (true) {
                    int bytesRead = connection.Socket.Receive(buffer);
                    if (bytesRead > 0) {
                        try {
                            DataPacket packet = GetDataFromBuffer(buffer);
                            PrintMessage($"Узел: {packet._unitId}, пакет: {packet._number} , данные: {packet._value}");
                        }
                        catch (Exception ex) {
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

        int BytesInCollection(byte[] collection) {
            int count = 0;
            while (collection[count] != 0) {
                count++;
            }
            return count;
        }
        void CopyFromTo(byte[] bufferFrom, byte[] bufferTO) {
            for (int i = 0; i < bufferTO.Length; i++) {
                bufferTO[i] = bufferFrom[i];
            }
        }


        void ConnectAllForTopology(int clientsCount=1) {

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
            PrintMessage($"Ожидаемое число подключений: {clientsCount}");
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

        void CreateTopology(List<Client> clients) {
            ShowClients(clients);
            foreach (Client client in clients) {
                List<Unit> Neibs = GetNeibsForClient(client, clients);
                ConvertNeibsForSending(Neibs, out byte[] buffer);
                Neibs.Clear();
                SendNeibs(client, buffer);
                PrintMessage("Топология отправлена " + client.GetIp());
            }
        }

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

        
        // TODO: Добавить проверку на дурака
        int? GetNeibId() {
            PrintMessage("Id:");
            string strId = InputData();
            if (strId == "") {
                return null;
            }
            else {
                int id = int.Parse(strId);
                return id;
            }
        }

        // TODO: Добавить проверку на дурака
        int GetNeibPriority() {
            PrintMessage("Priority:");
            int priority = int.Parse(InputData());
            return priority;
        }

        List<Unit> GetNeibsForClient(Client client, List<Client> clients) {

            string clientIp = client.GetIpPort();
            int clientId = client._id;
            PrintMessage($"Для узла {clientIp} (id = {clientId}):");
            List<Unit> neibs = new List<Unit>();
            while (true) {
                int? id = GetNeibId();
                // проверка значения id на выход из цикла
                
                if(id == null) {
                    break;
                }

                foreach(Client cl in clients) {

                    if (id == 0) {
                        Unit NeibServer = new Unit(SERVER_IP, 1, ACCEPT_PORT);
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
            output(message);
        }

        // TODO: возможно функцию убрать и вызывать делегат
        string InputData() {
            return input();
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

    [DataContract]
    public class DataPacket {
        [DataMember]
        public int _unitId;
        [DataMember]
        public int _number;
        [DataMember]
        public int _value;
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
