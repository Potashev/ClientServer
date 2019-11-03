using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ClientServerLib {
    [DataContract]
    public class Neighbour {

        [DataMember]
        public string Ip { get; set; }
        //public string ip;
        
        [DataMember]
        public int Priority { get; set; }
        /*public int priority;*/        // пока >0 - отправка (с приоритетом), -1 - прием

        [DataMember]
        public int AcceptPort { get; set; }

        public Socket Socket { get; set; }
        //public Socket socket;       // нужны только у узлов отправки (сокет + приоритет = динам маршрут)

        public bool IsDied { get; set; }
        //public bool died;       // нужны только у узлов отправки

        public Neighbour(string addr, int weight, int port) {
            Ip = addr;
            Priority = weight;
            AcceptPort = port;
            IsDied = false;
        }

        public bool IsForSending() {
            if(Priority > 0) {
                return true;
            }
            else {
                return false;
            }
        }

        public bool IsForReceiving() {
            if (Priority == 1) {
                return true;
            }
            else {
                return false;
            }
        }

        public bool IsBest() {
            if(Priority == 1) {
                return true;
            }
            else {
                return false;
            }
        }

        public bool IsBetterThan(Neighbour otherNeighbour) {
            if (this.Priority <= otherNeighbour.Priority) { // TODO: убрать =
                return true;
            }
            else {
                return false;
            }
        }

        // ф-я сравнения для сортировки списка по возрастанию (-1, -1... 1, 2...)
        // мб найти другую сортировку ( 1, 2... -1, -1...)
        public static int CompareUnitsByPriority(Neighbour x, Neighbour y) {
            if (x.Priority == y.Priority)
                return 0;
            if (x.Priority < y.Priority)
                return -1;
            else
                return 1;
        }




    }
}
