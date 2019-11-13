using ClientServerLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientServerLib {

    public class Client : Node {
        
        private int clientId;

        private bool addPacketsInMainSequnce;
        private bool sendingAvailable;
        private bool sendingInProcess;
        
        public int TimeGeneration {
            get {
                return timeGeneration;
            }
            set {
                if(value > 0) {
                    timeGeneration = value;
                }
                else {
                    timeGeneration = 100;
                }
            }
        }
        private int timeGeneration;

        public int TimeCheckingLost {
            get {
                return timeCheckingLost;
            }
            set {
                if (value > 0) {
                    timeCheckingLost = value;
                }
                else {
                    timeCheckingLost = 5000;
                }
            }
        }
        private int timeCheckingLost;

        List<DataPacket> additionalPacketsSequence = new List<DataPacket>();

        List<Neighbour> Neighbors = new List<Neighbour>();
        Neighbour currentNeighbourForSending;

        Thread CheckingDiedNodesThread;
        
        private object sendLocker = new object();
        private object addSequenceLocker = new object();
        
        public Client(InputDelegate userInput, OutputDelegate userOutput, int generationTime, int checkLostTime) : base(userInput, userOutput) {
            TimeGeneration = generationTime;
            timeCheckingLost = checkLostTime;
            CheckingDiedNodesThread = new Thread(CheckingLostNodes);
            addPacketsInMainSequnce = true;
            sendingAvailable = true;
            sendingInProcess = false;
            eventPacketSequenceAdded += TrySendNewPackets;
        }

        public void Run() {

            if (File.Exists(TOPOLOGY_FILENAME)) {
                PrintMessage("Восстановление топологии.");
                GetTopologyFromFile();
            }
            else {
                PrintMessage("Подключение к серверу для получения топологии.");
                GetTopologyFromServer();
            }

            ShowConfiguration();
            ShowNeighbours();

            SetNumberIncomingConnections();
            PrintMessage($"Число входящих подключений: {numberIncomingConnections}");

            FindNeighbourForSending();

            PrintMessage("Нажмите клавишу для запуска:");
            InputData();
            
            if (HasInConnection()) {
                PrintMessage("Есть входящие подключения.");
                StartReceiving();
            }
            else {
                PrintMessage("Входящих подключений нет.");
            }

            StartGenerationData();
        }

        void GetTopologyFromFile() {
            FileStream fsReading = new FileStream(TOPOLOGY_FILENAME, FileMode.OpenOrCreate);
            Neighbors = DeserializeJson<Neighbour>(fsReading);
            fsReading.Close();

            clientId = ReadStream(ID_FILENAME);
            AcceptPort = ReadStream(PORT_FILENAME);
        }

        void GetTopologyFromServer() {
            try {
                ConnectToServer(out Socket serverSocket);
                GetNeibsFromServer(serverSocket);
                GetUnitIdFromServer(serverSocket);
                GetAcceptPortFromServer(serverSocket);
                serverSocket.Close();
                Neighbors.Sort(Neighbour.CompareUnitsByPriority);
                WriteTopologyToFiles();
            }
            catch (Exception ex) {
                PrintMessage(ex.Message);
            }
        }

        void ConnectToServer(out Socket socket) {
            string serverIp = GetServerIp();
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(serverIp, SERVER_PORT_FOR_TOPOLOGY);
        }

        string GetServerIp() {
            return ReadStreamString(SERVER_IP_FILENAME);
        }

        void GetNeibsFromServer(Socket socket) {
            PrintMessage("Прием топологии..");
            byte[] neighborsBuffer = new byte[1000];

            socket.Receive(neighborsBuffer);
            
            byte[] streamBuffer = new byte[BytesInCollection(neighborsBuffer)];
            CopyFromTo(neighborsBuffer, streamBuffer);

            MemoryStream memoryStream = new MemoryStream(streamBuffer);
            Neighbors = DeserializeJson<Neighbour>(memoryStream);
            memoryStream.Close();
        }

        void GetUnitIdFromServer(Socket socket) {
            byte[] receiveId = new byte[1000];
                socket.Receive(receiveId);


                byte[] arr = new byte[BytesInCollection(receiveId)];
                CopyFromTo(receiveId, arr);
                string strId = Encoding.ASCII.GetString(arr);
                clientId = int.Parse(strId);
        }

        void GetAcceptPortFromServer(Socket socket) {
            byte[] receivePort = new byte[1000];
            socket.Receive(receivePort);
            byte[] arr2 = new byte[BytesInCollection(receivePort)];
            CopyFromTo(receivePort, arr2);
            string strPort = Encoding.ASCII.GetString(arr2);
            AcceptPort = int.Parse(strPort);
        }

        void WriteTopologyToFiles() {
            FileStream fstream = new FileStream(TOPOLOGY_FILENAME, FileMode.OpenOrCreate);
            SerializeJson(Neighbors, fstream);
            fstream.Close();

            WriteStream(ID_FILENAME, clientId);
            WriteStream(PORT_FILENAME, AcceptPort);

        }

        void ShowConfiguration() {
            PrintMessage($"Время генерации пакета (мс): {TimeGeneration}");
            PrintMessage($"Периодичность проверки узлов (мс): {TimeCheckingLost}");
            PrintMessage($"Id: {clientId}");
            PrintMessage($"Accept port: {AcceptPort}");
        }

        void ShowNeighbours() {
            PrintMessage("Полученная топология:");
            foreach (Neighbour u in Neighbors) {
                PrintMessage(u.Ip + ":" + u.AcceptPort + ", " + u.Priority);
            }
        }

        void SetNumberIncomingConnections() {
            int counter = 0;
            foreach (Neighbour neighbour in Neighbors) {
                if (neighbour.IsForReceiving()) {
                    counter++;
                }
            }
            numberIncomingConnections = counter;
        }

        void FindNeighbourForSending() {
            bool success = false;
            PrintMessage("Поиск узла отправки пакетов..");
            foreach (Neighbour n in Neighbors) {
                if ((n.IsForSending()) && (!n.IsDied)) {
                    currentNeighbourForSending = n;
                    PrintMessage($"Текущий узел - {currentNeighbourForSending.Priority}");
                    success = true;
                    sendingAvailable = true;
                    break;
                }
                else if ((n.IsBest()) && (n.IsDied) && (!CheckingDiedNodesThread.IsAlive)) {
                    PrintMessage("Запуск проверки узлов для восстановления..");
                    StartChecking();

                }
            }
            if (!success) {
                PrintMessage($"Нет доступных узлов для отправки.");
                sendingAvailable = false;
            }
        }

        bool HasInConnection() {
            foreach (Neighbour u in Neighbors) {
                if (u.IsForReceiving())
                    return true;
            }
            return false;
        }

        void CheckingLostNodes() {
            bool needChecking = true;
            while (needChecking) {
                Thread.Sleep(TimeCheckingLost);
                foreach (Neighbour n in Neighbors) {
                    if ((n.IsDied) && (n.IsForSending()) && (n.IsBetterThan(currentNeighbourForSending))) {
                        Socket sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        try {
                            var tcpEndPoint = new IPEndPoint(IPAddress.Parse(n.Ip), n.AcceptPort);
                            sendSocket.Connect(tcpEndPoint);
                            sendSocket.Send(new byte[0]);
                            n.IsDied = false;
                            needChecking = false;
                            sendSocket.Shutdown(SocketShutdown.Both);
                            sendSocket.Close();
                            FindNeighbourForSending();
                            break;
                        }
                        catch (Exception ex) {
                            PrintMessage(ex.Message);
                            continue;
                        }
                    }
                }
            }
            PrintMessage("Проверка узлов для восстановления окончена.");
        }
        
        List<DataPacket> CreateData(int packetsNumber) {
            List<DataPacket> result = new List<DataPacket>();

            for (int i = 0; i < packetsNumber; i++) {
                DataPacket newPacket = new DataPacket(clientId);
                result.Add(newPacket);
            }
            return result;
        }
        
        void Generation() {
            while (true) {
                Thread.Sleep(TimeGeneration);
                List<DataPacket> testpackets = CreateData(1);
                AddPacketsInSequence(testpackets);
                TrySendNewPackets();
            }
        }
        
        override protected void AddPacketsInSequence(List<DataPacket> packets) {
            lock (addSequenceLocker) {
                if (addPacketsInMainSequnce) {
                    packetsSequence.AddRange(packets);
                }
                else {
                    additionalPacketsSequence.AddRange(packets);
                }
            }
        }
        
        void TrySendNewPackets(List<DataPacket> packets = null) {
            if (sendingInProcess == false) {
                Monitor.Enter(sendLocker, ref sendingInProcess);
                if (sendingAvailable) {
                    SendingProccess();

                }
                else {
                    PrintMessage("Отправка пакетов остановлена, накоплено пакетов " + packetsSequence.Count);
                }
                Monitor.Exit(sendLocker);
                sendingInProcess = false;
            }
        }
        
        void SendingProccess() {
                if (packetsSequence.Count > 0) {
                    addPacketsInMainSequnce = false;
                    GetSendBuffer(out byte[] sendBuffer);
                    SendBuffer(sendBuffer, out bool successSending);
                    if (successSending) {
                        packetsSequence.Clear();
                    }
                    addPacketsInMainSequnce = true;
                    TransferNewDataFromAddPS();
                }
        }

        void GetSendBuffer(out byte[] sendBuffer) {
            MemoryStream memoryStream = new MemoryStream();
            SerializeJson(packetsSequence, memoryStream);
            sendBuffer = memoryStream.GetBuffer();
            memoryStream.Close();
        }

        void SendBuffer(byte[] sendBuffer, out bool successSending) {
            Socket sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try {
                var tcpEndPoint = new IPEndPoint(IPAddress.Parse(currentNeighbourForSending.Ip), currentNeighbourForSending.AcceptPort);
                sendSocket.Connect(tcpEndPoint);
                sendSocket.Send(sendBuffer);
                PrintMessage($"Пакет отправлен узлу {currentNeighbourForSending.Priority}");
                successSending = true;
                sendSocket.Shutdown(SocketShutdown.Both);
                sendSocket.Close();
            }
            catch (Exception ex) {
                PrintMessage(ex.Message);
                currentNeighbourForSending.IsDied = true;
                PrintMessage("Перевод маршрута...");
                FindNeighbourForSending();
                successSending = false;
            }
        }

        void TransferNewDataFromAddPS() {
            packetsSequence.AddRange(additionalPacketsSequence);
            additionalPacketsSequence.Clear();
        }
        
        void StartChecking() {
            CheckingDiedNodesThread = new Thread(CheckingLostNodes);
            CheckingDiedNodesThread.Start();
        }

        void StartReceiving() {
            Thread acceptThread = new Thread(ReceivingProccess);
            acceptThread.Start();
        }

        void StartGenerationData() {
            Thread generationThread = new Thread(Generation);
            generationThread.Start();
        }

        private const string TOPOLOGY_FILENAME = "topology.json";
        private const string ID_FILENAME = @"id.txt";
        private const string PORT_FILENAME = @"port.txt";
        private const string SERVER_IP_FILENAME = @"serverip.txt";
    }
}





