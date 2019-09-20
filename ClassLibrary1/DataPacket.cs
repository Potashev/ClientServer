using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ClientServerLib {
    [DataContract]
    public class DataPacket {
        [DataMember]
        public int _unitId;
        [DataMember]
        public int _number;
        [DataMember]
        public int _value;

        static int packetCount = 1;
        public DataPacket(int unitId) {
            _unitId = unitId;   // раньше unitId не передавался в конструктор, тк использовал id узла напрямую
            _number = packetCount;
            _value = CreateValue();
            packetCount++;
        }

        int CreateValue() {
            Random rand = new Random();
            int value = rand.Next(1, 1000);
            return value;
        }
    }
}
