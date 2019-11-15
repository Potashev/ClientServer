using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientServerLib {
    public class Server: Node {

        public Server(InputDelegate userInput, OutputDelegate userOutput, int acceptPort) : base(userInput, userOutput) {
            AcceptPort = acceptPort;
            eventPacketSequenceAdded += PrintNewPackets;
        }

        public void Start(bool createTopology = false) {

            if (createTopology) {
                PrintMessage("Формирование топологии.");
                InputConnectionsCount(out int numberClientsInTopology);
                CreateTopology(numberClientsInTopology);
                WriteNumberConnectionsToServer();
            }
            else {
                GetNumberConnectionsToServer();
            }

            PrintMessage($"Число входящих подключений: {numberIncomingConnections}");
            PrintMessage("Прием пакетов...");

            ReceivingProccess();
        }

        void WriteNumberConnectionsToServer() {
            WriteStream(INCONNECTIONS_FILENAME, numberIncomingConnections);
        }

        void GetNumberConnectionsToServer() {
            numberIncomingConnections = ReadStream(INCONNECTIONS_FILENAME);
        }
        
        void PrintNewPackets(List<DataPacket> packets) {
                foreach (DataPacket p in packets)
                    PrintMessage(p.GetInfo());
        }
        
        void CreateTopology(int clientsCount) {
            CreateClietnsList(out List<TopologyUnit> clients);
            OpenConnectForTopology(clientsCount, clients);
            ShowClients(clients);
            DefineClientsCommunications(clients);
            CloseConnectionForTopology(clients);
            clients.Clear();
        }

        void CreateClietnsList(out List<TopologyUnit> clients) {
            TopologyUnit.SetStartPortValue(AcceptPort);
            clients = new List<TopologyUnit>();
        }

        void OpenConnectForTopology(int clientsCount, List<TopologyUnit> clients) {
            RunAcceptSocket(out Socket serverAcceptSocket, clientsCount, SERVER_PORT_FOR_TOPOLOGY);
            PrintMessage($"Ожидаемое число подключений: {clientsCount}");
            PrintMessage("Ожидание подключений...");
            for (int i = 0; i < clientsCount; i++) {
                Socket socket = serverAcceptSocket.Accept();
                TopologyUnit newClient = new TopologyUnit(socket);
                clients.Add(newClient);
                PrintMessage("Новое подключение.");
            }
            PrintMessage("Все узлы подключились.");
            serverAcceptSocket.Close();
        }

        void DefineClientsCommunications(List<TopologyUnit> clients) {
            foreach (TopologyUnit client in clients) {
                var Neighbours = GetNeighboursForClient(client, clients);
                ConvertNeighboursForSending(Neighbours, out byte[] byteNeighbours);
                Neighbours.Clear();
                SendData(client, byteNeighbours);
                PrintMessage("Топология отправлена " + client.GetIp());
            }
        }

        void ShowClients (List<TopologyUnit> clients){
            PrintMessage("Узлы топологии:");
            string ip;
            int id;
            foreach (TopologyUnit cl in clients) {
                ip = Convert.ToString(cl.Socket.RemoteEndPoint);
                id = cl.Id;
                PrintMessage($"ip:port = {ip}, id = {id}");
            }
            PrintMessage("\n*id=0 - сервер");
        }

        void InputConnectionsCount(out int count) {
            count = GetInputData(x => (x > 0), "Введите число подключений:");
        }

        void InputNeighbourPriority(out int priority) {
            PrintMessage(Neighbour.GetPriorityValueInfo());
            priority = GetInputData(x => (x >= 0), "Приоритет:");

        }

        int? InputNeighbourId(int clientsCount, int clientId) {
            string inputMessage = "Id (Enter - завершение ввода):";
            PrintMessage(inputMessage);
            string strId = InputData();
            if (strId == "") {
                return null;
            }
            else {
                int id = CheckInputData(x => (x >= 0 && x <= clientsCount && x != clientId), strId, inputMessage);
                return id;
            }
        }

        List<Neighbour> GetNeighboursForClient(TopologyUnit client, List<TopologyUnit> clients) {

            string clientIp = client.GetIpPort();
            int clientId = client.Id;
            PrintMessage($"Для узла {clientIp} (id = {clientId}):");
            List<Neighbour> neibs = new List<Neighbour>();
            while (true) {
                int? id = InputNeighbourId(clients.Count, clientId);
                if(id == null) {
                    break;
                }
                if (id == 0) {

                    string serverIp = GetIpAdress().ToString();

                    Neighbour NeibServer = new Neighbour(serverIp, 1, AcceptPort);
                    neibs.Add(NeibServer);

                    numberIncomingConnections++;

                    continue;
                }
                foreach (TopologyUnit cl in clients) {
                    if (cl.Id == id) {
                        InputNeighbourPriority(out int priority);
                        string ip = cl.GetIp();
                        int port = cl.AcceptPort;
                        Neighbour n = new Neighbour(ip, priority, port);
                        neibs.Add(n);
                        break;
                    }
                }

            }
            return neibs;
        }
        
        void SendData(TopologyUnit client, byte[] byteNeighbours) {
            try {
                client.Socket.Send(byteNeighbours);

                var clientId = client.Id.ToString();
                client.Socket.Send(Encoding.ASCII.GetBytes(clientId));
                Thread.Sleep(100);
                var clientAcceptPort = client.AcceptPort.ToString();
                client.Socket.Send(Encoding.ASCII.GetBytes(clientAcceptPort));
            }
            catch(Exception ex) {
                PrintMessage($"Соединение с клиентом (id={client.Id}) было потеряно.");
                PrintMessage(ex.Message);
            }

        }

        void ConvertNeighboursForSending(List<Neighbour> Neighbours, out byte[] buffer) {
            MemoryStream memoryStream = new MemoryStream();
            SerializeJson(Neighbours, memoryStream);
            buffer = memoryStream.GetBuffer();
            memoryStream.Close();
        }

        void CloseConnectionForTopology(List<TopologyUnit> clients) {
            foreach (TopologyUnit client in clients) {
                client.Socket.Close();
            }
        }

        private const string INCONNECTIONS_FILENAME = "NumberIncomingConnections.txt";
    }
}
