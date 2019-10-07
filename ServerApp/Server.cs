using ClientServerLib;
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
    public class Server: Node {

        //const string SERVER_IP = "192.168.1.107";   // TODO: брать ip терминала либо методом, либо вводить при запуске
        //const string SERVER_IP = "172.20.10.2";
        
        const int SERVER_ACCEPT_PORT = 700;

        static object locker = new object();    // TODO: попробовать перенести locker в SendNeibs

        public delegate string inputData();
        inputData input;
        public delegate void outputData(string message);
        outputData output;
        
        List<ConnectionInfo> Connections = new List<ConnectionInfo>();

        public Server(inputData userInput, outputData userOutput) {
            input = userInput;
            output = userOutput;

            AcceptPort = SERVER_ACCEPT_PORT;
        }

        public void Run(bool createTopology) {

            if (createTopology) {
                PrintMessage("Формирование топологии");
                PrintMessage("Введите число подключений");
                int unitsCount = GetConnectionsCount();
                ConnectAllForTopology(unitsCount);
            }
            PrintMessage("Прием пакетов");
            // прослушку запускаю раньше топологии, тк она отправляется не всем сразу,
            // а каждому, сразу после ввода, если клиентов несколько, начнутся проблемы

            Thread acceptThread = new Thread(AcceptConnections);
            acceptThread.Start();
        }

        bool NotEmpty(byte[] buffer) {
            for(int i=0; i< buffer.Length; i++) {
                if (buffer[i] != 0)
                    return true;
            }
            return false;
        }
        
        //void ProcessConnection(object state) {
        //    ConnectionInfo connection = (ConnectionInfo)state;
        //    byte[] buffer = new byte[255];
        //    try {
        //        while (true) {
        //            int bytesRead = connection.Socket.Receive(buffer);
        //            if (bytesRead > 0) {

        //                int startIndex = 0;
        //                //while (NotEmpty(buffer)) {
        //                    try {
        //                        // TODO: Обрабатывать ситуации, когда в буфере несколько пакетов



        //                        // ОСТАНОВИЛСЯ ЗДЕСЬ
        //                        PrintMessage("Прием+");
        //                    //int size = DataPacket.GetObjectSize();  // TODO: возможно сделать GetBytes.lenght т.к размер буфера считать тоже надо
        //                    //byte[] onePacketBuf = new byte[size+1]; // размер буфера с 1 объектом на 2 ьайта больше чем объект в байтах
        //                    //Array.Copy(buffer,startIndex, onePacketBuf, size);
        //                    //Array.Clear(buffer, startIndex, size+2);
        //                    //startIndex += size+2;

        //                    // TODO: перенести getdatafrom в datapacket как статич
        //                        List<DataPacket> packet = GetDataFromBuffer(buffer);
        //                        foreach (DataPacket p in packet)
        //                        PrintMessage(p.GetInfo());
        //                    //PrintMessage(DataPacket.GetObjectSize().ToString()); // 36
        //                }
        //                    catch (Exception ex) {
        //                        continue;
        //                    }
        //                //}

        //            }
        //            else if (bytesRead == 0) return;
        //        }
        //    }
        //    catch (SocketException exc) {
        //        Console.WriteLine("Socket exception: " +
        //            exc.SocketErrorCode);
        //    }
        //    catch (Exception exc) {
        //        Console.WriteLine("Exception: " + exc);
        //    }
        //    finally {
        //        connection.Socket.Close();
        //        lock (Connections) Connections.Remove(
        //            connection);
        //    }
        //}


        void ConnectAllForTopology(int clientsCount = 1) {

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
                List<Neighbour> Neibs = GetNeibsForClient(client, clients);
                ConvertNeibsForSending(Neibs, out byte[] buffer);
                Neibs.Clear();
                SendNeibs(client, buffer);
                PrintMessage("Топология отправлена " + client.GetIp());
            }
        }

        void ShowClients (List<Client> clients){
            PrintMessage("Узлы топологии:");
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
        int? InputNeibId() {
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

        int GetConnectionsCount() {
            string strCount = InputData();
                int count = int.Parse(strCount);
                return count;
        }

        // TODO: Добавить проверку на дурака
        int GetNeibPriority() {
            PrintMessage("Priority:");
            int priority = int.Parse(InputData());
            return priority;
        }

        List<Neighbour> GetNeibsForClient(Client client, List<Client> clients) {

            string clientIp = client.GetIpPort();
            int clientId = client._id;
            PrintMessage($"Для узла {clientIp} (id = {clientId}):");
            List<Neighbour> neibs = new List<Neighbour>();
            while (true) {
                int? id = InputNeibId();
                // проверка значения id на выход из цикла
                
                if(id == null) {
                    break;
                }

                if (id == 0) {
                    //Unit NeibServer = new Unit(SERVER_IP, 1, ACCEPT_PORT);
                    Neighbour NeibServer = new Neighbour(SERVER_IP, 1, AcceptPort);
                    neibs.Add(NeibServer);
                    continue;
                }

                foreach (Client cl in clients) {
                   
                    //    if (id == 0) {
                    //    Unit NeibServer = new Unit(SERVER_IP, 1, ACCEPT_PORT);
                    //    neibs.Add(NeibServer);
                    //    continue;
                    //}

                    if (cl._id == id) {
                       int priority = GetNeibPriority();
                        string ip = cl.GetIp();
                        int port = cl._acceptPort;
                        Neighbour n = new Neighbour(ip, priority, port);
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
                // TODO: убрать либо сделать меньше
                Thread.Sleep(2000);
                string clientAcceptPort = Convert.ToString(client._acceptPort);
                client._socket.Send(Encoding.ASCII.GetBytes(clientAcceptPort));
            }
        }

        void ConvertNeibsForSending(List<Neighbour> neibs, out byte[] buffer) {
            buffer = new byte[1000];
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Neighbour>));
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
