using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace Cache
{
    public partial class CacheForm : Form
    {
        private IPAddress _serverAddress;
        private int _serverPort;
        private string _selectFileName;

        public CacheForm()
        {
            InitializeComponent();
            label2.Text = "Waiting Client Connection...";
            _serverAddress = IPAddress.Loopback;
            _serverPort = 8081;
            //获取server_data路径
            string FolderPath_A = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\CacheData");
            FolderPath_A = Path.GetFullPath(FolderPath_A);

            if (!Directory.Exists(FolderPath_A))
            {
                Directory.CreateDirectory(FolderPath_A);
            }

            //获取文件夹中的文件名
            string[] fileNames_A = Directory.GetFiles(FolderPath_A);



            for (int i = 0; i < fileNames_A.Length; i++)
            {
                fileNames_A[i] = Path.GetFileName(fileNames_A[i]);
            }

            //把文件名添加到listBox中
            foreach (string fileName in fileNames_A)
            {
                listBox1.Items.Add(fileName);
            }

        }

        public void StartCache()
        {
            IPAddress iPAddress = IPAddress.Loopback;
            int port_cache = 8082;

            TcpListener clientlistener = new TcpListener(iPAddress, port_cache);
            clientlistener.Start();

            while (true)
            {
                var client = clientlistener.AcceptTcpClient();
                Invoke(new Action(() => label2.Text = "Client connected"));

                // 使用 Task.Run 处理连接
                Task.Run(async () => await HandleClientConnection(client));
            }
        }

        //防止线程卡顿死循环
        private async Task HandleClientConnection(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte command = (byte)stream.ReadByte();

            await HandleClientCommand(command, client);
            client.Close();
        }

        //连接server方法调用--因为后面的每个comand都需要重新新建TCP
        public TcpClient GetServerConnection()
        {
            TcpClient tcpCache = new TcpClient();
            tcpCache.Connect(_serverAddress, _serverPort);
            Invoke(new Action(() => label1.Text = "Connect to server"));
            return tcpCache;

        }

        //处理指令
        private async Task HandleClientCommand(byte command, TcpClient client)
        {
            NetworkStream clientStream = client.GetStream();

            //申请文件列表
            if (command == 0)
            {
                using (TcpClient serverConnection = GetServerConnection())
                {
                    NetworkStream _stream_cs = serverConnection.GetStream();
                    //转发命令到Server
                    _stream_cs.WriteByte(command);
                    _stream_cs.Flush();

                    //从server读取响应
                    StreamReader serverReader = new StreamReader(_stream_cs, Encoding.UTF8);
                    string serverResponse = serverReader.ReadToEnd();

                    //根据换行分隔符来获取文件列表
                    string[] fileNames = serverResponse.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);


                    //从响应返回Client
                    StreamWriter clientWriter = new StreamWriter(clientStream, Encoding.UTF8);
                    clientWriter.WriteLine(string.Join(Environment.NewLine, fileNames));
                    clientWriter.Flush();
                    //BeginInvoke(new Action(() => label3.Text = "FileName to Client"));

                    //关闭clientWriter
                    clientWriter.Close();
                }

            }

            //申请文件下载显示，并且cache存储
            else if (command == 1)
            {
                using (TcpClient serverConnection = GetServerConnection())
                {
                    NetworkStream _stream_cs = serverConnection.GetStream();
                    // 从客户端读取文件名长度和文件名
                    byte[] fileNameLengthBytes = new byte[4];
                    clientStream.Read(fileNameLengthBytes, 0, 4);
                    int fileNameLength = BitConverter.ToInt32(fileNameLengthBytes, 0);

                    byte[] fileNameBytes = new byte[fileNameLength];
                    clientStream.Read(fileNameBytes, 0, fileNameLength);
                    string fileName = Encoding.UTF8.GetString(fileNameBytes);

                    // 记录客户端请求下载文件的日志
                    string timestamp = DateTime.Now.ToString("HH:mm:ss yyyy-MM-dd");
                    string logMessage = $"user request:{fileName} {timestamp}";
                    UpdateLog(logMessage);

                    // 在缓存端 UI 中显示需要下载的文件
                    //BeginInvoke(new Action(() => label3.Text = $"Check {fileName}"));

                    //cache区的文件存储路径
                    string FolderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\CacheData");
                    FolderPath = Path.GetFullPath(FolderPath);
                    string filePath = Path.Combine(FolderPath, fileName);
                    Invoke(new Action(() => label1.Text = "Connect to server"));

                    //文件存在不存在都发送请求申请--以便对比文件               
                    _stream_cs.WriteByte(command);
                    _stream_cs.Write(fileNameLengthBytes, 0, 4);
                    _stream_cs.Write(fileNameBytes, 0, fileNameLength);
                    _stream_cs.Flush();



                    ////////////////////////////////读取server传来的文件内容--filefragment////////////////////////////////

                    //把所有server发来的文件片按hash命名存在一个大文件夹下
                    string cacheFoldPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\CacheData");
                    cacheFoldPath = Path.GetFullPath(cacheFoldPath);

                    //接收file发来的指令看是处理什么文件
                    byte fileType = (byte)_stream_cs.ReadByte();

                    //存储拼接好的文件
                    MemoryStream File_Full = new MemoryStream();


                    if (!Directory.Exists(cacheFoldPath))
                    {
                        Directory.CreateDirectory(cacheFoldPath);
                    }

                    while (true)
                    {

                        //先读取Flag
                        byte[] FlagBytes = new byte[4];
                        _stream_cs.Read(FlagBytes, 0, 4);
                        int Flag = BitConverter.ToInt32(FlagBytes, 0);

                        //发hash--已存文件
                        if (Flag == 0)
                        {
                            //已存文件---文本文件处理
                            if (fileType == 0)
                            {
                                //读取hash的长度
                                byte[] hashLengBytes = new byte[4];
                                _stream_cs.Read(hashLengBytes, 0, 4);
                                int hashLength = BitConverter.ToInt32(hashLengBytes, 0);

                                //读hash
                                byte[] hashBytes = new byte[hashLength];
                                _stream_cs.Read(hashBytes, 0, hashLength);
                                string Hash = Encoding.UTF8.GetString(hashBytes);

                                //获取文件夹中的所有文件
                                string[] filePaths = Directory.GetFiles(cacheFoldPath);


                                foreach (string HashNameFile in filePaths)
                                {
                                    string Hash_file = Path.GetFileNameWithoutExtension(HashNameFile);
                                    if (Hash_file == Hash)
                                    {
                                        byte[] fileContentBytes = File.ReadAllBytes(HashNameFile);
                                        File_Full.Write(fileContentBytes, 0, fileContentBytes.Length);
                                    }
                                }
                            }

                            //处理未存文件--图片处理
                            else if (fileType == 1)
                            {
                                //读取hash的长度
                                byte[] hashLengBytes = new byte[4];
                                _stream_cs.Read(hashLengBytes, 0, 4);
                                int hashLength = BitConverter.ToInt32(hashLengBytes, 0);

                                //读hash
                                byte[] hashBytes = new byte[hashLength];
                                _stream_cs.Read(hashBytes, 0, hashLength);
                                string Hash = Encoding.UTF8.GetString(hashBytes);

                                //获取文件夹中的所有文件
                                string[] filePaths = Directory.GetFiles(cacheFoldPath);


                                foreach (string HashNameFile in filePaths)
                                {
                                    string Hash_file = Path.GetFileNameWithoutExtension(HashNameFile);
                                    if (Hash_file == Hash)
                                    {
                                        using (FileStream fileStream = File.OpenRead(HashNameFile))
                                        {
                                            byte[] fileContentBytes = new byte[fileStream.Length];
                                            fileStream.Read(fileContentBytes, 0, fileContentBytes.Length);
                                            File_Full.Write(fileContentBytes, 0, fileContentBytes.Length);
                                        }
                                    }
                                }
                            }

                        }

                        //处理未存文件                                         
                        else if (Flag == 1)
                        {
                            //处理未存文件--文本文件处理
                            if (fileType == 0)
                            {
                                // 读取文件内容长度
                                byte[] fileContentLengthBytes = new byte[4];
                                int bytesRead = _stream_cs.Read(fileContentLengthBytes, 0, 4);
                                if (bytesRead == 0)
                                {
                                    break;
                                }

                                int fileContentLength = BitConverter.ToInt32(fileContentLengthBytes, 0);

                                //读文件内容
                                byte[] fileContentBytes = new byte[fileContentLength];
                                _stream_cs.Read(fileContentBytes, 0, fileContentLength);


                                //计算hash
                                string fileHashName = CalculateMD5Hash(fileContentBytes);

                                //创建文件存储内容--存储在本地
                                string filePath_fragment = Path.Combine(cacheFoldPath, fileHashName + ".dat");


                                //创建文件存储文件片段并用hash值命名
                                using (FileStream fileStream = new FileStream(filePath_fragment, FileMode.Append))
                                {
                                    Invoke(new Action(() => listBox1.Items.Add(fileHashName + ".dat")));
                                    fileStream.Write(fileContentBytes, 0, fileContentBytes.Length);
                                }

                                //拼接整块
                                File_Full.Write(fileContentBytes, 0, fileContentBytes.Length);
                            }

                            //处理未存文件--图片处理
                            else if (Flag == 1)
                            {
                                // 读取文件内容长度
                                byte[] fileContentLengthBytes = new byte[4];
                                int bytesRead = _stream_cs.Read(fileContentLengthBytes, 0, 4);
                                if (bytesRead == 0)
                                {
                                    break;
                                }

                                int fileContentLength = BitConverter.ToInt32(fileContentLengthBytes, 0);

                                //读文件内容
                                byte[] fileContentBytes = new byte[fileContentLength];
                                _stream_cs.Read(fileContentBytes, 0, fileContentLength);


                                //计算hash
                                string fileHashName = CalculateMD5Hash(fileContentBytes);

                                //创建文件存储内容--存储在本地
                                string filePath_fragment = Path.Combine(cacheFoldPath, fileHashName + ".dat");


                                //创建文件存储文件片段并用hash值命名
                                using (FileStream fileStream = new FileStream(filePath_fragment, FileMode.Append))
                                {
                                    Invoke(new Action(() => listBox1.Items.Add(fileHashName + ".dat")));
                                    fileStream.Write(fileContentBytes, 0, fileContentBytes.Length);
                                }

                                //拼接整块
                                File_Full.Write(fileContentBytes, 0, fileContentBytes.Length);
                            }

                        }

                        //文件结束跳出循环，输出复用率
                        else if (Flag == 3)
                        {
                            byte[] reuseRateBytes = new byte[8];//double类型占8个
                            _stream_cs.Read(reuseRateBytes, 0, reuseRateBytes.Length);
                            double reuseRate = BitConverter.ToDouble(reuseRateBytes, 0);

                            // 记录客户端请求下载文件的日志
                            string logMessage_reuse = $"response:{reuseRate.ToString("0.000")}% of {fileName} was constructed by the cached data";
                            UpdateLog(logMessage_reuse);
                            break;

                        }

                    }

                    //将memoryStream的位置设置为0，以确保从文件开始发送数据
                    File_Full.Position = 0;

                    //获取文件内容的字节长度
                    int contentLength = (int)File_Full.Length;

                    // 发送文件内容长度
                    byte[] contentLengthBytes = BitConverter.GetBytes(contentLength);
                    clientStream.Write(contentLengthBytes, 0, 4);
                    clientStream.Flush();

                    // 文件内容发送给客户端
                    File_Full.CopyTo(clientStream);
                    clientStream.Flush();

                }

            }

        }

        private void CacheForm_Load(object sender, EventArgs e)
        {
            // 使用 Task.Run 启动缓存服务器
            Task.Run(() => StartCache());
        }

        //hash值计算
        private string CalculateMD5Hash(byte[] fragmentBytes)
        {
            using (MD5 md5 = MD5.Create())
            {
                //创建实例
                byte[] hashBytes = md5.ComputeHash(fragmentBytes);
                //创建字符串
                StringBuilder stringBuilder = new StringBuilder();
                //遍历hash字节数组
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    //将每个byte转为2为16进制的字符串
                    stringBuilder.Append(hashBytes[i].ToString("X2"));
                }
                return stringBuilder.ToString();
            }
        }

        //清除cache
        private void button1_Click(object sender, EventArgs e)
        {
            Byte command = 2;
            string FolderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\CacheData");
            FolderPath = Path.GetFullPath(FolderPath);

            //本地清除
            try
            {
                //获取当前文件夹下的所有文件
                string[] files = Directory.GetFiles(FolderPath);

                if (files.Length != 0)
                {
                    //遍历删除所有文件
                    foreach (string file in files)
                    {
                        File.Delete(file);
                        string fileName = Path.GetFileName(file);
                        BeginInvoke(new Action(() => listBox1.Items.Remove(fileName)));
                    }

                    BeginInvoke(new Action(() => label4.Text = "cache cleard successfully"));

                }
                else
                {
                    BeginInvoke(new Action(() => label4.Text = "cache already clear"));
                }



            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => label4.Text = "cache cleard unsuccessfully"));
            }

            //告诉server，让server清除
            try
            {
                using (TcpClient serverConnection = GetServerConnection())
                {
                    NetworkStream stream_clean = serverConnection.GetStream();
                    stream_clean.WriteByte(command);
                    stream_clean.Flush();
                }

            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => label4.Text = "Failed to send clear cache command to server."));
            }


        }

        //显示我选中的文件名
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1) //确保有一个选定的项
            {
                _selectFileName = listBox1.SelectedItem.ToString();
            }
        }

        //更新日志
        private void UpdateLog(string message)
        {
            BeginInvoke(new Action(() => textBox1.AppendText(message + Environment.NewLine)));
        }

        private void button2_Click(object sender, EventArgs e)
        {

            // 获取文件完整路径
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\CacheData");
            folderPath = Path.GetFullPath(folderPath);
            string filePath = Path.Combine(folderPath, _selectFileName);

            // 读取文件内容
            byte[] fileContent = File.ReadAllBytes(filePath);

            // 将文件内容转换为16进制字符串
            StringBuilder hexString = new StringBuilder(fileContent.Length * 2);
            foreach (byte b in fileContent)
            {
                hexString.AppendFormat("{0:X2}", b);
            }

            // 在文本框中显示16进制字符串
            textBox2.Text = hexString.ToString();
        }
       
    }
}