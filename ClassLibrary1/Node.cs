using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;

namespace ClientServerLib {
    public abstract class Node {

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
        private int acceptPort;

        protected List<DataPacket> packetsSequence = new List<DataPacket>();

        protected int numberIncomingConnections;

        //protected const string SERVER_IP = "192.168.1.106"; // TODO: временно
        protected const int SERVER_PORT_FOR_TOPOLOGY = 1000;
        
        protected readonly InputDelegate input;
        protected readonly OutputDelegate output;
        
        protected event PacketSequenceState eventPacketSequenceAdded;
        
        protected Node(InputDelegate userInput, OutputDelegate userOutput) {
            input = userInput;
            output = userOutput;
            numberIncomingConnections = 0;
            Ip = GetIpAdress();
        }
        
        protected void RunAcceptSocket(out Socket acceptSocket, int numberConnections, int port) {
            acceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            acceptSocket.Bind(new IPEndPoint(Ip, port));
            acceptSocket.Listen(numberConnections);
        }
        
        virtual protected void AddPacketsInSequence(List<DataPacket> packets) {
            packetsSequence.AddRange(packets);
        }

        protected void ReceivingProccess() {
            RunAcceptSocket(out Socket acceptSocket, numberIncomingConnections, AcceptPort);
            while (true) {
                try {
                    var listener = acceptSocket.Accept();
                    var buffer = new byte[256];
                    var acumBuffer = new List<byte>();

                    var size = 0;
                    
                    do {
                        size = listener.Receive(buffer);
                        acumBuffer.AddRange(buffer);
                    } while (listener.Available > 0);

                    listener.Shutdown(SocketShutdown.Both);
                    listener.Close();

                    
                    if (size > 0) {
                        List<DataPacket> receivedPackets = GetDataFromBuffer(acumBuffer.ToArray());
                        AddPacketsInSequence(receivedPackets);

                        PrintMessageReceivingPackets(receivedPackets.Count);

                        eventPacketSequenceAdded(receivedPackets);
                    }
                    else {
                        PrintMessage("Входящий узел восстановил соединение.");
                    }
                }
                catch (Exception ex) {
                    PrintMessage(ex.Message);
                }
            }
        }
        
        protected List<DataPacket> GetDataFromBuffer(byte[] buffer) {
            byte[] streamBuffer = new byte[BytesInCollection(buffer)];
            CopyFromTo(buffer, streamBuffer);

            MemoryStream memoryStream = new MemoryStream(streamBuffer);
            List<DataPacket> packets = DeserializeJson<DataPacket>(memoryStream);
            memoryStream.Close();

            return packets;
        }

        protected void SerializeJson<T>(List<T> objectsList, Stream stream) {
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<T>));
            try {
                jsonSerializer.WriteObject(stream, objectsList);
            }
            catch (Exception ex) {
                PrintMessage(ex.Message);
            }
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

        protected string ReadStreamString(string fileName) {
            string resultString = "";
            try {
                StreamReader stream = new StreamReader(fileName);
                resultString = stream.ReadLine().ToString();
                stream.Close();
            }
            catch (Exception ex) {
                PrintMessage(ex.Message);
            }
            return resultString;
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

        protected int BytesInCollection(byte[] collection) {
            int count = 0;
            while (collection[count] != 0) {
                count++;
            }
            return count;
        }

        protected void CopyFromTo(byte[] bufferFrom, byte[] bufferTo) {
            Array.Copy(bufferFrom, bufferTo, bufferTo.Length);
        }

        protected void PrintMessage(string message) {
            output(message); 
        }
        
        protected string InputData() {
            return input();
        }

        protected int GetInputData(ValueRequirement valueRequirement, string messageForInput = null) {
            int resultData = 0;
            while (true) {
                if (messageForInput != null) {
                    PrintMessage(messageForInput);
                }
                if (int.TryParse(InputData(), out int inputvalue) && valueRequirement(inputvalue)) {
                    resultData = inputvalue;
                    break;
                }
                else {
                    PrintMessage("Неверный ввод, повторите попытку!");
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

        protected IPAddress GetIpAdress() {
            string s = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostByName(Dns.GetHostName());
            IPAddress[] addr = ipEntry.AddressList;
            return addr[0];
        }
        
        void PrintMessageReceivingPackets(int packetsInMessage) {
            string messageEnding;
            if (packetsInMessage == 1) {
                messageEnding = "пакет";
            }
            else if ((packetsInMessage > 1) && (packetsInMessage < 5)) {
                messageEnding = "пакета";
            }
            else {
                messageEnding = "пакетов";
            }
            PrintMessage($"Получено сообщение. Содержит {packetsInMessage} {messageEnding}.");
        }

    }

    public delegate string InputDelegate();
    public delegate void OutputDelegate(string message);
    public delegate bool ValueRequirement(int value);
    public delegate void PacketSequenceState(List<DataPacket> addedPackets);

}

