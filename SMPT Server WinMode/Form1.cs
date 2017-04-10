using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text.RegularExpressions;

namespace SMPT_Server_WinMode
{
   
    public partial class Form1 : Form
    {
        private TcpListener server;
        private Socket socket;
        private TcpClient client = null;
        private bool is_running = false;
        private string server_name = "server.example.com";
        private bool client_logged = false;
        private bool read_mail_message = false;

        public StreamReader STR;
        public StreamWriter STW;
        public string text_to_send;
        public string received_message;

        private int bufferSize = 1024;
        private byte[] buffer=null;
        private bool ready_to_save_file = false;
        private int _file_size = 0;
        private string _file_name = "";
        private bool receiving_file = false;

        private LinkedList<Users> userDatabase=new LinkedList<Users>();

        public Form1()
        {
            InitializeComponent();


            IPAddress[] localIP = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress address in localIP)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    IPTextBox.Text = address.ToString();
                }
            }


        }

       
        //start serwera
        private void button1_Click(object sender, EventArgs e) //start server
        {
            try {
                if (Regex.IsMatch(IPTextBox.Text, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b")&&!is_running)
                {
                    server = new TcpListener(IPAddress.Parse(IPTextBox.Text), int.Parse(PortTextBox.Text));
                    server.Start();

                    ReadUserDataFromFile("userdata.txt");


                    TextConsole.AppendText("Server started\n");
                    TextConsole.AppendText("Waiting for client...");

                    client = server.AcceptTcpClient();
                    socket = client.Client;         
                    TextConsole.AppendText("Client connected \n");

                    STR = new StreamReader(client.GetStream());
                    STW = new StreamWriter(client.GetStream());
                    STW.AutoFlush = true;
                    is_running = true;

                    STW.WriteLine("220 " + server_name + " "+ DateTime.Now.ToString("MM\\/dd\\/yyyy h\\:mm tt"));

                    backgroundWorker1.WorkerSupportsCancellation = true;
                    backgroundWorker1.RunWorkerAsync(); //receiving data
                    

                } else
                {
                    if (is_running)
                        MessageBox.Show("Already started\n");
                    else
                        MessageBox.Show("Wrong IP Address");
                }
            }
            catch(Exception exc)
            {
                MessageBox.Show(exc.Message);
            }

        }

        //wysylanie wiadomosci do klienta
        private void SendTextMessage(string message)
        {
            if (message != "")
            {
                if (client.Connected)
                {
                    STW.WriteLine(message);
                    //this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText("To client :" + message + "\n"); }));
                }
                else
                {
                    MessageBox.Show("Send failed");
                }
            }

        }

        //odbieranie wiadomosci i rozpoznawanie komend
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            while (client.Connected)
            {
                try
                {
                    if (ready_to_save_file)
                    {
                        this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText("Saving file"+ "\n"); }));
                        SaveAsFile();
                        ready_to_save_file = false;
                    }

                    else
                    {
                        
                        received_message = STR.ReadLine();
                        if (received_message != "")
                        {
                            //this.TextConsole.Invoke(new MethodInvoker( delegate() { TextConsole.AppendText("From Client: "+received_message + "\n"); }));

                            if (received_message.Length > 3&&received_message.Substring(0,4) == "Size")
                            {
                                _file_size = int.Parse(received_message.Substring(5));
                                this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText(received_message + "\n"); }));
                                SendTextMessage("250 Size Ok");
                            }


                            if(received_message.Length>7&&received_message.Substring(0,8)=="FileName")
                            {
                                this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText(received_message + "\n"); }));
                                _file_name = received_message.Substring(9);
                                ready_to_save_file = true;
                                SendTextMessage("250 Ready");
                            }
                            if (read_mail_message)
                            {

                                if (received_message == ".")
                                {
                                    read_mail_message = false;
                                }
                                    this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText(received_message + "\n"); }));

                            }
                            if(received_message=="No files")
                            {
                                this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText(received_message + "\n"); }));
                            }

                            if (!client_logged&&received_message.Substring(0,4)=="user")
                            {
                                bool user_found = false;
                                foreach (Users user in userDatabase)
                                {
                                    if (user.username == received_message.Substring(5,received_message.Length-5))
                                    {
                                        SendTextMessage("250 User Ok");
                                        user_found = true;
                                    }
                                }
                                if(!user_found)
                                {
                                    MessageBox.Show("Wrong username");
                                }
                            }

                            if (!client_logged&&received_message.Substring(0,4)=="pass")
                            {
                                bool pass_found = false;
                                foreach (Users user in userDatabase)
                                {
                                    if (user.password == received_message.Substring(5, received_message.Length - 5))
                                    {
                                        SendTextMessage("250 Pass Ok");
                                        pass_found = true;
                                        client_logged = true;
                                    }
                                }
                                if(!pass_found)
                                {
                                    MessageBox.Show("Wrong password");
                                }
                            }


                            if(received_message.Length>9&&received_message.Substring(0,10)=="mail from:")
                            {
                                this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText(received_message + "\n"); }));
                                SendTextMessage("250 Ok");
                            }

                            if(received_message.Length>7&&received_message.Substring(0,8)=="rcpt to:")
                            {
                                this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText(received_message + "\n"); }));
                                SendTextMessage("250 Accepted");
                            }


                            if (received_message.Length >= 4)
                            {
                                switch (received_message.Substring(0, 4))
                                {
                                    case "helo":
                                        {
                                            this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText(received_message + "\n"); }));
                                            STW.WriteLine("250 Hello "+ received_message.Substring(5,received_message.Length-5)+" " + server_name + " here!");
                                            break;
                                        }
                                    case "data":
                                        {
                                            this.TextConsole.Invoke(new MethodInvoker(delegate () { TextConsole.AppendText(received_message + "\n"); }));
                                            SendTextMessage("354 Ok Send data");
                                            read_mail_message = true;
                                            break;
                                        }
                                    default:
                                        {
                                            break;
                                        }
                                }


                            }
                            received_message = "";
                        }
                    }
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message.ToString());
                }
            }
        }


        private void ReadUserDataFromFile(string path)
        {
            FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read);

            StreamReader stream = new StreamReader(path);
            
            while (!stream.EndOfStream)
            {
                string login = stream.ReadLine();
                string password = stream.ReadLine();         

                this.userDatabase.AddLast(new Users(login,password));
            }
            file.Close();
        }

        private void TextConsole_TextChanged(object sender, EventArgs e)
        {

        }

        private void ChatBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void SaveAsFile()
        {
                NetworkStream stream = client.GetStream();

                FileStream fs = new FileStream(_file_name, FileMode.OpenOrCreate);

                while (_file_size > 0)
                {
                    buffer = new byte[bufferSize];
                    int size = socket.Receive(buffer, SocketFlags.Partial);
                    fs.Write(buffer, 0, size);
                    _file_size -= size;

                }
            receiving_file = false;
            fs.Close();

        }

    }

    public class Users
    {
        public string username;
        public string password;

        public Users(string name,string pass)
        {
            username = name;
            password = pass;
        }


    }




}

