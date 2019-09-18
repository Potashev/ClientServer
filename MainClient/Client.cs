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

        string serverIp = "192.168.1.104";
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
            Console.WriteLine("Начать?");
            Console.ReadLine();

            // ПОДКЛЮЧЕНИЕ К СЛЕДУЮЩИМ КЛИЕНТАМ
            OutConnect();


            // ПРОВЕРКА ВХОДЯЩИХ ПОДКЛЮЧЕНИЙ
            if (!NoAccept()) {
                Console.WriteLine("Есть входящие подключения");
                Thread acceptThread = new Thread(Accept);
                // ПОДКЛЮЧЕНИЕ ВХОДЯЩИХ
                acceptThread.Start();
            }
            else {
                Console.WriteLine("Входящих подключений нет");
                //Generation();
            }

            // ГЕНЕРАЦИЯ ПАКЕТОВ ДАННЫХ
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
        
        bool NoAccept() {
            foreach (Unit u in Neighbors) {
                if (u.priority == -1)
                    return false;
            }
            return true;
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

        void Generation() {
            while (true) {
                Console.WriteLine("Генерация...");
                Random rnd = new Random();
                //int time = rnd.Next(3000, 10000);
                //Thread.Sleep(time);
                Thread.Sleep(15000);
                int data = rnd.Next(1, 100);
                //int data = Convert.ToInt32(Console.ReadLine());
                DataPacket packet = new DataPacket(data, clientId);
                byte[] messageBuffer = new byte[1000];
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DataPacket));
                MemoryStream stream = new MemoryStream();


                serializer.WriteObject(stream, packet);

                messageBuffer = stream.GetBuffer();    //???
                stream.Close();
                AddToLocMem(localMemory, messageBuffer);
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
            ShowNeighbours();
        }



        // подключение к следующим узлам 
        void OutConnect() {
            foreach (Unit neighb in Neighbors) {
                if (neighb.priority > 0) {
                    neighb.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    //neighb.socket.Connect(neighb.ip, 700);
                    neighb.socket.Connect(neighb.ip, neighb.acceptPort);
                }
            }
            Console.WriteLine("OutСonnect +");
        }

        // прием новых подключений и и прослушка от подключившихся
        void Accept() {
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //listenSocket.Bind(new IPEndPoint(IPAddress.Any, 900));
            listenSocket.Bind(new IPEndPoint(IPAddress.Any, acceptPort));
            listenSocket.Listen(1);             // у меня аргумент не влиял на размер очереди
            while (true) {

                // новое входящее подключение
                Socket socket = listenSocket.Accept();
                Console.WriteLine("Новое подключение");
                AcceptConnectionInfo connection = new AcceptConnectionInfo();
                connection.Socket = socket;

                // поток для него
                connection.Thread = new Thread(ProcessInConnection);
                connection.Thread.IsBackground = true;
                connection.Thread.Start(connection);

                InConnections.Add(connection);
            }
        }

        // прием пакетов
        void ProcessInConnection(object state) {
            AcceptConnectionInfo connection = (AcceptConnectionInfo)state;
            byte[] buffer = new byte[255];
            try {
                while (true) {
                    int bytesRead = connection.Socket.Receive(buffer);
                    if (bytesRead > 0) {
                        AddToLocMem(localMemory, buffer);
                        Console.WriteLine("Прием+");

                        //string str = Encoding.ASCII.GetString(localMemory);
                        //Console.WriteLine("Полученный пакет: " + str);

                    }
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
                //connection.Socket.Close();
                InConnections.Remove(connection);
            }
        }

        // отправка пакетов
        void Sending() {
            while (true) {
                int count = 0;
                // подсчет инф в памяти
                while (localMemory[count] != 0) {
                    count++;
                }
                // если память не пустая
                if (count > 0) {      // проверить
                    bool successSend = false;
                    //int bestWay = 1;
                    // ищем соседа, кому можно отправить 
                    foreach (Unit neighb in Neighbors) {
                        if (neighb.priority > 0) {

                            byte[] buffer = new byte[count];
                            //bool successSend = false;

                            for (int i = 0; i < count; i++) {  // проверить
                                buffer[i] = localMemory[i];
                            }

                            if (neighb.died) {
                                lock (locker) {
                                    try {
                                        Console.Write("Попытка восстановить соединение: ");
                                        //neighb.socket.Connect(neighb.ip, neighb.acceptPort);
                                        neighb.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                        //neighb.socket.Connect(neighb.ip, 700);
                                        neighb.socket.Connect(neighb.ip, neighb.acceptPort);

                                        ////neighb.socket.BeginConnect(neighb.ip, neighb.acceptPort);
                                        neighb.died = false;
                                        Console.WriteLine("восстановлено");
                                    }
                                    catch (Exception ex) {
                                        Console.WriteLine("не восстановлено");
                                        continue;
                                    }
                                }
                            }

                            try {
                                neighb.socket.Send(buffer);
                                successSend = true;
                            }
                            // если не удалось отправить - ищем получателя дальше
                            catch (Exception ex) {
                                //neighb.activity = false;
                                neighb.died = true;
                                //neighb.socket.Shutdown();
                                Console.WriteLine("Перевод маршрута");
                                //bestWay++;
                                continue;                   // проверить
                                //break;
                            }
                            // если пакет отправили, чистим память, цикл заканчиваем
                            if (successSend) {
                                Console.WriteLine("Передача+");
                                //bestWay = 1;

                                //string str = Encoding.ASCII.GetString(localMemory);
                                //Console.WriteLine("Отправленный пакет: " + str);

                                Array.Clear(localMemory, 0, count - 1);        // прооверить
                                Console.WriteLine("Память очищена");
                                //count = 0;
                                break;
                            }
                        }
                    }
                    if (!successSend) {
                        Console.WriteLine("Пакет отправить не удалось");
                        Thread.Sleep(5000);
                        //break;
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
