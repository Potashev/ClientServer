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

namespace ClientProject {
    class Client : Node {
        // TODO: мб сделать id в node, и по нему смотреть - сервер node или клиент
        int clientId;

        int generationTime;
        int diedCHeckingTime = 10000;

        // TODO: мб поднять на node, чтобы у сервера была топология входящих подключений (чтоб не пускать других)
        List<Neighbour> Neighbors = new List<Neighbour>();

        // TODO: мб активного соседа инкапсулировать в класс Neighbour, и проволить манипуляции либо методами (static) либо также статич переменной neib
        Neighbour currentNeighbourForSending;

        Thread checkDiedNodeThread;
        // альтернативы:
        // timer
        // task и CancellationToken для отмены операции


        // TODO: возможно потом объединить в 1 файл конфигурации
        string topologyFileName = "topology.json";
        string idFileName = @"id.txt";
        string portFileName = @"port.txt";


        // TODO: дописать
        public Client(InputDelegate userInput, OutputDelegate userOutput, int generationTime) : base(userInput, userOutput) {

            PacketSequenceAdded += SendNewPackets;
            this.generationTime = generationTime;


            checkDiedNodeThread = new Thread(CheckingDiedNodes);
        }

        void SendNewPackets(List<DataPacket> packets) {
            SendingProccess();
        }

        public override void Run() {

            if (File.Exists(topologyFileName)) {
                PrintMessage("Восстановление топологии");
                GetTopologyFromFile();
            }
            else {
                PrintMessage("Подключение к серверу для получения топологии");
                GetTopologyFromServer();
            }
            
            PrintMessage($"id: {clientId}");
            PrintMessage($"port: {AcceptPort}");

            ShowNeighbours();
            FindNeighbourForSending();

            PrintMessage("Начать?");
            InputData();
            
            if (HasInConnection()) {
                PrintMessage("Есть входящие подключения");
                Thread acceptThread = new Thread(ReceivingProccess);
                acceptThread.Start();
            }
            else {
                PrintMessage("Входящих подключений нет");
            }
            
            // TODO: сделать обертку для генерации данных GetData()
            Thread generationThread = new Thread(Generation);
            generationThread.Start();
            
        }

        void CheckingDiedNodes() {
            bool needChecking = true;
            while (needChecking) {
                PrintMessage($"ПРОВЕРКА МЕРТвячинки потоком {Thread.CurrentThread.ManagedThreadId}");
                Thread.Sleep(diedCHeckingTime);
                foreach (Neighbour n in Neighbors) {
                    if ((n.died) && (n.priority > 0) && (n.priority <= currentNeighbourForSending.priority)) {

                        PrintMessage($"ПРОВЕРКА СОСЕДА С ПРИОРИТЕТОМ {n.priority}...");

                        Socket sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        try {
                            var tcpEndPoint = new IPEndPoint(IPAddress.Parse(n.ip), n.acceptPort);
                            //PrintMessage("CONNECTING..");
                            sendSocket.Connect(tcpEndPoint);
                            //PrintMessage("CONNECT+");
                            sendSocket.Send(new byte[0]);   // TODO: попроб передавать пустой массив new byte[]
                            n.died = false;
                            //FindNeighbourForSending();
                            
                            //PrintMessage("СОЕДИНЕНИЕ ВОССТАНОВЛЕНО");

                            // ПОПРОБОВАТЬ ЧИСТИТЬ ОЧЕРЕДЬ ПАКЕТОВ В СЛУЧАЕ ВОССТАНОВЛЕНИЯ

                            needChecking = false;
                            sendSocket.Shutdown(SocketShutdown.Both);
                            sendSocket.Close();
                            PrintMessage("СОКЕТ ЗАКРЫТ, МОЖНО ДЕЛАТЬ ОТПРАВКУ");
                            currentNeighbourForSending = n;
                            PrintMessage("СОЕДИНЕНИЕ ВОССТАНОВЛЕНО =)");
                            break;

                        }
                        catch (Exception ex) {
                            PrintMessage("СОЕДИНЕНИЕ ВОССТАНОВИТЬ НЕ ВЫШЛО");
                            PrintMessage(ex.Message);
                            continue;       // TODO: проверить
                        }
                        finally {
                            //TODO: проверить нужно ли проводить процедуры выключения и закрытия если соединение не было установлено
                            //sendSocket.Shutdown(SocketShutdown.Both);
                            //sendSocket.Close();
                        }
                    }
                }
            }
            PrintMessage("ПРОВЕРКА МЕРТВЯЧИНКИ ОКОНЧЕНА!!!");
        }

        void GetTopologyFromFile() {
            GetNeibsFromFile();
            GetUnitIdFromFile();
            GetAcceptPortFromFile();
        }

