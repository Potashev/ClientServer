﻿using ClientServerLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerProject {
    public class Server: Node {

        //static object locker = new object();    // TODO: попробовать перенести locker в SendNeibs
        private bool createTopology;

        public Server(InputDelegate userInput, OutputDelegate userOutput, int acceptPort, bool createTopology = false) : base(userInput, userOutput) {

            AcceptPort = acceptPort;
            this.createTopology = createTopology;

            eventPacketSequenceAdded += PrintNewPackets;
        }

        public override void Run() {

            if (createTopology) {
                PrintMessage("Формирование топологии");
                //PrintMessage("Введите число подключений");
                InputConnectionsCount(out int unitsCount);
                ConnectAllForTopology(unitsCount);
            }

            PrintMessage("Прием пакетов...");

            ReceivingProccess();
        }

        void PrintNewPackets(List<DataPacket> packets) {
                foreach (DataPacket p in packets)
                    PrintMessage(p.GetInfo());
        }

        

        //bool NotEmpty(byte[] buffer) {
        //    for(int i=0; i< buffer.Length; i++) {
        //        if (buffer[i] != 0)
        //            return true;
        //    }
        //    return false;
        //}
        

        void ConnectAllForTopology(int clientsCount) {

            // TODO: возможно позже здесь формировать список клиентов и передавать в методы

            List<TopologyClient> сlients = new List<TopologyClient>();
            OpenConnectForTopology(clientsCount, сlients);
            CreateTopology(сlients);
            CloseConnectionForTopology(сlients);
            сlients.Clear();

        }

        void OpenConnectForTopology(int clientsCount, List<TopologyClient> clients) {
            //Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //serverSocket.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT_FOR_TOPOLOGY));
            //serverSocket.Listen(1);


            RunAcceptSocket(SERVER_PORT_FOR_TOPOLOGY, out Socket serverSocket);

            PrintMessage($"Ожидаемое число подключений: {clientsCount}");
            PrintMessage("Ожидание подключений...");
            for (int i = 0; i < clientsCount; i++) {
                Socket socket = serverSocket.Accept();
                PrintMessage("Новое подключение");
                TopologyClient newClient = new TopologyClient(socket);
                clients.Add(newClient);
            }
            PrintMessage("Все подключились");

            ////serverSocket.Shutdown(SocketShutdown.Both);
            //serverSocket.Close();

            CloseSocket(serverSocket);


        }

        void CreateTopology(List<TopologyClient> clients) {
            ShowClients(clients);
            foreach (TopologyClient client in clients) {
                List<Neighbour> Neibs = GetNeibsForClient(client, clients);
                ConvertNeibsForSending(Neibs, out byte[] buffer);
                Neibs.Clear();
                SendNeibs(client, buffer);
                PrintMessage("Топология отправлена " + client.GetIp());
            }
        }

        void ShowClients (List<TopologyClient> clients){
            PrintMessage("Узлы топологии:");
            string ip;
            int id;
            foreach (TopologyClient cl in clients) {
                ip = Convert.ToString(cl._socket.RemoteEndPoint);
                id = cl._id;
                PrintMessage($"ip:port = {ip}, id = {id}");
            }
            PrintMessage("\n*id=0 - сервер");
        }

        
        

        //delegate bool ValueRequirement(int value);

        //int GetInputData(ValueRequirement requirement) {
        //        int resultData = 0;
        //        while (true) {
        //            if (int.TryParse(InputData(), out int inputvalue) && requirement(inputvalue)) {
        //                resultData = inputvalue;
        //                break;
        //            }
        //            else {
        //                PrintMessage("Неверный ввод, повторите попытку:");
        //            }
        //        }
        //        return resultData;
            
        //}

        //int CheckInputData(string stringValue, ValueRequirement requirement) {
        //    if(int.TryParse(stringValue, out int inputValue) && requirement(inputValue)) {
        //        return inputValue;
        //    }
        //    else {
        //        PrintMessage("Неверный ввод, повторите попытку:");
        //        return GetInputData(requirement);
        //    }
        //}

        void InputConnectionsCount(out int count) {
            count = GetInputData(x => (x > 0), "Введите число подключений:");
        }

        void InputNeighbourPriority(out int priority) {
            priority = GetInputData(x => (x > 0 || x == -1), "Приоритет:");
        }

        // TODO: Добавить проверку на дурака
        int? InputNeighbourId(int clientsCount, int clientId) {
            string inputMessage = "Id:";
            PrintMessage(inputMessage);
            string strId = InputData();
            if (strId == "") {
                return null;
            }
            else {
                int id = CheckInputData(x => (x >= 0 && x <= clientsCount && x != clientId), strId, inputMessage);
                return id;
            }
        }

        // TODO: Добавить проверку на дурака
        //int InputNeibPriority() {
        //    PrintMessage("Приоритет:");
        //    //int priority = int.Parse(InputData());
        //    int priority = GetInputData(x => (x > 0 || x == -1));
        //    return priority;
        //}

        List<Neighbour> GetNeibsForClient(TopologyClient client, List<TopologyClient> clients) {

            string clientIp = client.GetIpPort();
            int clientId = client._id;
            PrintMessage($"Для узла {clientIp} (id = {clientId}):");
            List<Neighbour> neibs = new List<Neighbour>();
            while (true) {
                //PrintMessage("Id:");
                int? id = InputNeighbourId(clients.Count, clientId);

                // проверка значения id на выход из цикла
                if(id == null) {
                    break;
                }

                if (id == 0) {
                    Neighbour NeibServer = new Neighbour(SERVER_IP, 1, AcceptPort);
                    neibs.Add(NeibServer);
                    continue;
                }

                foreach (TopologyClient cl in clients) {

                    if (cl._id == id) {
                        PrintMessage("Приоритет:");
                        InputNeighbourPriority(out int priority);
                        string ip = cl.GetIp();
                        int port = cl._acceptPort;
                        Neighbour n = new Neighbour(ip, priority, port);
                        neibs.Add(n);
                        break;
                    }
                }

            }

            return neibs;
        }
        
        void SendNeibs(TopologyClient client, byte[] buffer) {
            lock (locker) {
                client._socket.Send(buffer);


                string clientId = Convert.ToString(client._id);
                client._socket.Send(Encoding.ASCII.GetBytes(clientId));
                // TODO: убрать либо сделать меньше
                Thread.Sleep(100);
                string clientAcceptPort = Convert.ToString(client._acceptPort);
                client._socket.Send(Encoding.ASCII.GetBytes(clientAcceptPort));
            }
        }

        void ConvertNeibsForSending(List<Neighbour> neibs, out byte[] buffer) {
            MemoryStream memoryStream = new MemoryStream();
            SerializeJson(neibs, memoryStream);
            buffer = memoryStream.GetBuffer();
            memoryStream.Close();
        }

        void CloseConnectionForTopology(List<TopologyClient> clients) {
            foreach (TopologyClient cl in clients) {
                ////cl._socket.Shutdown((SocketShutdown.Both));
                //cl._socket.Close();
                CloseSocket(cl._socket);
            }
        }
    }

    class TopologyClient {
        public Socket _socket;
        public int _id;
        // порт приема каждого узла (теперь можно на одном компе запускать несколько клиентов)
        public int _acceptPort;

        static int clientCounter = 1; // серв - id 0

        static int acceptPortCounter = 660;

        public TopologyClient(Socket socket) {
            _socket = socket;
            _id = clientCounter;
            _acceptPort = acceptPortCounter;
            clientCounter++;
            acceptPortCounter++;
        }

        public string GetIpPort() {
            string ipPort = Convert.ToString(_socket.RemoteEndPoint);
            return ipPort;
        }

        // мб найти получше
        public string GetIp() {
            string ipAndPort = Convert.ToString(_socket.RemoteEndPoint);
            int index = ipAndPort.IndexOf(":");
            string ip = ipAndPort.Remove(index);
            return ip;
        }
    }
}
