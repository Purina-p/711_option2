using System.IO;
using System.Net;
using System.Net.Sockets;
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

                    //读取server传来的文件内容
                    StreamReader stream_cs_Reader = new StreamReader(_stream_cs, Encoding.UTF8);
                    string fileContent = stream_cs_Reader.ReadToEnd();
                    stream_cs_Reader?.Close();

                    //判断我的listbox中是不是已经有了
                    //实时更新listbox
                    bool fileInListBox = listBox1.Items.Contains(fileName);

                    if (!fileInListBox)
                    {
                        BeginInvoke(new Action(() => listBox1.Items.Add(fileName)));
                    }
                    
                    //判断文件路径是否存在,并且把log写上
                    string cacheFileContent;
                    string logMessage_response;
                    if (File.Exists(filePath))
                    {
                        //如果存在，直接把cache的文件价中的内容传给client
                        cacheFileContent = File.ReadAllText(filePath);
                        logMessage_response = $"user response: cached {fileName}";
                        UpdateLog(logMessage_response);
                    }
                    else
                    {
                        //如果不存在，就创建新文件并写入server的内容
                        using (File.Create(filePath)) { }                      
                        File.WriteAllText(filePath, fileContent);
                        cacheFileContent = File.ReadAllText(filePath);                       
                        logMessage_response = $"user response: {fileName} downloaded from server";
                        UpdateLog(logMessage_response);
                    }

                    
                    // 获取文件内容的字节长度
                    byte[] contentBytes = Encoding.UTF8.GetBytes(cacheFileContent);
                    int contentLength = contentBytes.Length;

                    // 发送文件内容长度
                    byte[] contentLengthBytes = BitConverter.GetBytes(contentLength);
                    clientStream.Write(contentLengthBytes, 0, 4);
                    clientStream.Flush();

                    // 文件内容发送给客户端
                    using (BinaryWriter clientWriter = new BinaryWriter(clientStream, Encoding.UTF8, leaveOpen: true))
                    {
                        clientWriter.Write(contentBytes, 0, contentLength);
                        clientWriter.Flush();
                    }

                }

            }

        }

        private void CacheForm_Load(object sender, EventArgs e)
        {
            // 使用 Task.Run 启动缓存服务器
            Task.Run(() => StartCache());
        }

        //清除cache
        private void button1_Click(object sender, EventArgs e)
        {
            string FolderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\CacheData");
            FolderPath = Path.GetFullPath(FolderPath);

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

        }

        //更新日志
        private void UpdateLog(string message)
        {
            BeginInvoke(new Action(() => textBox1.AppendText(message + Environment.NewLine)));
        }


    }
}