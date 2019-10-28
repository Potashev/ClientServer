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

        //static object locker = new object();    // TODO: попробовать перенести locker в SendNeibs
        bool createTopology;

        public Server(InputDelegate userInput, OutputDelegate userOutput, int acceptPort, bool createTopology = false) : base(userInput, userOutput) {

            AcceptPort = acceptPort;
            this.createTopology = createTopology;

            PacketSequenceAdded += PrintNewPackets;

        }

        void PrintNewPackets(List<DataPacket> packets) {
                foreach (DataPacket p in packets)
                    PrintMessage(p.GetInfo());
        }

        public override void Run() {

            if (createTopology) {
                PrintMessage("Формирование топологии");
                PrintMessage("Введите число подключений");
                int unitsCount = GetConnectionsCount();
                ConnectAllForTopology(unitsCount);
            }

            PrintMessage("Прием пакетов...");

            ReceivingProccess();
        }

        bool NotEmpty(byte[] buffer) {
            for(int i=0; i< buffer.Length; i++) {
                if (buffer[i] != 0)
                    return true;
            }
            return false;
        }
        

        void ConnectAllForTopology(int clientsCount) {

            // TODO: возможно позже здесь формировать список клиентов и передавать в методы

            List<Client> сlients = new List<Client>();
            OpenConnectForTopology(clientsCount, сlients);
            CreateTopology(сlients);
            CloseConnectionForTopology(сlients);
            сlients.Clear();

        }

        void OpenConnectForTopology(int clientsCount, List<Client> clients) {
            //Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //serverSocket.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT_FOR_TOPOLOGY));
            //serverSocket.Listen(1);


            RunAcceptSocket(SERVER_PORT_FOR_TOPOLOGY, out Socket serverSocket);

            PrintMessage($"Ожидаемое число подключений: {clientsCount}");
            PrintMessage("Ожидание подключений...");
            for (int i = 0; i < clientsCount; i++) {
                Socket socket = serverSocket.Accept();
                PrintMessage("Новое подключение");
                Client newClient = new Client(socket);
                clients.Add(newClient);
            }
            PrintMessage("Все подключились");

            ////serverSocket.Shutdown(SocketShutdown.Both);
            //serverSocket.Close();

            CloseSocket(serverSocket);


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
            PrintMessage("Приоритет:");
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
                    Neighbour NeibServer = new Neighbour(SERVER_IP, 1, AcceptPort);
                    neibs.Add(NeibServer);
                    continue;
                }

                foreach (Client cl in clients) {

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
                Thread.Sleep(100);
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
                ////cl._socket.Shutdown((SocketShutdown.Both));
                //cl._socket.Close();
                CloseSocket(cl._socket);
            }
        }
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
