//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Net.Sockets;
//using System.Runtime.Serialization;
//using System.Runtime.Serialization.Json;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace ClientProject {
//    class Program {

//        static object locker = new object();

//        //static string serverIp = "192.168.1.106";
//        static string serverIp = "172.20.10.2";

//        [DataContract]
//        public class Unit {
//            [DataMember]
//            public string ip;
//            [DataMember]
//            public int priority;        // пока >0 - отправка (с приоритетом), -1 - прием
//            [DataMember]
//            public int acceptPort;

//            public Socket socket;       // нужны только у узлов отправки (сокет + приоритет = динам маршрут)

//            public bool died;       // нужны только у узлов отправки

//            public Unit(string addr, int weight, int port) {
//                ip = addr;
//                priority = weight;

//                acceptPort = port;

//                //activity = true;
//                died = false;
//            }
//        }

//        [DataContract]
//        public class DataPacket {
//            [DataMember]
//            public int _unitId;
//            [DataMember]
//            public int _number;
//            [DataMember]
//            public int _value;

//            static int packetCount = 1;
//            public DataPacket(int value) {
//                _unitId = myId;
//                _number = packetCount;
//                _value = value;
//                packetCount++;
//            }
//        }

//        // входящие сокеты (работают в потоках), связывает сокет с потоком
//        public class AcceptConnectionInfo {
//            public Socket Socket;
//            public Thread Thread;
//        }

//        static int myAcceptPort;

//        static int myId;

//        static byte[] localMemory = new byte[10000];

//        static List<Unit> Neighbors = new List<Unit>();
//        // список входящих сокетов (прием)
//        static List<AcceptConnectionInfo> InConnections = new List<AcceptConnectionInfo>();

//        // добавление принятого пакета в лок память
//        static public void AddToLocMem(byte[] locMem, byte[] buffer) {
//            int i = 0;
//            int j = 0;
//            while (locMem[i] != 0) {
//                i++;
//            }
//            while (j < buffer.Length) {
//                locMem[i] = buffer[j];
//                i++;
//                j++;
//            }
//        }

//        static int BytesInCollection(byte[] collection) {
//            int count = 0;
//            while (collection[count] != 0) {
//                count++;
//            }
//            return count;
//        }

//        static public void CopyFromTo(byte[] bufferFrom, byte[] bufferTO) {
//            for (int i = 0; i < bufferTO.Length; i++) {
//                bufferTO[i] = bufferFrom[i];
//            }
//        }



//        static bool NoAccept() {
//            foreach (Unit u in Neighbors) {
//                if (u.priority == -1)
//                    return false;
//            }
//            return true;
//        }

//        // ф-я сравнения для сортировки списка по возрастанию (-1, -1... 1, 2...)
//        // мб найти другую сортировку ( 1, 2... -1, -1...)
//        static int CompareUnitsByPriority(Unit x, Unit y) {
//            if (x.priority == y.priority)
//                return 0;
//            if (x.priority < y.priority)
//                return -1;
//            else
//                return 1;
//        }

//        //static void Generation() {
//        //    Console.Write("Генерация:");
//        //    string str = Console.ReadLine();
//        //    byte[] buffer = new byte[str.Length];
//        //    buffer = Encoding.ASCII.GetBytes(str);
//        //    AddToLocMem(localMemory, buffer);
//        //}

//        static void Generation() {
//            while (true) {
//                Console.WriteLine("Генерация...");
//                Random rnd = new Random();
//                //int time = rnd.Next(3000, 10000);
//                //Thread.Sleep(time);
//                Thread.Sleep(15000);
//                int data = rnd.Next(1, 100);
//                //int data = Convert.ToInt32(Console.ReadLine());
//                DataPacket packet = new DataPacket(data);
//                byte[] messageBuffer = new byte[1000];
//                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DataPacket));
//                MemoryStream stream = new MemoryStream();


//                serializer.WriteObject(stream, packet);

//                messageBuffer = stream.GetBuffer();    //???
//                stream.Close();
//                AddToLocMem(localMemory, messageBuffer);
//            }
//        }

//        static public void AcceptForTopology() {
//            Console.WriteLine("Подключение к серверу для топологии..");
//            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//            //serverSocket.Bind(new IPEndPoint(IPAddress.Any, 660));      // необязательно
//            serverSocket.Connect(serverIp, 999);
//            Console.WriteLine("Прием топологии..");
//            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
//            byte[] receiveBuffer = new byte[1000];
//            byte[] receivePort = new byte[1000];

//            byte[] receiveId = new byte[1000];

//            //byte[] receiveId = new byte[4];

//            lock (locker) {

//                serverSocket.Receive(receiveBuffer);

