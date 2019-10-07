using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ClientServerLib {
    // соседи узла в топологии
    // TODO: переименовать в соседа (neib..)?
    [DataContract]
    public class Neighbour {
        [DataMember]
        public string ip;
        [DataMember]
        public int priority;        // пока >0 - отправка (с приоритетом), -1 - прием
        [DataMember]
        public int acceptPort;

        public Socket socket;       // нужны только у узлов отправки (сокет + приоритет = динам маршрут)

        public bool died;       // нужны только у узлов отправки

        public Neighbour(string addr, int weight, int port) {
            ip = addr;
            priority = weight;

            acceptPort = port;

            //activity = true;
            died = false;
        }
    }
}
