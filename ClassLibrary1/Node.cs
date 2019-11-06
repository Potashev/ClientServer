using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientServerLib {
    public abstract class Node {

        protected int numberIncomingConnections;

        protected const string SERVER_IP = "192.168.1.102";
        protected const int SERVER_PORT_FOR_TOPOLOGY = 999;

        private int acceptPort;

        protected IPAddress Ip { get; set; }
        protected int AcceptPort {
            get {
                return acceptPort;
            }
            set {
                if (value > 0) {
                    acceptPort = value;
                }
                else {
                    acceptPort = 700;
                }
            }
        }

        

        protected List<DataPacket> packetsSequence = new List<DataPacket>();

        // TODO: проверить readonly
        
        protected readonly InputDelegate input;
        protected readonly OutputDelegate output;

        static protected object locker = new object();    // TODO: попробовать перенести locker в SendNeibs

        protected Node(InputDelegate userInput, OutputDelegate userOutput) {
            input = userInput;
            output = userOutput;

            numberIncomingConnections = 0;  // TODO: проверить

            Ip = GetIpAdress();

        }

        // TODO: возможно полсе найти другой вариант
        IPAddress GetIpAdress() {
            string s = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostByName(Dns.GetHostName());
            IPAddress[] addr = ipEntry.AddressList;
            return addr[0];
        }

        // TODO: возможно вместо виртуально сервера добавить интерфейс с Run()
        public abstract void Run();

        static protected void RunAcceptSocket(int acceptPort, out Socket acceptSocket) {
            acceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            acceptSocket.Bind(new IPEndPoint(IPAddress.Any, acceptPort));
            acceptSocket.Listen(1);             // у меня аргумент не влиял на размер очереди
        }

        static protected void CloseSocket(Socket socket) {
            // TODO: разобраться почему на shutdown ловлю исключение
            // (возможно связано с тем, что shutdown вызываю с обоих сторон)

            //socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        public delegate void PacketSequenceState(List<DataPacket> addedPackets);
        public event PacketSequenceState eventPacketSequenceAdded;

        // TODO: ПРОВЕРИТЬ
        virtual protected void AddPacketsInSequence(List<DataPacket> packets) {
            packetsSequence.AddRange(packets);
        }

        protected void ReceivingProccess() {
            Socket acceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //acceptSocket.Bind(new IPEndPoint(IPAddress.Parse(SERVER_IP), 700));
            acceptSocket.Bind(new IPEndPoint(Ip, AcceptPort));
            acceptSocket.Listen(numberIncomingConnections); //TODO: проверить  // здесь параметр будет число соседов сервера по топологии (в теории)

            while (true) {
                try {
                    var listener = acceptSocket.Accept();
                    var buffer = new byte[1000];
                    List<byte[]> messageList = new List<byte[]>();  // содержит все куски сообщения
                                                                    //var acumBuffer = new byte[10000];
                    var size = 0;
                    //var messageSize = 0;
                    //var data = new StringBuilder();

                    // РАБОТАЕТ ЕСЛИ СООБЩЕНИЕ 1 КУСКОМ (1 ИТЕРАЦИЯ ЦИКЛА)
                    do {
                        size = listener.Receive(buffer);
                        //messageList.Add(buffer);

                    } while (listener.Available > 0);
                    listener.Shutdown(SocketShutdown.Both);
                    listener.Close();


                    PrintMessage("Получены данные");

                    //byte[] resultbytes = GetResultBuffer(messageList);
                    if (size > 0) {
                        List<DataPacket> receivedPackets = GetDataFromBuffer(buffer);
                        AddPacketsInSequence(receivedPackets);

                        eventPacketSequenceAdded(receivedPackets);
                    }
                    else {
                        PrintMessage("СОЕДИНЕНИЕ ВОССТАНОВЛЕНО!!!");
                    }
                }
                catch (Exception ex) {
                    PrintMessage(ex.Message);
                }

            }
        }

        protected byte[] GetResultBuffer(List<byte[]> msgList) {
            int size = 0;
            foreach(byte[] b in msgList) {
                size += b.Length;
            }
            byte[] resBuf = new byte[size];
            int index = 0;
            foreach(byte[] b in msgList) {
                Array.Copy(b, 0, resBuf, index,b.Length);
                index += b.Length;
            }
            return resBuf;
        }

        protected List<DataPacket> GetDataFromBuffer(byte[] buffer) {
            byte[] streamBuffer = new byte[BytesInCollection(buffer)];
            // TODO: возможно поменять на array.(copy)
            CopyFromTo(buffer, streamBuffer);

            MemoryStream memoryStream = new MemoryStream(streamBuffer);
            List<DataPacket> packets = DeserializeJson<DataPacket>(memoryStream);
            memoryStream.Close();

            return packets;
        }


        // пример полиморфизма (апкаста): принимает memorystream и filestream
        //protected void SerializeJson(List<Neighbour> neighbours, Stream stream) {
        //    DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Neighbour>));
        //    jsonSerializer.WriteObject(stream, neighbours); //TODO: проверить отправку у клиента, возможно подогнать под deserialize
        //}

        protected void SerializeJson<T>(List<T> objectsList, Stream stream) {
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<T>));    
            jsonSerializer.WriteObject(stream, objectsList);    // по идее здесь try не нужен тк список так и так сериализуется?
        }

        protected List<T> DeserializeJson<T>(Stream stream) {
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<T>));
            try {
                List<T> resultList = (List<T>)jsonSerializer.ReadObject(stream);
                return resultList;
            }
            catch(Exception ex) {
                PrintMessage(ex.Message);
            }
            return new List<T>();
        }

        protected int ReadStream(string fileName) {
            int result = 0;
            try {
                StreamReader stream = new StreamReader(fileName);
                result = Convert.ToInt32(stream.ReadLine());
                stream.Close();
            }
            catch(Exception ex) {
                PrintMessage(ex.Message);
            }
            return result;
        }

        protected void WriteStream(string fileName, int value) {
            StreamWriter stream = new StreamWriter(fileName);
            stream.WriteLine(Convert.ToString(value));
            stream.Close();
        }

        // TODO: возможно позже поменять на dataInCollection (либо подобное)
        protected int BytesInCollection(byte[] collection) {
            int count = 0;
            while (collection[count] != 0) {
                count++;
            }
            return count;
        }
        protected void CopyFromTo(byte[] bufferFrom, byte[] bufferTo) {
            for (int i = 0; i < bufferTo.Length; i++) {
                bufferTo[i] = bufferFrom[i];
            }
        }


        
        protected void PrintMessage(string message) {
            output(message); 
        }
        // TODO: возможно функцию убрать и вызывать делегат
        protected string InputData() {
            return input();
        }

        protected int GetInputData(ValueRequirement requirement, string inputMessage = null) {
            int resultData = 0;
            while (true) {
                if (inputMessage != null) {
                    PrintMessage(inputMessage);
                }
                if (int.TryParse(InputData(), out int inputvalue) && requirement(inputvalue)) {
                    resultData = inputvalue;
                    break;
                }
                else {
                    PrintMessage("Неверный ввод, повторите попытку.");
                }
            }
            return resultData;

        }

        protected int CheckInputData(ValueRequirement requirement, string stringValue, string inputMessage) {
            if (int.TryParse(stringValue, out int inputValue) && requirement(inputValue)) {
                return inputValue;
            }
            else {
                PrintMessage("Неверный ввод, повторите попытку:");
                return GetInputData(requirement, inputMessage);
            }
        }

    }


    

    public delegate string InputDelegate();
    public delegate void OutputDelegate(string message);
    public delegate bool ValueRequirement(int value);

}

// NODE - класс родитель для клиента и сервера
// включает:
// метод 
// + ByteInCollection
// + CopyFromTo
// + установка входящего соединения (AcceptConncetion)
// + процесс приема пакета
// + публичный метод Run  (виртуальный либо абстрактный?)
// + метод GetDataFromBuffer

//RUNTIME EXCEPTIONS:
// +  1) клиент: при отвале входящего подключения
// 2) узел (отвал сервера): периодическая ошибка десериализации( выше при высокой скорости генерации или передачи нескольких пакетов)
// связано (скорей всего) с некоректной сериализацией на клиенте

// ПОМЕНЯТЬ ПОСЛЕ:
// 1) работать с ip адресами не как строки а как класс IPAddress
// 2) возможно, связать идею работы с файлами, т.е не методы записи и чтения, а формат работы с файлами(в каком виде они будут)
// (может сделать это через отдельный класс работы с файлами на уровне node)
// 3) посмотреть правильное закрытие сокета. если не 1 строчка .close, тогда все брать из метода closesocket в node

//ПОДПРАВИТЬ:
