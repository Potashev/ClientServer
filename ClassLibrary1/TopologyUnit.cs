using System;
using System.Net.Sockets;

namespace ClientServerLib {
    class TopologyUnit {
        public Socket Socket { get; set; }
        public int Id { get; set; }
        public int AcceptPort { get; set; }

        private static int clientCounter = 1;
        private static int acceptPortCounter;
        
        public TopologyUnit(Socket socket) {
            Socket = socket;
            Id = clientCounter;
            AcceptPort = acceptPortCounter;

            clientCounter++;
            acceptPortCounter++;
        }

        public string GetIpPort() {
            string ipPort = Convert.ToString(Socket.RemoteEndPoint);
            return ipPort;
        }
        
        public string GetIp() {
            string ipAndPort = Convert.ToString(Socket.RemoteEndPoint);
            int index = ipAndPort.IndexOf(":");
            string ip = ipAndPort.Remove(index);
            return ip;
        }

        public static void SetStartPortValue(int ServerAcceptPort) {
            acceptPortCounter = ServerAcceptPort + 1;
        }
    }
}
