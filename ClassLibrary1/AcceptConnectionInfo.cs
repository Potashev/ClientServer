using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientServerLib {
    // входящие сокеты (работают в потоках), связывает сокет с потоком
    // есть connectionInfo в server?
    public class AcceptConnectionInfo {
        public Socket Socket;
        public Thread Thread;
    }
}