        bool HasInConnection() {
            foreach (Neighbour u in Neighbors) {
                if (u.priority == -1)   //TODO: не работать с приоритетом напрямую
                    return true;
            }
            return false;
        }

        // ф-я сравнения для сортировки списка по возрастанию (-1, -1... 1, 2...)
        // мб найти другую сортировку ( 1, 2... -1, -1...)
        int CompareUnitsByPriority(Neighbour x, Neighbour y) {
            if (x.priority == y.priority)
                return 0;
            if (x.priority < y.priority)
                return -1;
            else
                return 1;
        }

        // временный метод
        List<DataPacket> CREATETESTS(int cnt) {
            List<DataPacket> result = new List<DataPacket>();

            for (int i = 0; i < cnt; i++) {
                DataPacket newPacket = new DataPacket(clientId);
                result.Add(newPacket);
            }
            return result;
        }

        
        void Generation() {
            while (true) {
                Thread.Sleep(generationTime);

                lock (locker) {
                    //DataPacket newPacket = new DataPacket(clientId);
                    //// 1 генерю пакет
                    //// 2 передаю его
                    //// 3 если не смог передать добавляю в packetSeq
                    //packetsSequence.Add(newPacket);

                    List<DataPacket> testpackets = CREATETESTS(1);
                    packetsSequence.AddRange(testpackets);
                }
                SendingProccess();
            }
        }

        
        void ConnectToServer(out Socket socket) {
            PrintMessage("Подключение к серверу для топологии..");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //serverSocket.Bind(new IPEndPoint(IPAddress.Any, 660));      // необязательно
            socket.Connect(SERVER_IP, SERVER_PORT_FOR_TOPOLOGY);
        }

        void GetNeibsFromServer(Socket socket) {
            PrintMessage("Прием топологии..");
            //DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
            byte[] neighborsBuffer = new byte[1000];

            // TODO: разобраться с lock'ом (возможно убрать)
            lock (locker) {
                socket.Receive(neighborsBuffer);
            }

            // нужен чтобы убрать пустой хвост в буфере
            byte[] streamBuffer = new byte[BytesInCollection(neighborsBuffer)];
            CopyFromTo(neighborsBuffer, streamBuffer);
            MemoryStream stream = new MemoryStream(streamBuffer);
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Neighbour>));

            stream.Position = 0;    // необязательно?
            Neighbors = (List<Neighbour>)jsonSerializer.ReadObject(stream);

