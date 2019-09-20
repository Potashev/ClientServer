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
    class Client {
        static object locker = new object();

        // TODO: потом у клиента айпи сервера убрать из кода:
        // либо ввод консоли, либо чтение файла

        string serverIp = "192.168.1.106";
        //string serverIp = "172.20.10.2";


        int acceptPort;
        int clientId;

        byte[] localMemory = new byte[10000];

        List<Unit> Neighbors = new List<Unit>();
        // список входящих сокетов (прием)
        List<AcceptConnectionInfo> InConnections = new List<AcceptConnectionInfo>();

        string topologyFileName = "topology.json";
        string idFileName = @"id.txt";
        string portFileName = @"port.txt";  // возможно потом объединить в 1 файл конфигурации


        public void Run() {
            if (File.Exists(topologyFileName)) {
                Console.WriteLine("Восстановление топологии");

                GetNeibsFromFile();
                GetUnitIdFromFile();
                GetAcceptPortFromFile();
            }
            else {
                Console.WriteLine("Подключение для топологии");
                GetTopologyFromServer();
            }

            Console.WriteLine("id: " + Convert.ToString(clientId));
            Console.WriteLine("port: " + Convert.ToString(acceptPort));

            ShowNeighbours();

            Console.WriteLine("Начать?");
            Console.ReadLine();

            // ПОДКЛЮЧЕНИЕ К СЛЕДУЮЩИМ КЛИЕНТАМ
            CreateOutConnections();


            // ПРОВЕРКА ВХОДЯЩИХ ПОДКЛЮЧЕНИЙ
            if (HasInConnection()) {
                Console.WriteLine("Есть входящие подключения");
                Thread acceptThread = new Thread(AcceptInConnections);
                acceptThread.Start();
            }
            else {
                Console.WriteLine("Входящих подключений нет");
            }

            // ГЕНЕРАЦИЯ ПАКЕТОВ ДАННЫХ
            // TODO: сделать обертку для генерации данных GetData()
            Thread generationThread = new Thread(Generation);
            generationThread.Start();


            // ОТПРАВКА ПАКЕТОВ СЛЕДУЮЩИМ КЛИЕНТАМ
            Thread sendThread = new Thread(Sending);
            sendThread.Start();
        }

        
        void AddToLocMem(byte[] locMem, byte[] buffer) {
            int i = 0;
            int j = 0;
            while (locMem[i] != 0) {
                i++;
            }
            while (j < buffer.Length) {
                locMem[i] = buffer[j];
                i++;
                j++;
            }
        }

        // TODO: возможно позже поменять на dataInCollection (либо подобное)
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
        
        bool HasInConnection() {
            foreach (Unit u in Neighbors) {
                if (u.priority == -1)
                    return true;
            }
            return false;
        }

        // ф-я сравнения для сортировки списка по возрастанию (-1, -1... 1, 2...)
        // мб найти другую сортировку ( 1, 2... -1, -1...)
        int CompareUnitsByPriority(Unit x, Unit y) {
            if (x.priority == y.priority)
                return 0;
            if (x.priority < y.priority)
                return -1;
            else
                return 1;
        }

        //static void Generation() {
        //    Console.Write("Генерация:");
        //    string str = Console.ReadLine();
        //    byte[] buffer = new byte[str.Length];
        //    buffer = Encoding.ASCII.GetBytes(str);
        //    AddToLocMem(localMemory, buffer);
        //}

        // TODO: возможно заменить, может добавить метод GetBytes в классе dataPacket
        byte[] ConvertPacket(DataPacket packet) {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DataPacket));
            MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, packet);
            byte[] bytesPacket = stream.GetBuffer();    //???
            stream.Close(); // возможно закрытие потока автоматом при завершении метода
            return bytesPacket;
        }

        void Generation() {
            while (true) {
                Console.WriteLine("Генерация...");
                Thread.Sleep(5000);
                DataPacket packet = new DataPacket(clientId);
                byte[] bytesPacket = ConvertPacket(packet);

                AddToLocMem(localMemory, bytesPacket);
            }
        }

        // TODO: посмотреть что общего с методом outconnect (возможно объеденить)
        void ConnectToServer(out Socket socket) {
            Console.WriteLine("Подключение к серверу для топологии..");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //serverSocket.Bind(new IPEndPoint(IPAddress.Any, 660));      // необязательно
            socket.Connect(serverIp, 999);
        }

        void GetNeibsFromServer(Socket socket) {
            Console.WriteLine("Прием топологии..");
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
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
            
            stream.Position = 0;    // необязательно?
            Neighbors = (List<Unit>)jsonSerializer.ReadObject(stream);

            stream.Close(); // TODO: проверить необходимость закрытия, ведь метод завершается
        }

        void GetUnitIdFromServer(Socket socket) {
            byte[] receiveId = new byte[1000];
            lock (locker) {
                // получение id
                socket.Receive(receiveId);


                byte[] arr = new byte[BytesInCollection(receiveId)];
                CopyFromTo(receiveId, arr);
                string strId = Encoding.ASCII.GetString(arr);
                Console.WriteLine("id:" + strId + "!");
                clientId = Int32.Parse(strId);
            }
        }

        void GetAcceptPortFromServer(Socket socket) {
            byte[] receivePort = new byte[1000];

            lock (locker) {
                socket.Receive(receivePort);
                byte[] arr2 = new byte[BytesInCollection(receivePort)];
                CopyFromTo(receivePort, arr2);
                string strPort = Encoding.ASCII.GetString(arr2);
                Console.WriteLine("port:" + strPort + "!");
                acceptPort = Convert.ToInt32(strPort);
            }
        }

        void WriteFiles() {
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
            FileStream fstream = new FileStream(topologyFileName, FileMode.OpenOrCreate);
            jsonSerializer.WriteObject(fstream, Neighbors);
            fstream.Close();

            StreamWriter unitIdFile = new StreamWriter(idFileName);
            unitIdFile.WriteLine(Convert.ToString(clientId));
            unitIdFile.Close();

            StreamWriter portFile = new StreamWriter(portFileName);
            portFile.WriteLine(Convert.ToString(acceptPort));
            portFile.Close();

        }

        void ShowNeighbours() {
            Console.WriteLine("Полученная топология:");
            foreach (Unit u in Neighbors) {
                Console.WriteLine(u.ip + ":" + u.acceptPort + ", " + u.priority);
            }
        }

        void GetTopologyFromServer() {
            ConnectToServer(out Socket serverSocket);
            GetNeibsFromServer(serverSocket);
            GetUnitIdFromServer(serverSocket);
            GetAcceptPortFromServer(serverSocket);
            serverSocket.Close();
            Neighbors.Sort(CompareUnitsByPriority); // TODO: посмотреть как работает данный вызов
            WriteFiles();
            //ShowNeighbours();
        }



        // подключение к следующим узлам 
        void CreateOutConnections() {
            foreach (Unit neighb in Neighbors) {
                if (neighb.priority > 0) {
                    neighb.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    neighb.socket.Connect(neighb.ip, neighb.acceptPort);
                }
            }
            Console.WriteLine("OutСonnect +");
        }

        void RunAcceptSoket(out Socket acceptSocket) {
            // запуск сокета приема
            acceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            acceptSocket.Bind(new IPEndPoint(IPAddress.Any, acceptPort));
            acceptSocket.Listen(1);             // у меня аргумент не влиял на размер очереди
        }

        void RunInConnection(Socket socket) {
            AcceptConnectionInfo connection = new AcceptConnectionInfo();
            connection.Socket = socket;
            connection.Thread = new Thread(ReceivingData);
            connection.Thread.IsBackground = true;
            InConnections.Add(connection);
            connection.Thread.Start(connection);
        }

        // прием новых подключений и и прослушка от подключившихся
        void AcceptInConnections() {
            //// запуск сокета приема
            //Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //listenSocket.Bind(new IPEndPoint(IPAddress.Any, acceptPort));
            //listenSocket.Listen(1);             // у меня аргумент не влиял на размер очереди

            RunAcceptSoket(out Socket acceptSocket);

            while (true) {

                // принять новое подключение
                Socket socket = acceptSocket.Accept();
                Console.WriteLine("Новое подключение");

                //запуск нового подключения
                // возможно не оборачивать в метод тк поток может заглохнуть

                //AcceptConnectionInfo connection = new AcceptConnectionInfo();
                //connection.Socket = socket;
                //connection.Thread = new Thread(ProcessInConnection);
                //connection.Thread.IsBackground = true;
                //connection.Thread.Start(connection);
                //InConnections.Add(connection);

                RunInConnection(socket);
            }
        }

        // прием пакетов
        void ReceivingData(object state) {
            AcceptConnectionInfo connection = (AcceptConnectionInfo)state;
            byte[] buffer = new byte[255];
            //try {
                while (true) {
                    int bytesRead = connection.Socket.Receive(buffer);

                    // возможно if убрать тк receive подразумевает, что пакет не пустой
                    // или проверить входящие данные на соответствие формату (DataIsChecked())
                    if (bytesRead > 0) {

                        // TODO: убрать из параметра localmem
                        AddToLocMem(localMemory, buffer);
                        Console.WriteLine("Прием+");
                    }
                }
            //}
            //catch (SocketException exc) {
            //    Console.WriteLine("Socket exception: " +
            //        exc.SocketErrorCode);
            //}
            //catch (Exception exc) {
            //    Console.WriteLine("Exception: " + exc);
            //}
            //finally {
            //    //connection.Socket.Close();
            //    InConnections.Remove(connection);
            //}
        }

        int GetBytesCountInLocMem() {
            int bytesCount = 0;
            while (localMemory[bytesCount] != 0) {
                bytesCount++;
            }
            return bytesCount;
        }

        byte[] GetBufferForSending(int bufferSize) {
            byte[] buffer = new byte[bufferSize];
            CopyFromTo(localMemory, buffer);
            return buffer;
        }

        void TryLifeUpConnection(Unit neighbour, out bool successLifeUp) {
            lock (locker) {
                try {
                    //Console.Write("Попытка восстановить соединение: ");
                    neighbour.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    neighbour.socket.Connect(neighbour.ip, neighbour.acceptPort);
                    neighbour.died = false;
                    //Console.WriteLine("восстановлено");
                    successLifeUp = true;
                }
                catch (Exception ex) {
                    //Console.WriteLine("не восстановлено");
                    successLifeUp = false;
                }
            }
        }

        void TrySend(byte[] buffer, Unit neighbour, ref bool successSend) {
            try {
                neighbour.socket.Send(buffer);
                successSend = true;
            }
            catch (Exception ex) {
                //neighb.activity = false;
                neighbour.died = true;
                successSend = false;
                //neighb.socket.Shutdown();
                Console.WriteLine("Перевод маршрута");
            }
        }

        // отправка пакетов
        void Sending() {
            // TODO: возможно убрать ВСЕ циклы while(true) и попроб заменить на события
            while (true) {
                int dataBytesInLocMemory = GetBytesCountInLocMem();
                if (dataBytesInLocMemory > 0) {
                    byte[] sendBuffer = GetBufferForSending(dataBytesInLocMemory);
                    bool successSend = false;
                    foreach (Unit neighb in Neighbors) {
                        if (neighb.priority > 0) {
                            if (neighb.died) {
                                Console.Write("Попытка восстановить соединение: ");
                                TryLifeUpConnection(neighb, out bool successLifeUp);
                                if (successLifeUp) {
                                    Console.WriteLine("восстановлено");
                                }
                                else {
                                    Console.WriteLine("не восстановлено");
                                }
                            }

                            TrySend(sendBuffer, neighb, ref successSend);
                            if (successSend) {
                                Console.WriteLine("Передача+");
                                // TODO: записать номер пакета в файл
                                Array.Clear(localMemory, 0, dataBytesInLocMemory - 1);
                                break;

                            }
                            else {
                                Console.WriteLine("Перевод маршрута");
                                continue;
                            }
                        }
                    }

                    if (!successSend) {
                        Console.WriteLine("Пакет отправить не удалось");
                        Thread.Sleep(5000);
                    }
                }
                Thread.Sleep(1);       // перестает есть проц (с 25% до 0)
            }
        }

        //void FileWriteIdAndPort() {
        //    //FileStream fs = File.Create(@"Id.txt");
        //    StreamWriter textFile = new StreamWriter(idFileName);
        //    textFile.WriteLine(Convert.ToString(myId));
        //    textFile.Close();

        //    StreamWriter PortFile = new StreamWriter(portFileName);
        //    PortFile.WriteLine(Convert.ToString(myAcceptPort));
        //    PortFile.Close();
        //}

        void GetNeibsFromFile() {
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
            FileStream fsTopology = new FileStream(topologyFileName, FileMode.OpenOrCreate);
            Neighbors = (List<Unit>)jsonSerializer.ReadObject(fsTopology);
            //fsTopology.Close();
        }

        void GetUnitIdFromFile() {
            StreamReader fsId = new StreamReader(idFileName);
            clientId = Convert.ToInt32(fsId.ReadLine());
            fsId.Close();
        }
        void GetAcceptPortFromFile() {
            StreamReader fsPort = new StreamReader(portFileName);
            acceptPort = Convert.ToInt32(fsPort.ReadLine());
            fsPort.Close();
        }
    }

    // UNIT - класс родитель для клиента и сервера
    // включает:
    // метод 
    //ByteInCollection
    //CopyFromTo
    // установка входящего соединения (AcceptConncetion)
    // процесс приема пакета (ProccessConnection): без распаковки, 
    //где сервер дальше распаковывает, а клиент добавляет в locmemory и передает дальше
    // публичный метод Run

}
