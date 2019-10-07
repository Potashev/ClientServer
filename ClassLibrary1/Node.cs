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
    public class Node {

        protected const string SERVER_IP = "192.168.1.107";



        protected IPAddress ip { get; set; }
        protected int AcceptPort { get; set; }


        protected List<AcceptConnectionInfo> InConnections = new List<AcceptConnectionInfo>();
        protected List<DataPacket> packetsSequence = new List<DataPacket>();


        public void Run() { }

        protected void AcceptConnections() {
            RunListenSoket(out Socket acceptSocket);
            while (true) {
                Socket socket = acceptSocket.Accept();
                Console.WriteLine("Новое подключение");

                RunAcceptedConnection(socket);
            }
        }

        protected void RunListenSoket(out Socket listenSocket) {
            listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Any, AcceptPort));
            listenSocket.Listen(1);             // у меня аргумент не влиял на размер очереди
        }
        protected void RunAcceptedConnection(Socket socket) {
            AcceptConnectionInfo connection = new AcceptConnectionInfo();
            connection.Socket = socket;
            connection.Thread = new Thread(ReceivingData);
            connection.Thread.IsBackground = true;
            InConnections.Add(connection);
            connection.Thread.Start(connection);
        }

        protected void ReceivingData(object state) {
            AcceptConnectionInfo connection = (AcceptConnectionInfo)state;
            byte[] buffer = new byte[255];
            try {
                while (true) {
                int bytesRead = connection.Socket.Receive(buffer);

                // возможно if убрать тк receive подразумевает, что пакет не пустой
                // или проверить входящие данные на соответствие формату (DataIsChecked())
                if (bytesRead > 0) {

                    List<DataPacket> receivedPackets = GetDataFromBuffer(buffer);
                    packetsSequence.AddRange(receivedPackets);



                    // КОСТЫЛЬ ДЛЯ ОПРЕДЕЛЕНИЯ СЕРВЕРА
                    if (AcceptPort == 700) {
                        foreach (DataPacket p in receivedPackets)
                            Console.WriteLine(p.GetInfo());
                    }

                    Console.WriteLine("Прием+");
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
        protected void CopyFromTo(byte[] bufferFrom, byte[] bufferTO) {
            for (int i = 0; i < bufferTO.Length; i++) {
                bufferTO[i] = bufferFrom[i];
            }
        }


    }
}

// NODE - класс родитель для клиента и сервера
// включает:
// метод 
// + ByteInCollection
// + CopyFromTo
// + установка входящего соединения (AcceptConncetion)
// + процесс приема пакета
// публичный метод Run  (виртуальный либо абстрактный?)
// + метод GetDataFromBuffer

//RUNTIME EXCEPTIONS:
// +  1) клиент: при отвале входящего подключения

// ПОМЕНЯТЬ ПОСЛЕ:
// 1) работать с ip адресами не как строки а как класс IPAddress

//ПОДПРАВИТЬ:
