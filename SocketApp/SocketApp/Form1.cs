using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace SocketApp
{
    public partial class Form1 : Form
    {

        private Mutex mutexOnce;// для однократного запуска сервера
        private Mutex mutexLog;// для защиты журналов
        private List<Client> clients = new List<Client>();
        EventWaitHandle receiveDone = new EventWaitHandle(false, EventResetMode.AutoReset);

        bool isStop;

        public Form1()
        {
            isStop = false;
            
            bool isCreated;
            mutexOnce = new Mutex(true, "ServerOnceMutex", out isCreated);

            if (!isCreated)
            {
                MessageBox.Show("Сервер уже запущен!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                System.Environment.Exit(1);                
            }

            mutexLog = new Mutex(false);

            InitializeComponent();

            txtPort.Text = "12345";
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());
            txtLog.Text = Dns.GetHostName() + "\r\n";

            ipList.Items.Add("0.0.0.0");

            foreach(var addres in addresses)
            {
                txtLog.Text += addres.ToString() +"\r\n";
                ipList.Items.Add(addres.ToString());
            }

            ipList.SelectedItem = ipList.Items[0];
        }

        Thread threadServer = null;

        private void startButton_Click(object sender, EventArgs e)
        {
            if (threadServer != null)
            {
                MessageBox.Show("Сервер уже запущен!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            threadServer = new Thread(ServerRoutine)
            {
                IsBackground = true
            };
            threadServer.Start(this);
            startButton.Enabled = false;
            stopButton.Enabled = true;
        }

        Socket socketServer = null;

        private void ServerRoutine(object obj)
        {
            Form1 form = obj as Form1;
            if (form == null) return;

            string ipAddres = ipList.Items[ipList.SelectedIndex].ToString();

            //ipList.Invoke(new Action<string>((x) => {
            //    x = ipList.SelectedItem as String;
            //}),ipAddres);

            int port = 0;

            try
            {
                port = Convert.ToInt32(txtPort.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Порт должен состоять только из цифр", "Ошибка", MessageBoxButtons.OK,MessageBoxIcon.Error);
                return;
            }

            socketServer = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipServer = IPAddress.Parse(ipAddres);
            IPEndPoint endPoint = new IPEndPoint(ipServer, port);

            socketServer.Bind(endPoint); // подключится к серверу
            socketServer.Listen(100); //100 - это кол-во клиентов которые могут одновременно обслуживатся            

            ThreadPool.SetMaxThreads(30,30);
            ThreadPool.SetMinThreads(20,20);

            while (!isStop)
            {
                Socket client = socketServer.Accept();
                if (client != null)
                {
                    ThreadPool.QueueUserWorkItem(ClientRoutine, client);
                }
            }
        }

        private void ClientRoutine(object obj)
        {
            
            var client = obj as Socket;

            clients.Add(new Client()
            {
                socket = client
            });

            var message = "You are connected!";
            try
            {
                client.Send(Encoding.ASCII.GetBytes(message));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            try
            {
                
                StateObject state = new StateObject();
                state.workSocket = client;
                //receiveDone.Reset();
                while (true)
                {
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                    receiveDone.WaitOne();
                }
            }
            catch (Exception ex)
            {
                LogOut("Error: " + ex.Message + "\r\n");
                //Здесь нужно будет учесть в глобальном списке что клиент отключился
                var item = clients.Where(x => x.socket == client).ToList()[0];
                clients.Remove(item);
                client.Close();
                return;
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

                    LogOut(Encoding.ASCII.GetString(state.buffer));
                    foreach (var item in clients)
                    {
                        item.socket.Send(state.buffer);
                    }
                    //client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    //    new AsyncCallback(ReceiveCallback), state);
                    receiveDone.Set();
                }                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void LogOut(string message)
        {
            mutexLog.WaitOne();
            txtLog.Invoke(new Action<string>((x) => {
                txtLog.Text += x;
            }),message); // x == message
            mutexLog.ReleaseMutex();
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            startButton.Enabled = true;
            stopButton.Enabled = false;
        }
    }
}
