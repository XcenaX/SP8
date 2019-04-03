using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using Client;

namespace SocketApp
{
    public partial class Client : Form
    {
        public Client()
        {
            InitializeComponent();
            txtIp.Text = "172.27.9.17";
            txtPort.Text = "12345";
            connectButton.Enabled = true;
            disconnectButton.Enabled = false;
            sendButton.Enabled = false;
        }
        Socket socket;
        Thread threadConnect = null;
        Mutex mtxTxtRecive = new Mutex(false);
        bool IsDisconnect = false;
        EventWaitHandle envSend = new EventWaitHandle(false,EventResetMode.AutoReset);

        private void connectButton_Click(object sender, EventArgs e)
        {
            
            
            IPAddress ipServer = null;
            try
            {
                ipServer = IPAddress.Parse(txtIp.Text);   
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR");
                return;
            }

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (socket == null) return;

            int portServer = Convert.ToInt32(txtPort.Text);
            var srvAddres = new IPEndPoint(ipServer, portServer);
            try
            {
                socket.Connect(srvAddres);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR");
                return;
            }
                        
            connectButton.Enabled = false;
            disconnectButton.Enabled = true;
            sendButton.Enabled = true;

            threadConnect = new Thread(ConnectFunction)
            {
                IsBackground = true
            };
            threadConnect.Start(socket);
        }

        private void ConnectFunction(object obj)
        {
            Socket socket = obj as Socket;
            
            StateObject state = new StateObject();
            state.workSocket = socket;
            //envSend.Reset();
            while (!IsDisconnect)
            {                 
                socket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
                envSend.WaitOne();
                //envSend.Reset();

                //ar.AsyncWaitHandle.WaitOne();
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {                
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;
                                
                int bytesRead = client.EndReceive(ar);                
                if (bytesRead > 0)
                {                    
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));                    
                    RecieveOut(Encoding.ASCII.GetString(state.buffer));
                    
                    //client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                      //  new AsyncCallback(ReceiveCallback), state);
                    envSend.Set();
                }                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void RecieveOut(string str)
        {
            mtxTxtRecive.WaitOne();
            txtRecieve.Invoke(new Action<string>((x) => { txtRecieve.Text += x; }),str);
            mtxTxtRecive.ReleaseMutex();
        }

        private void disconnectButton_Click(object sender, EventArgs e)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            socket.Dispose();
            socket = null;
            connectButton.Enabled = true;
            sendButton.Enabled = false;
            disconnectButton.Enabled = false;
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            socket.Send(Encoding.ASCII.GetBytes(txtSend.Text));
            //envSend.Set();
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
