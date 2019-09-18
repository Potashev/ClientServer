using ServerProject;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerAppWinForms {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        Server server;

        private void Form1_Load(object sender, EventArgs e) {

            server = new Server(Input, Print);
            server.Run(false);

        }

        void Print(string message) {
            richTextBox1.Text += message + "\n";
        }

        string Input() {
            return "0";
        }

        private void button1_Click(object sender, EventArgs e) {

        }
    }
}
