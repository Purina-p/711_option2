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

        public CacheForm()
        {
            InitializeComponent();
            label2.Text = "Waiting Client Connection...";
            _serverAddress = IPAddress.Loopback;
            _serverPort = 8081;
            //获取server_data路径
            string FolderPath_A = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\CacheData");
            FolderPath_A = Path.GetFullPath(FolderPath_A);

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
                using(TcpClient serverConnection = GetServerConnection())
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



                    ////////////////////////////////读取server传来的文件内容--filefragment--------

                    //把所有server发来的文件片按hash命名存在一个大文件夹下
                    string cacheFoldPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\CacheData");
                    cacheFoldPath = Path.GetFullPath(cacheFoldPath);

                    //存储拼接好的文件
                    MemoryStream File_Full = new MemoryStream();


                    if (!Directory.Exists(cacheFoldPath))
                    {
                        Directory.CreateDirectory(cacheFoldPath);
                    }


                    while (true)
                    {

                        //先读取Flag
                        byte[] FlagBytes= new byte[4];
                        _stream_cs.Read(FlagBytes, 0, 4);
                        int Flag = BitConverter.ToInt32(FlagBytes, 0);
                        Invoke(new Action(() => label1.Text = "Connect to 0"));
                        if (Flag == 0 )//发hash
                        {
                            //读取hash的长度
                            byte[] hashLengBytes = new byte[4];
                            _stream_cs.Read(hashLengBytes, 0, 4);
                            int hashLength = BitConverter.ToInt32(hashLengBytes,0);
                            Invoke(new Action(() => label1.Text = "Connect to 1"));
                            //读hash
                            byte[] hashBytes = new byte[hashLength];
                            _stream_cs.Read(hashBytes, 0, hashLength);
                            string Hash = Encoding.UTF8.GetString(hashBytes);
                            Invoke(new Action(() => label1.Text = "Connect to 2"));
                            //获取文件夹中的所有文件
                            string[] filePaths = Directory.GetFiles(cacheFoldPath);
                            Invoke(new Action(() => label1.Text = "Connect to 3"));
                            foreach (string HashNameFile in filePaths)
                            {
                                Invoke(new Action(() => label1.Text = "Connect to 4"));

                                string Hash_file = Path.GetFileNameWithoutExtension(HashNameFile);
                                if(Hash_file== Hash)
                                {
                                    byte[] fileContentBytes = File.ReadAllBytes(HashNameFile);
                                    File_Full.Write(fileContentBytes, 0, fileContentBytes.Length);
                                    Invoke(new Action(() => label1.Text = "Connect to 5"));
                                    break;

                                }                               
                            }
                            Invoke(new Action(() => label1.Text = "Connect to 6"));

                        }
                        else
                        {
                            Invoke(new Action(() => label1.Text = "Connect to 7"));

                            // 读取文件内容长度
                            byte[] fileContentLengthBytes = new byte[4];
                            int bytesRead = _stream_cs.Read(fileContentLengthBytes, 0, 4);
                            if (bytesRead == 0)
                            {
                                break;
                            }
                            int fileContentLength = BitConverter.ToInt32(fileContentLengthBytes, 0);
                            Invoke(new Action(() => label1.Text = "Connect to 8"));
                            //读文件内容
                            byte[] fileContentBytes = new byte[fileContentLength];
                            _stream_cs.Read(fileContentBytes, 0, fileContentLength);
                            Invoke(new Action(() => label1.Text = "Connect to 9"));
                            //计算hash
                            string fileHashName = CalculateMD5Hash(fileContentBytes);

                            //创建文件存储内容--存储在本地
                            string filePath_fragment = Path.Combine(cacheFoldPath, fileHashName + ".dat");
                            Invoke(new Action(() => label1.Text = "Connect to 10"));
                            //创建文件存储文件片段并用hash值命名
                            using (FileStream fileStream = new FileStream(filePath_fragment, FileMode.Append))
                            {
                                Invoke(new Action(() => listBox1.Items.Add(fileHashName + ".dat")));
                                fileStream.Write(fileContentBytes, 0, fileContentBytes.Length);
                            }
                            Invoke(new Action(() => label1.Text = "Connect to 11"));
                            //拼接整块
                            File_Full.Write(fileContentBytes, 0, fileContentBytes.Length);
                            Invoke(new Action(() => label1.Text = "Connect to 12"));
                        }

                    }

                    // 获取文件内容的字节长度
                    //byte[] contentBytes = Encoding.UTF8.GetBytes(cacheFileContent);
                    //int contentLength = contentBytes.Length;

                    // 发送文件内容长度
                    //byte[] contentLengthBytes = BitConverter.GetBytes(contentLength);
                    //clientStream.Write(contentLengthBytes, 0, 4);
                    //clientStream.Flush();

                    // 文件内容发送给客户端
                    //using (BinaryWriter clientWriter = new BinaryWriter(clientStream, Encoding.UTF8, leaveOpen: true))
                    {
                        //clientWriter.Write(contentBytes, 0, contentLength);
                        //clientWriter.Flush();
                    }

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

                if(files.Length != 0) 
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
                    BeginInvoke(new Action(() => label4.Text = $"Cache has already clear, nothing in CacheData"));
                }



            }
            catch(Exception ex)
            {
                BeginInvoke(new Action(() => label4.Text = "cache cleard unsuccessfully"));
            }

            //告诉server，让server清除
            try
            {
                using(TcpClient serverConnection = GetServerConnection())
                {
                    NetworkStream stream_clean = serverConnection.GetStream();
                    stream_clean.WriteByte(command);
                    stream_clean.Flush();
                }

            }
            catch(Exception ex)
            {
                BeginInvoke(new Action(() => label4.Text = "Failed to send clear cache command to server."));
            }


        }

        //更新日志
        private void UpdateLog(string message)
        {
            BeginInvoke(new Action(() => textBox1.AppendText(message + Environment.NewLine)));
        }


    }
}