//                serverSocket.Receive(receiveId);
//                byte[] arr = new byte[BytesInCollection(receiveId)];
//                CopyFromTo(receiveId, arr);
//                string strId = Encoding.ASCII.GetString(arr);
//                Console.WriteLine("id:" + strId + "!");
//                myId = Int32.Parse(strId);
//                //serverSocket.Receive(receiveId);

//                serverSocket.Receive(receivePort);
//                byte[] arr2 = new byte[BytesInCollection(receivePort)];
//                CopyFromTo(receivePort, arr2);
//                string strPort = Encoding.ASCII.GetString(arr2);
//                Console.WriteLine("port:" + strPort + "!");
//                myAcceptPort = Convert.ToInt32(strPort);
//            }
//            //string strId = Encoding.ASCII.GetString(receiveId);
//            //myId = Convert.ToInt32(strId);

//            byte[] streamBuffer = new byte[BytesInCollection(receiveBuffer)];
//            CopyFromTo(receiveBuffer, streamBuffer);
//            MemoryStream stream = new MemoryStream(streamBuffer);


//            //CreateFile(jsonSerializer.ReadObject(stream));

//            //MemoryStream stream = new MemoryStream();
//            //stream.Write(buffer2, 0, buffer2.Length);
//            stream.Position = 0;    // необязательно?
//            Neighbors = (List<Unit>)jsonSerializer.ReadObject(stream);

//            //string fileName = Convert.ToString(myId) + "_Unit_Topology.json";


//            //FileStream fsid = new FileStream("id.json", FileMode.OpenOrCreate);
//            //jsonSerializer.WriteObject(fs, Convert.ToString(myAcceptPort));


//            //FileStream fsport = new FileStream("Port.json", FileMode.OpenOrCreate);
//            //jsonSerializer.WriteObject(fs, Convert.ToString(myAcceptPort));
//            //fsport.Close();




//            //FileStream fs = new FileStream("Topology.json", FileMode.OpenOrCreate);
//            //Neighbors = (List<Unit>)jsonSerializer.ReadObject(fs);

//            //fstream.


//            stream.Close();
//            serverSocket.Close();   // проверить
//            Neighbors.Sort(CompareUnitsByPriority);     // приоритет сортировка по возрастанию (-1, -1... 1, 2...)

//            FileStream fstream = new FileStream("topology.json", FileMode.OpenOrCreate);
//            jsonSerializer.WriteObject(fstream, Neighbors);
//            fstream.Close();
//            FileWriteIdAndPort();

//            Console.WriteLine("Полученная топология:");
//            foreach (Unit u in Neighbors) {
//                Console.WriteLine(u.ip + ":" + u.acceptPort + ", " + u.priority);
//            }
//        }



//        // подключение к следующим узлам 
//        static public void OutConnect() {
//            foreach (Unit neighb in Neighbors) {
//                if (neighb.priority > 0) {
//                    neighb.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//                    //neighb.socket.Connect(neighb.ip, 700);
//                    neighb.socket.Connect(neighb.ip, neighb.acceptPort);
//                }
//            }
//            Console.WriteLine("OutСonnect +");
//        }

//        // прием новых подключений и и прослушка от подключившихся
//        static void Accept() {
//            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//            //listenSocket.Bind(new IPEndPoint(IPAddress.Any, 900));
//            listenSocket.Bind(new IPEndPoint(IPAddress.Any, myAcceptPort));
//            listenSocket.Listen(1);             // у меня аргумент не влиял на размер очереди
//            while (true) {

//                // новое входящее подключение
//                Socket socket = listenSocket.Accept();
//                Console.WriteLine("Новое подключение");
//                AcceptConnectionInfo connection = new AcceptConnectionInfo();
//                connection.Socket = socket;

//                // поток для него
//                connection.Thread = new Thread(ProcessInConnection);
//                connection.Thread.IsBackground = true;
//                connection.Thread.Start(connection);

//                InConnections.Add(connection);
//            }
//        }

//        // прием пакетов
//        static void ProcessInConnection(object state) {
//            AcceptConnectionInfo connection = (AcceptConnectionInfo)state;
//            byte[] buffer = new byte[255];
//            try {
//                while (true) {
//                    int bytesRead = connection.Socket.Receive(buffer);
//                    if (bytesRead > 0) {
//                        AddToLocMem(localMemory, buffer);
//                        Console.WriteLine("Прием+");

//                        //string str = Encoding.ASCII.GetString(localMemory);
//                        //Console.WriteLine("Полученный пакет: " + str);

//                    }
//                }
//            }
//            catch (SocketException exc) {
//                Console.WriteLine("Socket exception: " +
//                    exc.SocketErrorCode);
//            }
//            catch (Exception exc) {
//                Console.WriteLine("Exception: " + exc);
//            }
//            finally {
//                //connection.Socket.Close();
//                InConnections.Remove(connection);
//            }
//        }

