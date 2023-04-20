using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    public partial class ServerForm : Form
    {
        private string _selectFileName;

        public ServerForm()
        {
            InitializeComponent();
            label2.Text = "Waiting for connection...";
            //获取server_data路径
            string FolderPath_A = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\AdminData");
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

            //获取server_data路径
            string FolderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data");
            FolderPath = Path.GetFullPath(FolderPath);

            //获取文件夹中的文件名
            string[] fileNames = Directory.GetFiles(FolderPath);

            for (int i = 0; i < fileNames.Length; i++)
            {
                fileNames[i] = Path.GetFileName(fileNames[i]);
            }

            //把文件名添加到listBox中
            foreach (string fileName in fileNames)
            {
                listBox2.Items.Add(fileName);
            }

            //将文件列表连接成一个字符串
            string fileList = string.Join(Environment.NewLine, fileNames);

        }

        private void StartServer()
        {
            IPAddress ipAddr = IPAddress.Loopback;
            int port_server = 8081;

            TcpListener cachelistener = new TcpListener(ipAddr, port_server);
            cachelistener.Start();

            //使用委托和invoke来让lable进行更新
            Invoke(new Action(() => label1.Text = "Server starting"));

            while (true)
            {
                var cache = cachelistener.AcceptTcpClient();
                Invoke(new Action(() => label2.Text = "Cache connected"));

                NetworkStream stream = cache.GetStream();
                byte command = (byte)stream.ReadByte();
                HandleCacheCommand(command, stream);

                cache.Close();
            }
        }

        //处理指令
        private void HandleCacheCommand(byte command, NetworkStream stream)
        {
            //返回文件列表
            if (command == 0)
            { 
                
                //到当前文件夹中的
                string FolderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data");
                FolderPath = Path.GetFullPath(FolderPath);                
                
                //获取文件夹中的文件名
                string[] fileNames = Directory.GetFiles(FolderPath);

                for (int i = 0; i < fileNames.Length; i++)
                {
                    fileNames[i] = Path.GetFileName(fileNames[i]);
                }

                //将文件列表连接成一个字符串
                string fileList = string.Join(Environment.NewLine, fileNames);
                //Invoke(new Action(() => label3.Text = "Send fileNames to cache"));

                StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
                writer.WriteLine(fileList);
                writer.Flush();                                               
                writer.Close();
                stream.Close();

            }

            //返回文件数据
            else if(command == 1) 
            {
                // 从cache端读取文件名长度和文件名
                byte[] fileNameLengthBytes = new byte[4];
                stream.Read(fileNameLengthBytes, 0, 4);
                int fileNameLength = BitConverter.ToInt32(fileNameLengthBytes, 0);

                byte[] fileNameBytes = new byte[fileNameLength];
                stream.Read(fileNameBytes, 0, fileNameLength);
                string fileName = Encoding.UTF8.GetString(fileNameBytes);
              
                //查看是否在我的文件里面
                string FolderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data");
                FolderPath = Path.GetFullPath(FolderPath);
                string filePath = Path.Combine(FolderPath, fileName);
              
                //读取文件内容发给cache
                string fileContent = File.ReadAllText(filePath,Encoding.UTF8);
                StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
                //Invoke(new Action(() => label4.Text = $"Send Content of {fileName} "));
                writer.WriteLine(fileContent);
                writer.Flush();
                writer.Close();
                stream.Close();
               

            }
        }
     
        private void ServerForm_Load(object sender, EventArgs e)
        {
            //时间处理程序中启动服务器，以便加载主窗体运行在后台线程
            new System.Threading.Thread(StartServer) { IsBackground = true }.Start();
        }

        //显示我选中的文件名
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1) //确保有一个选定的项
            {
                _selectFileName = listBox1.SelectedItem.ToString();
                label5.Text = _selectFileName;

            }
        }


        //点击把文件copy一份进data
        private void button1_Click(object sender, EventArgs e)
        {
            if(_selectFileName!= null)
            {
                //从admin读取文件路径
                string sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\AdminData");
                sourcePath = Path.GetFullPath(sourcePath);
                string filePath_A = Path.Combine(sourcePath, _selectFileName);

                //目标路径
                string target = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data");
                target = Path.GetFullPath(target);
                string filePath = Path.Combine(target, _selectFileName);

                if (!File.Exists(filePath))
                {
                    File.Copy(filePath_A, filePath, true);
                    listBox2.Items.Add(_selectFileName);

                }
                else
                {
                    Invoke(new Action(() => label5.Text="already have"));
                }                

            }




        }        

        //文件切片--基于Rabin函数

    }

}