using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    public partial class ClientForm : Form
    {
        private IPAddress _cacheaddress;
        private int _cacheport;
        private string _selectFileName;

        public ClientForm()
        {
            InitializeComponent();
            _cacheaddress = IPAddress.Loopback;
            _cacheport = 8082;
            label1.Text = "Ready to Connect";
            label2.Text = "Message";
        }


        //显示我想下载的文件名
        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1) //确保有一个选定的项
            {
                _selectFileName = listBox1.SelectedItem.ToString();
                label2.Text = _selectFileName;

            }
        }


        //申请文件列表
        private void button1_Click(object sender, EventArgs e)
        {
            _selectFileName = null;
            using (TcpClient tcpClient = new TcpClient())
            {
                tcpClient.Connect(_cacheaddress, _cacheport);
                using (NetworkStream stream_cc = tcpClient.GetStream())
                {
                    Byte command = 0;
                    try
                    {
                        Invoke(new Action(() => label2.Text = "click to downLoad"));
                        //向缓存发出指令
                        stream_cc.WriteByte(command);
                        stream_cc.Flush();

                        //从cache端回收信息
                        StreamReader reader = new StreamReader(stream_cc, Encoding.UTF8);

                        listBox1.Items.Clear(); // 清除列表中的所有项
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            listBox1.Items.Add(line); // 将文件名添加到列表框中
                        }
                        reader.Close();
                    }
                    catch (Exception ex)
                    {
                        Invoke(new Action(() => label2.Text = "Error"));
                    }
                }
            }
        }


        //下载文件,显示text文件内容
        private void button2_Click(object sender, EventArgs e)
        {
            using (TcpClient tcpClient = new TcpClient())
            {
                tcpClient.Connect(_cacheaddress, _cacheport);
                using (NetworkStream stream_cc = tcpClient.GetStream())
                {
                    Byte command = 1;

                    if (_selectFileName != null)
                    {
                        //给cache的文件名字节数据和长度数据
                        byte[] fileNameBytes = Encoding.UTF8.GetBytes(_selectFileName);
                        byte[] fileNameLengBytes = BitConverter.GetBytes(fileNameBytes.Length);

                        try
                        {
                            //向缓存发指令
                            stream_cc.WriteByte(command);
                            stream_cc.Flush();

                            //发送文件名长度和文件名
                            stream_cc.Write(fileNameLengBytes, 0, 4);
                            stream_cc.Write(fileNameBytes, 0, fileNameBytes.Length);
                            stream_cc.Flush();


                            // 在此处处理来自缓存的响应，例如将文件内容显示在客户端窗口中
                            // 读取文件内容长度
                            byte[] contentLengthBytes = new byte[4];
                            stream_cc.Read(contentLengthBytes, 0, 4);
                            int contentLength = BitConverter.ToInt32(contentLengthBytes, 0);
                            Invoke(new Action(() => label2.Text = "Content"));

                            // 读取文件内容
                            byte[] contentBytes = new byte[contentLength];

                            using (BinaryReader reader = new BinaryReader(stream_cc, Encoding.UTF8, leaveOpen: true))
                            {
                                reader.Read(contentBytes, 0, contentLength);
                                string fileContent_txt = Encoding.UTF8.GetString(contentBytes);
                                Invoke(new Action(() => textBox1.Text = $"{fileContent_txt}"));

                            }

                            //using (MemoryStream imageStream = new MemoryStream(contentBytes)) 
                            {
                                //Image image = Image.FromStream(imageStream);
                                //Invoke(new Action(() => pictureBox1.Image = image));

                            }



                        }
                        catch (Exception ex)
                        {
                            Invoke(new Action(() => label2.Text = $"DownLoadError: {ex.Message}"));
                        }
                    }
                    else
                    {
                        Invoke(new Action(() => label2.Text = "DownLoadError: haven't chooose file"));
                    }


                }
            }

        }

        //接收图片拼接
        private void button3_Click(object sender, EventArgs e)
        {
            using (TcpClient tcpClient = new TcpClient())
            {
                tcpClient.Connect(_cacheaddress, _cacheport);
                using (NetworkStream stream_cc = tcpClient.GetStream())
                {

                    if (_selectFileName != null)
                    {

                        Byte command = 1;

                        //给cache的文件名字节数据和长度数据
                        byte[] fileNameBytes = Encoding.UTF8.GetBytes(_selectFileName);
                        byte[] fileNameLengBytes = BitConverter.GetBytes(fileNameBytes.Length);
                        try
                        {

                            //向缓存发指令
                            stream_cc.WriteByte(command);
                            stream_cc.Flush();

                            //发送文件名长度和文件名
                            stream_cc.Write(fileNameLengBytes, 0, 4);
                            stream_cc.Write(fileNameBytes, 0, fileNameBytes.Length);
                            stream_cc.Flush();


                            // 在此处处理来自缓存的响应，例如将文件内容显示在客户端窗口中
                            // 读取文件内容长度
                            byte[] contentLengthBytes = new byte[4];
                            stream_cc.Read(contentLengthBytes, 0, 4);
                            int contentLength = BitConverter.ToInt32(contentLengthBytes, 0);

                            // 读取文件内容
                            byte[] contentBytes = new byte[contentLength];
                            stream_cc.Read(contentBytes, 0, contentLength);
                            stream_cc.Flush();


                            using (MemoryStream imageStream = new MemoryStream(contentBytes))
                            {
                                Image image = Image.FromStream(imageStream);
                                Invoke(new Action(() => pictureBox1.Image = image));

                            }

                        }
                        catch (Exception ex)
                        {
                            Invoke(new Action(() => label2.Text = $"DownLoadError: {ex.Message}"));
                        }
                    }
                    else
                    {
                        Invoke(new Action(() => label2.Text = "DownLoadError: haven't chooose file"));
                    }


                }
            }
        }

        private void ClientForm_Load(object sender, EventArgs e)
        {

        }


        
    }
}