            stream.Close(); // TODO: проверить необходимость закрытия, ведь метод завершается
        }

        void GetUnitIdFromServer(Socket socket) {
            byte[] receiveId = new byte[1000];
            lock (locker) {
                socket.Receive(receiveId);


                byte[] arr = new byte[BytesInCollection(receiveId)];
                CopyFromTo(receiveId, arr);
                string strId = Encoding.ASCII.GetString(arr);
                clientId = int.Parse(strId);
            }
        }

        void GetAcceptPortFromServer(Socket socket) {
            byte[] receivePort = new byte[1000];

            lock (locker) {
                socket.Receive(receivePort);
                byte[] arr2 = new byte[BytesInCollection(receivePort)];
                CopyFromTo(receivePort, arr2);
                string strPort = Encoding.ASCII.GetString(arr2);
                AcceptPort = int.Parse(strPort);
            }
        }

        void WriteFiles() {
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Neighbour>));
            FileStream fstream = new FileStream(topologyFileName, FileMode.OpenOrCreate);
            jsonSerializer.WriteObject(fstream, Neighbors);
            fstream.Close();

            StreamWriter unitIdFile = new StreamWriter(idFileName);
            unitIdFile.WriteLine(Convert.ToString(clientId));
            unitIdFile.Close();

            StreamWriter portFile = new StreamWriter(portFileName);
            portFile.WriteLine(Convert.ToString(AcceptPort));
            portFile.Close();

        }

        void ShowNeighbours() {
            PrintMessage("Полученная топология:");
            foreach (Neighbour u in Neighbors) {
                PrintMessage(u.ip + ":" + u.acceptPort + ", " + u.priority);
            }
        }

        void GetTopologyFromServer() {
            ConnectToServer(out Socket serverSocket);
            GetNeibsFromServer(serverSocket);
            GetUnitIdFromServer(serverSocket);
            GetAcceptPortFromServer(serverSocket);
            //serverSocket.Close();
            CloseSocket(serverSocket);

            Neighbors.Sort(CompareUnitsByPriority); // TODO: возможно сортировку перенести на сервер перед отправкой
            WriteFiles();
        }

        byte[] GetBufferForSending() {

            // БЛОКИРОВКА ТЕСТОВАЯ
            byte[] bytesPacket;
            lock (locker) {
                bytesPacket = DataPacket.GetBytes(packetsSequence);
            }
            return bytesPacket;
        }

        void TryLifeUpConnection(Neighbour neighbour, out bool successLifeUp) {
            lock (locker) {
                try {
                    //Console.Write("Попытка восстановить соединение: ");
                    neighbour.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    neighbour.socket.Connect(neighbour.ip, neighbour.acceptPort);
                    //Console.WriteLine("восстановлено");
                    successLifeUp = true;
                }
                catch (Exception ex) {
                    //Console.WriteLine("не восстановлено");
                    successLifeUp = false;
                }
            }
        }

        void TrySend(byte[] buffer, Neighbour neighbour, ref bool successSend) {
            try {
                neighbour.socket.Send(buffer);
                successSend = true;
            }
            catch (Exception ex) {
                //neighb.activity = false;
                successSend = false;
                //neighb.socket.Shutdown();
            }
        }

        //1 пробуем подключиться
        //2 если не смогли - меняем currentNeib
        void SendingProccess() {
            Socket sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 1 ЗАПРЕЩАЕМ ДОБАВЛЕНИЕ В PACKETSEQ
            // 2 ВЫЗЫВАЕМ GETBUFFERFORS..
            // 3 ОЧИЩАЕМ PACKETSEQ
            // 4 СНИМАЕМ ЗАПРЕТ НА ДОББАВЛЕНИЕ
            // ** это нужно на случай, что между getbuf и packets.clear придут пакеты, которые затрутся
            byte[] sendBuffer = GetBufferForSending();
            packetsSequence.Clear();    // на данном этапе в случае перевода маршрута, пакеты записанные в sendbuf теряются
            try {
                //if (currentNeighbourForSending.died == false) {
                    var tcpEndPoint = new IPEndPoint(IPAddress.Parse(currentNeighbourForSending.ip), currentNeighbourForSending.acceptPort);
                //PrintMessage("conncet for sending..");
                    sendSocket.Connect(tcpEndPoint);
                //PrintMessage("conncet for sending+");
                sendSocket.Send(sendBuffer);
                    PrintMessage($"Пакет отправлен узлу {currentNeighbourForSending.priority}");
                    sendSocket.Shutdown(SocketShutdown.Both);
                    sendSocket.Close();
                //}
            }
            catch (Exception ex) {
                PrintMessage(ex.Message);
                currentNeighbourForSending.died = true;
                PrintMessage("Перевод маршрута...");
                FindNeighbourForSending();
            }
            finally {
                //TODO: проверить нужно ли проводить процедуры выключения и закрытия если соединение не было установлено
                //sendSocket.Shutdown(SocketShutdown.Both);
                //sendSocket.Close();
            }
        }



        void FindNeighbourForSending() {
            bool success = false;
            PrintMessage("ПОИСК СОСЕДА ДЛЯ ОТПРАВКИ");
            foreach (Neighbour n in Neighbors) {
                // TODO: заменить явную манипуляцию с приоритетом соседей, т.е
                // n.priority > 0 положить в функцию IsNeighbourForSending
                if ((n.priority > 0) && (!n.died)) {
                    currentNeighbourForSending = n;
                    PrintMessage($"ТЕКУЩИЙ СОСЕД - {currentNeighbourForSending.priority}");
                    success = true;
                    break;
                }
                else if ((n.priority == 1) && (n.died) && (!checkDiedNodeThread.IsAlive)) {
                    PrintMessage("ITS NOT ALIVE !!!");
                    PrintMessage("ВКЛЮЧЕНИЕ ПРОВЕРКИ..");
                    StartChecking();

                }
            }
            if (!success) {
                PrintMessage($"Нет доступных каналов отправки");
                // TODO: в данном случае подумать над генерацией события, которое стоппит отправку
            }
        }

        void StartChecking() {

            checkDiedNodeThread = new Thread(CheckingDiedNodes);
            checkDiedNodeThread.Start();
        }

        void GetNeibsFromFile() {
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Neighbour>));
            FileStream fsTopology = new FileStream(topologyFileName, FileMode.OpenOrCreate);
            Neighbors = (List<Neighbour>)jsonSerializer.ReadObject(fsTopology);
            //fsTopology.Close();
        }

        void GetUnitIdFromFile() {
            StreamReader fsId = new StreamReader(idFileName);
            clientId = Convert.ToInt32(fsId.ReadLine());
            fsId.Close();
        }
        void GetAcceptPortFromFile() {
            StreamReader fsPort = new StreamReader(portFileName);
            AcceptPort = Convert.ToInt32(fsPort.ReadLine());
            fsPort.Close();
        }
    }
}





