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
    class Client : Node{

        //static object locker = new object();

        // TODO: потом у клиента айпи сервера убрать из кода:
        // либо ввод консоли, либо чтение файла


        // TODO: мб сделать id в node, и по нему смотреть - сервер node или клиент
        int clientId;

        //List<DataPacket> packetsForSending = new List<DataPacket>();
        //byte[] localMemory = new byte[10000];


        // TODO: мб поднять на node, чтобы у сервера была топология входящих подключений (чтоб не пускать других)
        List<Neighbour> Neighbors = new List<Neighbour>();


        // TODO: возможно потом объединить в 1 файл конфигурации
        string topologyFileName = "topology.json";
        string idFileName = @"id.txt";
        string portFileName = @"port.txt";


        // TODO: дописать
        public Client(InputDelegate userInput, OutputDelegate userOutput) : base(userInput, userOutput) {
            //SequenceOverflowed += SendEvent;
            serverNode = false;
        }

        public override void Run() {

            if (File.Exists(topologyFileName)) {
                Console.WriteLine("Восстановление топологии");
                GetTopologyFromFile();
            }
            else {
                Console.WriteLine("Подключение для топологии");
                GetTopologyFromServer();
            }

            Console.WriteLine("id: " + Convert.ToString(clientId));
            Console.WriteLine("port: " + Convert.ToString(AcceptPort));

            ShowNeighbours();

            Console.WriteLine("Начать?");
            Console.ReadLine();

            //// ПОДКЛЮЧЕНИЕ К СЛЕДУЮЩИМ КЛИЕНТАМ
            //CreateOutConnections();


            // ПРОВЕРКА ВХОДЯЩИХ ПОДКЛЮЧЕНИЙ
            if (HasInConnection()) {
                Console.WriteLine("Есть входящие подключения");
                Thread acceptThread = new Thread(AcceptConnections);
                acceptThread.Start();
            }
            else {
                Console.WriteLine("Входящих подключений нет");
            }

            // ГЕНЕРАЦИЯ ПАКЕТОВ ДАННЫХ
            // TODO: сделать обертку для генерации данных GetData()
            Thread generationThread = new Thread(Generation);
            generationThread.Start();


            //// ОТПРАВКА ПАКЕТОВ СЛЕДУЮЩИМ КЛИЕНТАМ
            //Thread sendThread = new Thread(Sending);
            //sendThread.Start();

            //SendingProccess();
            Console.ReadKey();
        }

        void GetTopologyFromFile() {
            GetNeibsFromFile();
            GetUnitIdFromFile();
            GetAcceptPortFromFile();
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

        
        bool HasInConnection() {
            foreach (Neighbour u in Neighbors) {
                if (u.priority == -1)
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

        //static void Generation() {
        //    Console.Write("Генерация:");
        //    string str = Console.ReadLine();
        //    byte[] buffer = new byte[str.Length];
        //    buffer = Encoding.ASCII.GetBytes(str);
        //    AddToLocMem(localMemory, buffer);
        //}

        // TODO: возможно заменить, может добавить метод GetBytes в классе dataPacket
        //byte[] ConvertPacket(DataPacket packet) {
        //    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DataPacket));
        //    MemoryStream stream = new MemoryStream();
        //    serializer.WriteObject(stream, packet);
        //    byte[] bytesPacket = stream.GetBuffer();    //???
        //    stream.Close(); // возможно закрытие потока автоматом при завершении метода
        //    return bytesPacket;
        //}

        List<DataPacket> CREATETESTS(int cnt) {
            List<DataPacket> result = new List<DataPacket>();

            for(int i = 0; i< cnt; i++) {
                DataPacket newPacket = new DataPacket(clientId);
                result.Add(newPacket);
            }
            return result;
        }

        //public event EventHandler SequenceOverflowed;

        void Generation() {
            while (true) {
                Console.WriteLine("Генерация...");
                Thread.Sleep(500);

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


            //if(packetsSequence.Count > 3) {
            //    if(SequenceOverflowed != null) {
            //        SequenceOverflowed(this, new EventArgs());
            //    }
            //}


                //WriteBytesToLocMem();

                //byte[] bytesPacket = ConvertPacket(packet);


                //byte[] bytesPacket = DataPacket.GetBytes(list);
                //AddToLocMem(localMemory, bytesPacket);

                //List<DataPacket> list2 = new List<DataPacket>();
                //list2.Add(new DataPacket(clientId));
                //byte[] bytesPacket2 = DataPacket.GetBytes(list2);
                //AddToLocMem(localMemory, bytesPacket2);

                //DataPacket packet2 = new DataPacket(clientId);
                ////byte[] bytesPacket = ConvertPacket(packet);
                //byte[] bytesPacket2 = DataPacket.GetBytes(packet2);
                //AddToLocMem(localMemory, bytesPacket2);

            }
        }

        //void WriteBytesToLocMem() {
        //    byte[] bytesPacket = DataPacket.GetBytes(packetsForSending);
        //    Array.Clear(localMemory,0,localMemory.Length);
        //    AddToLocMem(localMemory, bytesPacket);
        //}

        // TODO: посмотреть что общего с методом outconnect (возможно объеденить)
        void ConnectToServer(out Socket socket) {
            Console.WriteLine("Подключение к серверу для топологии..");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //serverSocket.Bind(new IPEndPoint(IPAddress.Any, 660));      // необязательно
            socket.Connect(SERVER_IP, SERVER_PORT_FOR_TOPOLOGY);
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
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Neighbour>));
            
            stream.Position = 0;    // необязательно?
            Neighbors = (List<Neighbour>)jsonSerializer.ReadObject(stream);

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
                //Console.WriteLine("id:" + strId + "!");
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
                //Console.WriteLine("port:" + strPort + "!");
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
            Console.WriteLine("Полученная топология:");
            foreach (Neighbour u in Neighbors) {
                Console.WriteLine(u.ip + ":" + u.acceptPort + ", " + u.priority);
            }
        }

        void GetTopologyFromServer() {
            ConnectToServer(out Socket serverSocket);
            GetNeibsFromServer(serverSocket);
            GetUnitIdFromServer(serverSocket);
            GetAcceptPortFromServer(serverSocket);
            //serverSocket.Close();
            CloseSocket(serverSocket);

            Neighbors.Sort(CompareUnitsByPriority); // TODO: посмотреть как работает данный вызов
            WriteFiles();
            //ShowNeighbours();
        }



        // подключение к следующим узлам 
        void CreateOutConnections() {
            foreach (Neighbour neighb in Neighbors) {
                if (neighb.priority > 0) {
                    neighb.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    neighb.socket.Connect(neighb.ip, neighb.acceptPort);
                }
            }
            Console.WriteLine("OutСonnect +");
        }

        // TODO: ВРЕМЕННО - УБРАТЬ!
        //DataPacket GetDataFromBuffer(byte[] buffer) {
        //    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DataPacket));
        //    DataPacket packet = new DataPacket();
        //    byte[] streamBuffer = new byte[BytesInCollection(buffer)];
        //    CopyFromTo(buffer, streamBuffer);
        //    MemoryStream stream = new MemoryStream(streamBuffer);
        //    stream.Position = 0;    // необязательно?
        //    packet = (DataPacket)serializer.ReadObject(stream);
        //    stream.Close();
        //    return packet;
        //}

        //int GetBytesCountInLocMem() {
        //    int bytesCount = 0;
        //    while (localMemory[bytesCount] != 0) {
        //        bytesCount++;
        //    }
        //    return bytesCount;
        //}

        byte[] GetBufferForSending(/*int bufferSize*/) {
            //byte[] buffer = new byte[bufferSize];
            //CopyFromTo(localMemory, buffer);

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


        //void SendEvent(object sender, EventArgs e) {
        //    Console.WriteLine("Мы в событии!!!");
        //    // TODO: возможно убрать ВСЕ циклы while(true) и попроб заменить на события
        //        if (packetsSequence.Count > 0) {
        //            byte[] sendBuffer = GetBufferForSending(/*dataBytesInLocMemory*/);
        //            bool successSend = false;
        //            foreach (Neighbour neighb in Neighbors) {
        //                if (neighb.priority > 0) {
        //                    if (neighb.died) {
        //                        Console.Write("Попытка восстановить соединение: ");
        //                        TryLifeUpConnection(neighb, out bool successLifeUp);
        //                        if (successLifeUp) {
        //                            Console.WriteLine("восстановлено");
        //                            neighb.died = false;
        //                        }
        //                        else {
        //                            Console.WriteLine("не восстановлено");
        //                        }
        //                    }

        //                    TrySend(sendBuffer, neighb, ref successSend);
        //                    if (successSend) {
        //                        Console.WriteLine("Передача+");
        //                        // TODO: записать номер пакета в файл
        //                        //Array.Clear(localMemory, 0, dataBytesInLocMemory - 1);
        //                        packetsSequence.Clear();
        //                        break;

        //                    }
        //                    else {
        //                        Console.WriteLine("Перевод маршрута");
        //                        neighb.died = true;
        //                        continue;
        //                    }
        //                }
        //            }

        //            if (!successSend) {
        //                Console.WriteLine("Пакет отправить не удалось");
        //                Thread.Sleep(5000);
        //            }
        //        }
        //}


        void SendingProccess() {

            //var tcpEndPoint = new IPEndPoint(IPAddress.Parse(SERVER_IP), 700);
                Socket sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // 1 ЗАПРЕЩАЕМ ДОБАВЛЕНИЕ В PACKETSEQ
                // 2 ВЫЗЫВАЕМ GETBUFFERFORS..
                // 3 ОЧИЩАЕМ PACKETSEQ
                // 4 СНИМАЕМ ЗАПРЕТ НА ДОББАВЛЕНИЕ
                // ** это нужно на случай, что между getbuf и packets.clear придут пакеты, которые затрутся
                byte[] sendBuffer = GetBufferForSending();
                packetsSequence.Clear();

            // TODO: проверить перевод маршрута
            bool successSend = false;

            foreach (Neighbour neighb in Neighbors) {
                if (neighb.priority > 0) {
                    try {
                        var tcpEndPoint = new IPEndPoint(IPAddress.Parse(neighb.ip), neighb.acceptPort);
                        sendSocket.Connect(tcpEndPoint);

                        sendSocket.Send(sendBuffer);
                        PrintMessage("Пакет отправлен");
                        successSend = true;
                        sendSocket.Shutdown(SocketShutdown.Both);
                        sendSocket.Close();
                    }
                    catch (Exception ex) {
                        PrintMessage("Перевод маршрута..");
                        continue;
                    }
                }
                
            }
            if (!successSend) {
                PrintMessage("Пакет отправить не удалось");
            }

        }

        // отправка пакетов
        void Sending() {
            // TODO: возможно убрать ВСЕ циклы while(true) и попроб заменить на события
            while (true) {
                //int dataBytesInLocMemory = GetBytesCountInLocMem();
                //if (dataBytesInLocMemory > 0) {
                if (packetsSequence.Count > 0) {
                    byte[] sendBuffer = GetBufferForSending(/*dataBytesInLocMemory*/);
                    bool successSend = false;
                    foreach (Neighbour neighb in Neighbors) {
                        if (neighb.priority > 0) {
                            if (neighb.died) {
                                Console.Write("Попытка восстановить соединение: ");
                                TryLifeUpConnection(neighb, out bool successLifeUp);
                                if (successLifeUp) {
                                    Console.WriteLine("восстановлено");
                                    neighb.died = false;
                                }
                                else {
                                    Console.WriteLine("не восстановлено");
                                }
                            }
                            
                            TrySend(sendBuffer, neighb, ref successSend);

                            if (successSend) {
                                Console.WriteLine("Передача+");
                                // TODO: записать номер пакета в файл
                                //Array.Clear(localMemory, 0, dataBytesInLocMemory - 1);
                                packetsSequence.Clear();
                                break;

                            }
                            else {
                                Console.WriteLine("Перевод маршрута");
                                neighb.died = true;
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





