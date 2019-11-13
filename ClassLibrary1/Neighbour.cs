
using System.Runtime.Serialization;

namespace ClientServerLib {
    [DataContract]
    class Neighbour {

        [DataMember]
        public string Ip { get; set; }
        
        [DataMember]
        public int Priority { get; set; }

        [DataMember]
        public int AcceptPort { get; set; }

        public bool IsDied { get; set; }

        private static int receivingPriorityValue = 0;
        
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
            if (Priority == receivingPriorityValue) {
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
            if (this.Priority <= otherNeighbour.Priority) {
                return true;
            }
            else {
                return false;
            }
        }

        public static string GetPriorityValueInfo() {
            string messageInfo =
                $"Допустимые значения приоритета: {receivingPriorityValue} - прием, 1,2,3.. - отправка.";
            return messageInfo;
        }

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
