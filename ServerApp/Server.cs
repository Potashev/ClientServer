using ClientServerLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerProject {
    public class Server: Node {

        //static object locker = new object();    // TODO: попробовать перенести locker в SendNeibs
        private bool createTopology;

        private string inConnectionsFileName = "NumberIncomingConnections.txt";

        public Server(InputDelegate userInput, OutputDelegate userOutput, int acceptPort, bool createTopology = false) : base(userInput, userOutput) {

            // TODO: возможно от порта сервера формировать порты приема у клиентов, чтобы могло работать на одной машине
            AcceptPort = acceptPort;
            this.createTopology = createTopology;

            eventPacketSequenceAdded += PrintNewPackets;
        }

        public override void Run() {

            if (createTopology) {
                PrintMessage("Формирование топологии.");
                InputConnectionsCount(out int numberClientsInTopology);
                CreateTopology(numberClientsInTopology);
                WriteNumberConnectionsToServer();
            }
            else {
                GetNumberConnectionsToServer();
            }

            PrintMessage($"Число входящих - {numberIncomingConnections}");  // временно

            PrintMessage("Прием пакетов...");

            ReceivingProccess();
            //await Task.Run(() => ReceivingProccess());

        }

        void WriteNumberConnectionsToServer() {
            WriteStream(inConnectionsFileName, numberIncomingConnections);
        }

        void GetNumberConnectionsToServer() {
            numberIncomingConnections = ReadStream(inConnectionsFileName);
        }


        void PrintNewPackets(List<DataPacket> packets) {
                foreach (DataPacket p in packets)
                    PrintMessage(p.GetInfo());
        }

        

        //bool NotEmpty(byte[] buffer) {
        //    for(int i=0; i< buffer.Length; i++) {
        //        if (buffer[i] != 0)
        //            return true;
        //    }
        //    return false;
        //}
        

        void CreateTopology(int clientsCount) {
            // TODO: возможно позже здесь формировать список клиентов и передавать в методы
            CreateClietnsList(out List<TopologyClient> clients);
            OpenConnectForTopology(clientsCount, clients);  // подключить всех клиентов
            ShowClients(clients);                           // показать список клиентов
            DefineClientsCommunications(clients);           // ввод соседей клиента и отправка их ему
            CloseConnectionForTopology(clients);            // закрыть соединение
            clients.Clear();
        }

        void CreateClietnsList(out List<TopologyClient> clients) {
            TopologyClient.SetStartPortValue(AcceptPort);   //TODO: подумать, можно ли чем заменить
            clients = new List<TopologyClient>();
        }

        void OpenConnectForTopology(int clientsCount, List<TopologyClient> clients) {
            RunAcceptSocket(out Socket serverAcceptSocket, clientsCount, SERVER_PORT_FOR_TOPOLOGY);
            PrintMessage($"Ожидаемое число подключений: {clientsCount}");
            PrintMessage("Ожидание подключений...");
            for (int i = 0; i < clientsCount; i++) {
                Socket socket = serverAcceptSocket.Accept();
                TopologyClient newClient = new TopologyClient(socket);
                clients.Add(newClient);
                PrintMessage("Новое подключение.");
            }
            PrintMessage("Все узлы подключились.");
            //CloseSocket(serverAcceptSocket);
            serverAcceptSocket.Close();


        }

        void DefineClientsCommunications(List<TopologyClient> clients) {
            foreach (TopologyClient client in clients) {
                var Neighbours = GetNeighboursForClient(client, clients);
                ConvertNeighboursForSending(Neighbours, out byte[] byteNeighbours);
                Neighbours.Clear();
                SendData(client, byteNeighbours);
                PrintMessage("Топология отправлена " + client.GetIp());
            }
        }

        void ShowClients (List<TopologyClient> clients){
            PrintMessage("Узлы топологии:");
            string ip;
            int id;
            foreach (TopologyClient cl in clients) {
                ip = Convert.ToString(cl.Socket.RemoteEndPoint);
                id = cl.Id;
                PrintMessage($"ip:port = {ip}, id = {id}");
            }
            PrintMessage("\n*id=0 - сервер");
        }

        
        

        //delegate bool ValueRequirement(int value);

        //int GetInputData(ValueRequirement requirement) {
        //        int resultData = 0;
        //        while (true) {
        //            if (int.TryParse(InputData(), out int inputvalue) && requirement(inputvalue)) {
        //                resultData = inputvalue;
        //                break;
        //            }
        //            else {
        //                PrintMessage("Неверный ввод, повторите попытку:");
        //            }
        //        }
        //        return resultData;
            
        //}

        //int CheckInputData(string stringValue, ValueRequirement requirement) {
        //    if(int.TryParse(stringValue, out int inputValue) && requirement(inputValue)) {
        //        return inputValue;
        //    }
        //    else {
        //        PrintMessage("Неверный ввод, повторите попытку:");
        //        return GetInputData(requirement);
        //    }
        //}

        void InputConnectionsCount(out int count) {
            count = GetInputData(x => (x > 0), "Введите число подключений:");
        }

        void InputNeighbourPriority(out int priority) {
            //priority = GetInputData(x => (x > 0 || x == -1), "Приоритет:");
            PrintMessage(Neighbour.GetInputValueInfo());
            priority = GetInputData(x => (x >= 0), "Приоритет:");

        }

        // TODO: Добавить проверку на дурака
        int? InputNeighbourId(int clientsCount, int clientId) {
            string inputMessage = "Id:";
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

        // TODO: Добавить проверку на дурака
        //int InputNeibPriority() {
        //    PrintMessage("Приоритет:");
        //    //int priority = int.Parse(InputData());
        //    int priority = GetInputData(x => (x > 0 || x == -1));
        //    return priority;
        //}

        List<Neighbour> GetNeighboursForClient(TopologyClient client, List<TopologyClient> clients) {

            string clientIp = client.GetIpPort();
            int clientId = client.Id;
            PrintMessage($"Для узла {clientIp} (id = {clientId}):");
            List<Neighbour> neibs = new List<Neighbour>();
            while (true) {
                //PrintMessage("Id:");
                int? id = InputNeighbourId(clients.Count, clientId);

                // проверка значения id на выход из цикла
                if(id == null) {
                    break;
                }

                if (id == 0) {
                    Neighbour NeibServer = new Neighbour(SERVER_IP, 1, AcceptPort);
                    neibs.Add(NeibServer);

                    numberIncomingConnections++;    // TODO: проверить

                    continue;
                }

                foreach (TopologyClient cl in clients) {

                    if (cl.Id == id) {
                        //PrintMessage("Приоритет:");
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
        
        void SendData(TopologyClient client, byte[] byteNeighbours) {
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

        void CloseConnectionForTopology(List<TopologyClient> clients) {
            foreach (TopologyClient client in clients) {
                client.Socket.Close();
            }
        }
    }

    class TopologyClient {
        public Socket Socket { get; set; }
        public int Id { get; set; }
        public int AcceptPort { get; set; }

        private static int clientCounter = 1;
        private static int acceptPortCounter;

        public static void SetStartPortValue (int ServerAcceptPort) {
            acceptPortCounter = ServerAcceptPort + 1;
        }

        public TopologyClient(Socket socket) {
            Socket = socket;
            Id = clientCounter;
            AcceptPort = acceptPortCounter;

            clientCounter++;
            acceptPortCounter++;
        }

        public string GetIpPort() {
            string ipPort = Convert.ToString(Socket.RemoteEndPoint);
            return ipPort;
        }

        // мб найти получше
        public string GetIp() {
            string ipAndPort = Convert.ToString(Socket.RemoteEndPoint);
            int index = ipAndPort.IndexOf(":");
            string ip = ipAndPort.Remove(index);
            return ip;
        }
    }
}
