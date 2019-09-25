using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
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

        // 1 объект
        public static byte[] GetBytes(DataPacket packet) {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DataPacket));
            MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, packet);
            byte[] bytesStream = stream.GetBuffer();// получаем буфер всего потока, а не только объекта (256)
            stream.Close(); // возможно закрытие потока автоматом при завершении метода

            // получаем массив байт одного объекта (36)
            int bytesCount = 0;
            while (bytesStream[bytesCount] != 0) {
                bytesCount++;
            }
            byte[] bytesPacket = new byte[bytesCount];
            Array.Copy(bytesStream, bytesPacket, bytesCount);


            //stream.Close(); // возможно закрытие потока автоматом при завершении метода
            return bytesPacket;
        }

        // массив объектов
        public static byte[] GetBytes(List<DataPacket> packets) {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<DataPacket>));
            MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, packets);
            byte[] bytesStream = stream.GetBuffer();// получаем буфер всего потока, а не только объекта (256)
            stream.Close(); // возможно закрытие потока автоматом при завершении метода

            // получаем массив байт одного объекта (36)
            int bytesCount = 0;
            while (bytesStream[bytesCount] != 0) {
                bytesCount++;
            }
            byte[] bytesPacket = new byte[bytesCount];
            Array.Copy(bytesStream, bytesPacket, bytesCount);


            //stream.Close(); // возможно закрытие потока автоматом при завершении метода
            return bytesPacket;
        }

        // TODO: сделать адекватным
        public static int GetObjectSize() {
            int bytesInObject = GetBytes(new DataPacket()).Length;
            return bytesInObject; // размер на 2 ьайта больше /=
        }

        public DataPacket() { }

        int CreateValue() {
            Random rand = new Random();
            int value = rand.Next(1, 1000);
            return value;
        }
    }
}