//        // отправка пакетов
//        static void Sending() {
//            while (true) {
//                int count = 0;
//                // подсчет инф в памяти
//                while (localMemory[count] != 0) {
//                    count++;
//                }
//                // если память не пустая
//                if (count > 0) {      // проверить
//                    bool successSend = false;
//                    //int bestWay = 1;
//                    // ищем соседа, кому можно отправить 
//                    foreach (Unit neighb in Neighbors) {
//                        if (neighb.priority > 0) {

//                            byte[] buffer = new byte[count];
//                            //bool successSend = false;

//                            for (int i = 0; i < count; i++) {  // проверить
//                                buffer[i] = localMemory[i];
//                            }

//                            if (neighb.died) {
//                                lock (locker) {
//                                    try {
//                                        Console.Write("Попытка восстановить соединение: ");
//                                        //neighb.socket.Connect(neighb.ip, neighb.acceptPort);
//                                        neighb.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//                                        //neighb.socket.Connect(neighb.ip, 700);
//                                        neighb.socket.Connect(neighb.ip, neighb.acceptPort);

//                                        ////neighb.socket.BeginConnect(neighb.ip, neighb.acceptPort);
//                                        neighb.died = false;
//                                        Console.WriteLine("восстановлено");
//                                    }
//                                    catch (Exception ex) {
//                                        Console.WriteLine("не восстановлено");
//                                        continue;
//                                    }
//                                }
//                            }

//                            try {
//                                neighb.socket.Send(buffer);
//                                successSend = true;
//                            }
//                            // если не удалось отправить - ищем получателя дальше
//                            catch (Exception ex) {
//                                //neighb.activity = false;
//                                neighb.died = true;
//                                //neighb.socket.Shutdown();
//                                Console.WriteLine("Перевод маршрута");
//                                //bestWay++;
//                                continue;                   // проверить
//                                //break;
//                            }
//                            // если пакет отправили, чистим память, цикл заканчиваем
//                            if (successSend) {
//                                Console.WriteLine("Передача+");
//                                //bestWay = 1;

//                                //string str = Encoding.ASCII.GetString(localMemory);
//                                //Console.WriteLine("Отправленный пакет: " + str);

//                                Array.Clear(localMemory, 0, count - 1);        // прооверить
//                                Console.WriteLine("Память очищена");
//                                //count = 0;
//                                break;
//                            }
//                        }
//                    }
//                    if (!successSend) {
//                        Console.WriteLine("Пакет отправить не удалось");
//                        Thread.Sleep(5000);
//                        //break;
//                    }
//                }
//                Thread.Sleep(1);       // перестает есть проц (с 25% до 0)
//            }
//        }

//        static void FileWriteIdAndPort() {
//            //FileStream fs = File.Create(@"Id.txt");
//            StreamWriter textFile = new StreamWriter(@"id.txt");
//            textFile.WriteLine(Convert.ToString(myId));
//            textFile.Close();

//            StreamWriter PortFile = new StreamWriter(@"port.txt");
//            PortFile.WriteLine(Convert.ToString(myAcceptPort));
//            PortFile.Close();
//        }

//        static void LifeUpConnection() {
//            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Unit>));
//            FileStream fsTopology = new FileStream("topology.json", FileMode.OpenOrCreate);
//            Neighbors = (List<Unit>)jsonSerializer.ReadObject(fsTopology);
//            //fsTopology.Close();

//            StreamReader fsId = new StreamReader(@"id.txt");
//            myId = Convert.ToInt32(fsId.ReadLine());
//            fsId.Close();
//            StreamReader fsPort = new StreamReader(@"port.txt");
//            myAcceptPort = Convert.ToInt32(fsPort.ReadLine());
//            fsPort.Close();
//        }

//        static void Main(string[] args) {
//            //Console.WriteLine("\tMain Client");

//            //if (File.Exists("topology.json"))
//            //{
//            //    Console.WriteLine("Восстановление топологии");
//            //    LifeUpConnection();
//            //}
//            //else
//            //{
//            //    Console.WriteLine("Подключение для топологии");
//            //    AcceptForTopology();
//            //}

//            //Console.WriteLine("id: " + Convert.ToString(myId));
//            //Console.WriteLine("port: " + Convert.ToString(myAcceptPort));
//            //Console.WriteLine("Начать?");
//            //Console.ReadLine();

//            //OutConnect();

//            //if (!NoAccept()) {
//            //    Console.WriteLine("Есть входящие подключения");
//            //    Thread acceptThread = new Thread(Accept);
//            //    acceptThread.Start();
//            //}
//            //else {
//            //    Console.WriteLine("Входящих подключений нет");
//            //    //Generation();
//            //}

//            //Thread generationThread = new Thread(Generation);
//            //generationThread.Start();

//            ////Generation();

//            //Thread sendThread = new Thread(Sending);
//            //sendThread.Start();

//            Console.ReadLine();
//        }
//    }
//}
