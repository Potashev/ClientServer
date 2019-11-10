using ClientServerLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientProject {

    class Client : Node {
        int clientId;

        private int timeGeneration;
        private int timeCheckingDied;

        private bool addPacketsInMainSequnce;
        private bool sendingAvailable;

        int SENDTIME;       // временное - в конце убрать

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
        public int TimeCheckingDied {
            get {
                return timeCheckingDied;
            }
            set {
                if (value > 0) {
                    timeCheckingDied = value;
                }
                else {
                    timeCheckingDied = 5000;
                }
            }
        }

        

        //TODO: при анализе sending'а проверить, в надо ли addseq
        List<DataPacket> additionalPacketsSequence = new List<DataPacket>();

        // TODO: мб поднять на node, чтобы у сервера была топология входящих подключений (чтоб не пускать других)
        List<Neighbour> Neighbors = new List<Neighbour>();

        // TODO: мб активного соседа инкапсулировать в класс Neighbour, и проволить манипуляции либо методами (static) либо также статич переменной neib
        Neighbour currentNeighbourForSending;

        // TODO: мб перенести на более актуальную реализацию через task или async
        Thread checkDiedNodeThread;


        // TODO: возможно потом объединить в 1 файл конфигурации
        string topologyFileName = "topology.json";
        string idFileName = @"id.txt";
        string portFileName = @"port.txt";

        bool isLocked;

        public Client(InputDelegate userInput, OutputDelegate userOutput, int generationTime, int checkDiedTime, int TIME) : base(userInput, userOutput) {

            

            TimeGeneration = generationTime;
            TimeCheckingDied = checkDiedTime;
            //this.generationTime = CheckInputData(generationTime.ToString(), x => x >= 0);   // TODO: подумать, как упростить ввод и проверку genTime,
            checkDiedNodeThread = new Thread(CheckingDiedNodes);
            addPacketsInMainSequnce = true;
            eventPacketSequenceAdded += TrySendNewPackets;

            SENDTIME = TIME;


            isLocked = false;
        }

        public Client(InputDelegate userInput, OutputDelegate userOutput) : base(userInput, userOutput) {
            
            TimeGeneration = GetInputData(x=>x>0, "Время генерации пакета: ");
            TimeCheckingDied = 10000;
            //this.generationTime = CheckInputData(generationTime.ToString(), x => x >= 0);   // TODO: подумать, как упростить ввод и проверку genTime,
            checkDiedNodeThread = new Thread(CheckingDiedNodes);
            addPacketsInMainSequnce = true;
            eventPacketSequenceAdded += TrySendNewPackets;

            SENDTIME = 0;

        }


        

        void SetNumberIncomingConnections() {
            int counter = 0;
            foreach(Neighbour neighbour in Neighbors) {
                if (neighbour.IsForReceiving()) {
                    counter++;
                }
            }
            PrintMessage($"Число входящих - {counter}");
            numberIncomingConnections = counter;
        }

        public override void Run() {

            if (File.Exists(topologyFileName)) {
                PrintMessage("Восстановление топологии");
                GetTopologyFromFile();  // TODO: проверить runtimeex у чтения файлов
            }
            else {
                PrintMessage("Подключение к серверу для получения топологии");
                GetTopologyFromServer();
            }
            
            //PrintMessage($"id: {clientId}");
            //PrintMessage($"port: {AcceptPort}");

            ShowConfiguration();
            ShowNeighbours();

            SetNumberIncomingConnections();

            FindNeighbourForSending();

            PrintMessage("Начать?");
            InputData();
            
            if (HasInConnection()) {
                //PrintMessage("ГЕНЕРАЦИИ НЕ БУДЕТ");

                PrintMessage("Есть входящие подключения");

                //Thread acceptThread = new Thread(ReceivingProccess);
                //acceptThread.Start();
                StartReceiving();

            }
            else {
                PrintMessage("Входящих подключений нет");
            }

            //TODO: сделать обертку для генерации данных GetData()
            //Thread generationThread = new Thread(Generation);
            //generationThread.Start();

            StartGenerationData();

        }

        void ShowConfiguration() {
            PrintMessage($"time generation : {TimeGeneration}");
            PrintMessage($"time checking for live up: {TimeCheckingDied}");
            PrintMessage($"id: {clientId}");
            PrintMessage($"port: {AcceptPort}");
        }

        void CheckingDiedNodes() {
            bool needChecking = true;
            while (needChecking) {
                PrintMessage($"ПРОВЕРКА МЕРТвячинки потоком {Thread.CurrentThread.ManagedThreadId}");
                Thread.Sleep(TimeCheckingDied);
                foreach (Neighbour n in Neighbors) {
                    if ((n.IsDied) && (n.IsForSending()) && (n.IsBetterThan(currentNeighbourForSending)/*n.Priority <= currentNeighbourForSending.Priority*/)) {

                        PrintMessage($"ПРОВЕРКА СОСЕДА С ПРИОРИТЕТОМ {n.Priority}...");

                        Socket sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        try {
                            var tcpEndPoint = new IPEndPoint(IPAddress.Parse(n.Ip), n.AcceptPort);
                            //PrintMessage("CONNECTING..");
                            sendSocket.Connect(tcpEndPoint);
                            //PrintMessage("CONNECT+");
                            sendSocket.Send(new byte[0]);   // TODO: попроб передавать пустой массив new byte[]
                            n.IsDied = false;
                            //FindNeighbourForSending();
                            
                            //PrintMessage("СОЕДИНЕНИЕ ВОССТАНОВЛЕНО");

                            // ПОПРОБОВАТЬ ЧИСТИТЬ ОЧЕРЕДЬ ПАКЕТОВ В СЛУЧАЕ ВОССТАНОВЛЕНИЯ

                            needChecking = false;
                            sendSocket.Shutdown(SocketShutdown.Both);
                            sendSocket.Close();
                            PrintMessage("СОКЕТ ЗАКРЫТ, МОЖНО ДЕЛАТЬ ОТПРАВКУ");
                            //currentNeighbourForSending = n;
                            FindNeighbourForSending();
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

        
        
        bool HasInConnection() {
            foreach (Neighbour u in Neighbors) {
                if (u.IsForReceiving())
                    return true;
            }
            return false;
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

        
        // TODO: в конце поднять работу generation в main. 
        // Т.е работа не потока внутри класса, а внешняя генерация данных извне и передача уже в Client
        void Generation() {
            while (true) {
                Thread.Sleep(TimeGeneration);

                lock (locker) {
                    //DataPacket newPacket = new DataPacket(clientId);
                    //// 1 генерю пакет
                    //// 2 передаю его
                    //// 3 если не смог передать добавляю в packetSeq
                    //packetsSequence.Add(newPacket);

                    List<DataPacket> testpackets = CREATETESTS(1);

                    AddPacketsInSequence(testpackets);
                    //packetsSequence.AddRange(testpackets);

                    //PacketSequenceAdded(testpackets);
                    //PrintMessage("НАЧАЛО MAIN");
                    
                    TrySendNewPackets();
                    //PrintMessage("КОНЕЦ MAIN!!!!");
                }
                //SendingProccess();
            }
        }

       

        

        void ShowNeighbours() {
            PrintMessage("Полученная топология:");
            foreach (Neighbour u in Neighbors) {
                PrintMessage(u.Ip + ":" + u.AcceptPort + ", " + u.Priority);
            }
        }

        

        //byte[] GetBufferForSending() {

        //    //MemoryStream memoryStream = new MemoryStream();
        //    //SerializeJson(neibs, memoryStream);
        //    //buffer = memoryStream.GetBuffer();
        //    //memoryStream.Close();

        //    // БЛОКИРОВКА ТЕСТОВАЯ
        //    byte[] bytesPacket;
        //    lock (locker) {
        //        bytesPacket = DataPacket.GetBytes(packetsSequence);
        //    }
        //    return bytesPacket;
        //}

        //void TryLifeUpConnection(Neighbour neighbour, out bool successLifeUp) {
        //    lock (locker) {
        //        try {
        //            //Console.Write("Попытка восстановить соединение: ");
        //            neighbour.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //            neighbour.Socket.Connect(neighbour.Ip, neighbour.AcceptPort);
        //            //Console.WriteLine("восстановлено");
        //            successLifeUp = true;
        //        }
        //        catch (Exception ex) {
        //            //Console.WriteLine("не восстановлено");
        //            successLifeUp = false;
        //        }
        //    }
        //}

        //void TrySend(byte[] buffer, Neighbour neighbour, ref bool successSend) {
        //    try {
        //        neighbour.Socket.Send(buffer);
        //        successSend = true;
        //    }
        //    catch (Exception ex) {
        //        //neighb.activity = false;
        //        successSend = false;
        //        //neighb.socket.Shutdown();
        //    }
        //}

        //1 пробуем подключиться
        //2 если не смогли - меняем currentNeib
        //void SendingProccess() {
        //    Socket sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //    // 1 ЗАПРЕЩАЕМ ДОБАВЛЕНИЕ В PACKETSEQ
        //    // 2 ВЫЗЫВАЕМ GETBUFFERFORS..
        //    // 3 ОЧИЩАЕМ PACKETSEQ
        //    // 4 СНИМАЕМ ЗАПРЕТ НА ДОББАВЛЕНИЕ
        //    // ** это нужно на случай, что между getbuf и packets.clear придут пакеты, которые затрутся
        //    byte[] sendBuffer = GetBufferForSending();
        //    packetsSequence.Clear();    // на данном этапе в случае перевода маршрута, пакеты записанные в sendbuf теряются
        //    try {
        //        //if (currentNeighbourForSending.died == false) {
        //            var tcpEndPoint = new IPEndPoint(IPAddress.Parse(currentNeighbourForSending.ip), currentNeighbourForSending.acceptPort);
        //        //PrintMessage("conncet for sending..");
        //            sendSocket.Connect(tcpEndPoint);
        //        //PrintMessage("conncet for sending+");
        //        sendSocket.Send(sendBuffer);
        //            PrintMessage($"Пакет отправлен узлу {currentNeighbourForSending.priority}");
        //            sendSocket.Shutdown(SocketShutdown.Both);
        //            sendSocket.Close();
        //        //}
        //    }
        //    catch (Exception ex) {
        //        PrintMessage(ex.Message);
        //        currentNeighbourForSending.died = true;
        //        PrintMessage("Перевод маршрута...");
        //        FindNeighbourForSending();
        //    }
        //    finally {
        //        //TODO: проверить нужно ли проводить процедуры выключения и закрытия если соединение не было установлено
        //        //sendSocket.Shutdown(SocketShutdown.Both);
        //        //sendSocket.Close();
        //    }
        //}

        // TODO: разобраться с модификатором
        override protected void AddPacketsInSequence(List<DataPacket> packets) {
            // TODO: ДОБАВИТЬ LOCK
            lock (locker) {
                if (addPacketsInMainSequnce) {
                    packetsSequence.AddRange(packets);
                }
                else {
                    PrintMessage($"ДОБАВЛЯЕМ ПАКЕТЫ В ADDSEQ {packets.Count} пакетов !!!!!");
                    additionalPacketsSequence.AddRange(packets);
                }
            }
            //Thread.Sleep(1);
        }

        // метод заглушка для обработки события на клиенте
        void TrySendNewPackets(List<DataPacket> packets = null) {
                if (sendingAvailable) {
                    SendingProccess();

                }
                else {
                    PrintMessage("Отправка пакетов остановлена, накоплено пакетов " + packetsSequence.Count);
                }
        }

        static protected object locker2 = new object();

        void SendingProccess() {
            if(isLocked == false) { 
            Monitor.Enter(locker2, ref isLocked);
                if (packetsSequence.Count > 0) {
                    addPacketsInMainSequnce = false;
                    GetSendBuffer(out byte[] sendBuffer);
                    SendBuffer(sendBuffer, out bool successSending);
                    //PrintMessage("Число пакетов в PS после отправки " + packetsSequence.Count);
                    if (successSending) {
                        packetsSequence.Clear();
                    }
                    addPacketsInMainSequnce = true;
                    TransferNewDataFromAddPS();
                }
                Monitor.Exit(locker2);
                isLocked = false;
            }
        }

        void GetSendBuffer(out byte[] sendBuffer) {
            MemoryStream memoryStream = new MemoryStream();
            SerializeJson(packetsSequence, memoryStream);
            sendBuffer = memoryStream.GetBuffer();
            memoryStream.Close();

            //sendBuffer = GetBufferForSending();
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
                successSending = false; // TODO: проверить ветку неудавшейся отправки (присваивания successSending false)
            }
        }

        void TransferNewDataFromAddPS() {
            PrintMessage("число пакетов addseq " + additionalPacketsSequence.Count);
            packetsSequence.AddRange(additionalPacketsSequence);
            additionalPacketsSequence.Clear();
        }
        
        void FindNeighbourForSending() {
            bool success = false;
            PrintMessage("ПОИСК СОСЕДА ДЛЯ ОТПРАВКИ");
            foreach (Neighbour n in Neighbors) {
                if ((n.IsForSending()) && (!n.IsDied)) {
                    currentNeighbourForSending = n;
                    PrintMessage($"ТЕКУЩИЙ СОСЕД - {currentNeighbourForSending.Priority}");
                    success = true;
                    sendingAvailable = true;
                    break;
                }
                else if ((n.IsBest()) && (n.IsDied) && (!checkDiedNodeThread.IsAlive)) {
                    PrintMessage("ITS NOT ALIVE !!!");
                    PrintMessage("ВКЛЮЧЕНИЕ ПРОВЕРКИ..");
                    StartChecking();

                }
            }
            if (!success) {
                PrintMessage($"Нет доступных каналов отправки");
                sendingAvailable = false;
            }
        }

        //TODO: посмотреть, возможно лучше убрать метод и запускать поток из findNeibs
        void StartChecking() {

            checkDiedNodeThread = new Thread(CheckingDiedNodes);
            checkDiedNodeThread.Start();
        }

        void StartReceiving() {
            Thread acceptThread = new Thread(ReceivingProccess);
            acceptThread.Start();
        }

        void StartGenerationData() {
            Thread generationThread = new Thread(Generation);
            generationThread.Start();
        }

        void GetTopologyFromFile() {
            //try {
                FileStream fsReading = new FileStream(topologyFileName, FileMode.OpenOrCreate);
                Neighbors = DeserializeJson<Neighbour>(fsReading);
                fsReading.Close();  // нужно ли закрывать?

                clientId = ReadStream(idFileName);
                AcceptPort = ReadStream(portFileName);
            //}
            //catch(Exception ex) {
            //    PrintMessage(ex.Message);
            //}
        }

        void GetTopologyFromServer() {
            try {   //TODO: мб try перенести пониже - в методы, хотя если воспринимать получение топологии как транзакцию, то оставить как есть
                ConnectToServer(out Socket serverSocket);
                GetNeibsFromServer(serverSocket);
                GetUnitIdFromServer(serverSocket);
                GetAcceptPortFromServer(serverSocket);
                serverSocket.Close();
                Neighbors.Sort(Neighbour.CompareUnitsByPriority); // TODO: возможно сортировку перенести на сервер перед отправкой
                WriteTopologyToFiles();
            }
            catch(Exception ex) {
                //PrintMessage("Соединение с сервером было потеряно.");
                PrintMessage(ex.Message);
            }
        }

        void ConnectToServer(out Socket socket) {
            //PrintMessage("Подключение к серверу для топологии..");
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

            MemoryStream memoryStream = new MemoryStream(streamBuffer);
            Neighbors = DeserializeJson<Neighbour>(memoryStream);
            memoryStream.Close(); // TODO: проверить необходимость закрытия, ведь метод завершается
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

        void WriteTopologyToFiles() {
            FileStream fstream = new FileStream(topologyFileName, FileMode.OpenOrCreate);
            SerializeJson(Neighbors, fstream);
            fstream.Close();

            WriteStream(idFileName, clientId);
            WriteStream(portFileName, AcceptPort);

        }
    }
}





