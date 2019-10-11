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

        protected const string SERVER_IP = "192.168.1.107";
        protected const int SERVER_PORT_FOR_TOPOLOGY = 999;

        //protected const int SERVER_ACCEPT_PORT = 

        // решение (возможно временное) определения сервера в node
        protected bool serverNode;



        protected IPAddress Ip { get; set; }
        protected int AcceptPort { get; set; }


        protected List<AcceptConnectionInfo> InConnections = new List<AcceptConnectionInfo>();
        protected List<DataPacket> packetsSequence = new List<DataPacket>();

        // TODO: проверить readonly
        
        protected readonly InputDelegate input;
        protected readonly OutputDelegate output;

        static protected object locker = new object();    // TODO: попробовать перенести locker в SendNeibs

        protected Node(InputDelegate userInput, OutputDelegate userOutput) {
            input = userInput;
            output = userOutput;

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
        public virtual void Run() { }

        

        protected void AcceptConnections() {
            RunAcceptSocket(AcceptPort, out Socket listenSocket);
            while (true) {
                Socket acceptedSocket = listenSocket.Accept();
                PrintMessage("Новое подключение");

                RunAcceptedConnection(acceptedSocket);
            }
        }

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

        protected void RunAcceptedConnection(Socket socket) {
            AcceptConnectionInfo connection = new AcceptConnectionInfo();
            connection.Socket = socket;
            connection.Thread = new Thread(ReceivingData);
            connection.Thread.IsBackground = true;
            InConnections.Add(connection);
            connection.Thread.Start(connection);
        }

        protected void ReceivingProccess() {
            Socket acceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //acceptSocket.Bind(new IPEndPoint(IPAddress.Parse(SERVER_IP), 700));
            acceptSocket.Bind(new IPEndPoint(Ip, AcceptPort));
            acceptSocket.Listen(1); // здесь параметр будет число соседов сервера по топологии (в теории)

            while (true) {
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

                //byte[] resultbytes = GetResultBuffer(messageList);

                List<DataPacket> receivedPackets = GetDataFromBuffer(buffer);
                packetsSequence.AddRange(receivedPackets);

                if (serverNode) {
                    foreach (DataPacket p in receivedPackets)
                        PrintMessage(p.GetInfo());
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

        protected void ReceivingData(object state) {
            AcceptConnectionInfo connection = (AcceptConnectionInfo)state;
            byte[] buffer = new byte[255];

            // TODO: сравнить с возможным вариантом перенести try под while
            try {
                while (true) {
                int bytesRead = connection.Socket.Receive(buffer);

                // возможно if убрать тк receive подразумевает, что пакет не пустой
                // или проверить входящие данные на соответствие формату (DataIsChecked())
                if (bytesRead > 0) {

                    List<DataPacket> receivedPackets = GetDataFromBuffer(buffer);
                    packetsSequence.AddRange(receivedPackets);
                        
                    if (serverNode) {
                        foreach (DataPacket p in receivedPackets)
                                PrintMessage(p.GetInfo());
                    }

                        PrintMessage("Прием+");
                }
            }
            }
            catch (SocketException exc) {

                PrintMessage("Socket exception: " +
                    exc.SocketErrorCode);
            }
            catch (Exception exc) {
                PrintMessage("Exception: " + exc);
            }
            finally {
                //connection.Socket.Close();
                InConnections.Remove(connection);
            }
        }

        protected List<DataPacket> GetDataFromBuffer(byte[] buffer) {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<DataPacket>));
            //DataPacket packet = new DataPacket();
            List<DataPacket> packet = new List<DataPacket>();
            byte[] streamBuffer = new byte[BytesInCollection(buffer)];
            // TODO: возможно поменять на array.(copy)
            CopyFromTo(buffer, streamBuffer);
            MemoryStream stream = new MemoryStream(streamBuffer);
            stream.Position = 0;    // необязательно?
            packet = (List<DataPacket>)serializer.ReadObject(stream);
            stream.Close();
            return packet;
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

    }

    public delegate string InputDelegate();
    public delegate void OutputDelegate(string message);

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
