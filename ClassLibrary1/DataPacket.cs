using System;
using System.Runtime.Serialization;

namespace ClientServerLib {
    [DataContract]
    public class DataPacket {
        [DataMember]
        private int clientId;
        [DataMember]
        private int packetNumber;
        [DataMember]
        private int datavalue;

        
        static int packetCounter = 1;

        public DataPacket(int nodeId) {
            clientId        = nodeId;
            packetNumber    = packetCounter;
            datavalue       = CreateValue();
            packetCounter++;
        }

        public string GetInfo() {
            string packetInfo = $"Узел: {clientId}, пакет: {packetNumber} , значение: {datavalue}";
            return packetInfo;
        }

        private int CreateValue() {
            Random rand = new Random();
            int value = rand.Next(1, 1000);
            return value;
        }

        
    }
}
