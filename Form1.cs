using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using ARSTConfig;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Security.Principal;
using System.Threading;
using System.Runtime.CompilerServices;

namespace routApiTest
{
    public partial class Form1 : Form
    {
        ProtocolClient protocol;
        aConfg confg = new aConfg();

        int aliveInterval = 0;
        string targetDeviceName = "";

        public Form1()
        {
            InitializeComponent();
            appendLog("Initializing application...");
            
            try
            {
                appendLog("Reading config...");
                confg.init(Path.Combine(Application.StartupPath, "cfg.ini"));
                
                textBox1.Text = confg.read("fullURL");
                textBoxReceiverName.Text = confg.read("receiverName");
                textBoxSenderName.Text = confg.read("senderName");
                targetDeviceName = textBoxSenderName.Text;
                txtAccessKey.Text = confg.read("key");

                appendLog("Initialize complete!\n\n\n");
            }
            catch(Exception ex)
            {
                error(ex.Message, "APPLICATION_INIT_ERROR");
            }
        }

        void appendLog(string text)
        {
            richTextBox1.AppendText(text + "\n");
        }

        void error(string text, string code)
        {
            MessageBox.Show(text, code, MessageBoxButtons.OK, MessageBoxIcon.Error);
            appendLog($"Error::{code}: {text}");
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            string serverUrl = textBox1.Text;

            try
            {
                if (string.IsNullOrWhiteSpace(serverUrl)) throw new Exception("Серверный URL не должен содержать одни пробелы.");
                protocol = new ProtocolClient(serverUrl, targetDeviceName, txtAccessKey.Text);

                protocol.OnLog += Protocol_OnLog;
                protocol.OnMessage += (msg) =>
                {
                    if(msg.Length < 50) appendLog($"\n--- Данные с сервера: {msg} ---");
                    else appendLog($"\n--- Число символов строки данных с сервера: {msg.Length} ---");

                    if (msg.StartsWith("STATUS"))
                    {
                        msg = msg.Remove(0, 7);
                        appendLog("Статусное сообщение принято: " + msg);

                        try
                        {
                            if (msg.StartsWith("YOU_CONNECTED"))
                            {
                                string raw = msg.Split(':')[1].Trim();
                                int ttl = Convert.ToInt32(raw);
                                aliveInterval = (ttl - 10) * 1000;

                                keepAliveTimer.Interval = aliveInterval;
                                keepAliveTimer.Start();

                                appendLog($"Heartbeat interval set to {aliveInterval / 1000} seconds.");
                            }
                            else if (msg.StartsWith("INVALID")) appendLog("Server error status: " + msg);
                            else if (msg.StartsWith("OPERATION_SUCCESS")) appendLog("Серверная операция обработана успешно.");
                        }
                        catch (Exception ex)
                        {
                            error(ex.Message, "STATUS_PARSE_ERROR");
                        }
                    }
                    else
                    {
                        appendLog("Parsing message...");

                        try
                        {
                            string usrName = msg.Split(':')[0];
                            string messageText = msg.Remove(0, usrName.Length + 1);

                            appendToChat($"{usrName}: \"{messageText}\"");
                        }
                        catch(Exception ex)
                        {
                            error(ex.Message, "MESSAGE_PARSE_ERROR");
                        }
                    }
                };

                protocol.OnConnected += () =>
                {
                    appendLog("Connected to server!");
                    sendKeepAlive();
                };

                protocol.OnDisconnected += () =>
                {
                    appendLog("Disconnected.");
                    keepAliveTimer.Stop();
                };

                await protocol.StartAsync();
            }
            catch (Exception ex)
            {
                error("Target url: " + serverUrl + ": " + ex.Message, "CONNECTING_ERROR");
            }
        }

        private void Protocol_OnLog(string obj)
        {
            appendLog("[ PROTOCOL CLIENT ] " + obj);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text != "") button1.Enabled = true;
            else button1.Enabled = false;
        }

        void appendToChat(string text)
        {
            richTextBox3.AppendText(text + "\n");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string json = JsonConvert.SerializeObject(new
            {
                type = "SESSION_START",
                from = targetDeviceName,
                to = textBoxReceiverName.Text,
                accessKey = txtAccessKey.Text,
                payload = richTextBox2.Text
            });

            richTextBox2.Clear();
            protocol.Send(json);
            appendLog($"Sent SESSION_START('{json}')");
        }

        private void keepAliveTimer_Tick(object sender, EventArgs e)
        {
            sendKeepAlive();
        }

        void sendKeepAlive()
        {
            if (protocol != null) protocol.SendKeepAlive(targetDeviceName);
        }

        private void textBoxReceiverName_TextChanged(object sender, EventArgs e)
        {
            
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (protocol != null)
            {
                string json = $"{{\"type\":\"CONN_STOP\"}}";
                protocol.Send(json);
                protocol.Stop();
                keepAliveTimer.Stop();
                appendLog($"Sent CONN_STOP and closed connection('{json}')");
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            appendLog("Saving...");

            try
            {
                confg.write("fullURL", textBox1.Text);
                confg.write("senderName", textBoxSenderName.Text);
                confg.write("receiverName", textBoxReceiverName.Text);
                confg.write("key", txtAccessKey.Text);
            }
            catch (Exception ex)
            {
                error(ex.Message, "SAVING_ERROR");
            }
        }

        private void textBoxSenderName_TextChanged(object sender, EventArgs e)
        {
            targetDeviceName = textBoxSenderName.Text;
        }
    }
}
