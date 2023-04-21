﻿using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
            //获取Admindata路径
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

            //获取data->cache option1路径
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
              
                //查看是否在我的文件里面-改文件切片--直接把文件来源变成我的datafragment
                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data_Fragment", fileName);
                folderPath = Path.GetFullPath(folderPath);

                //在cache端申请的时候，直接把传过去的文件打进大文件夹下
                string folderPath_Cache = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data_Fragment_Cache");
                folderPath_Cache = Path.GetFullPath(folderPath_Cache);

                //判断我村没存过这个文件夹，如果没有，建一个存碎片
                if (!Directory.Exists(folderPath_Cache))
                {
                    Directory.CreateDirectory(folderPath_Cache);
                }
                
                //获取文件夹中的所有文件
                string[] filePaths = Directory.GetFiles(folderPath);

                //计算复用率的参数初始化
                int unmatchFragment = 0;
                int matchFragment = 0;

                //文件切片后,单个文件变成文件夹因此进行循环遍历读取文件内容
                foreach (string filePath in filePaths)
                {
                    //读取文件内容
                    //string fileContent = File.ReadAllText(filePath,Encoding.UTF8);--文件切片后不能读text->bytes
                    byte[] fileContent = File.ReadAllBytes(filePath);

                    //计算hash
                    string fragmentHash = CalculateMD5Hash(fileContent);

                    //检查cache端是否有相同的
                    string[] serverCacheFilePaths = Directory.GetFiles(folderPath_Cache);
                    string matchingServerCacheFilePath = null;

                    foreach (string serverCachePath in serverCacheFilePaths)
                    {
                        byte[] existContent= File.ReadAllBytes(serverCachePath);
                        string existContentHash = CalculateMD5Hash(existContent);

                        if (existContentHash == fragmentHash)
                        {
                            matchingServerCacheFilePath = serverCachePath;
                        }

                    }


                    if (matchingServerCacheFilePath == null)
                    {
                        //给他打个标签--发内容的--1
                        byte[] Flag = BitConverter.GetBytes(1);
                        stream.Write(Flag, 0, 4);

                        //发送文件内容长度
                        byte[] fileContentLengthBytes = BitConverter.GetBytes(fileContent.Length);
                        stream.Write(fileContentLengthBytes, 0, 4);

                        //发送内容
                        stream.Write(fileContent, 0, fileContent.Length);

                        //将发送的内容保存到server端的cache备份中
                        string serverCacheFilePath = Path.Combine(folderPath_Cache, Path.GetFileName(filePath));
                        File.WriteAllBytes(serverCacheFilePath, fileContent);

                        //复用率
                        unmatchFragment++;


                    }
                    else
                    {
                        //给他打个标签--发送hash--0
                        byte[] Flag = BitConverter.GetBytes(0);
                        stream.Write(Flag, 0, 4);

                        //发送hash
                        byte[] fragmentHashBytes = Encoding.UTF8.GetBytes(fragmentHash);
                        byte[] fragmentHashLengthBytes = BitConverter.GetBytes(fragmentHashBytes.Length);
                        stream.Write(fragmentHashLengthBytes, 0, 4);
                        stream.Write(fragmentHashBytes, 0, fragmentHashBytes.Length);

                        //复用率
                        matchFragment++;

                    }
                }
                
                //发完所有的文件块给出Flag=3，传输完成，并且把复用率传过去
                byte[] endFlag = BitConverter.GetBytes(3);
                stream.Write(endFlag, 0, 4);

                //计算复用率
                double reuseRate = (double) matchFragment / (matchFragment + unmatchFragment) * 100;
                byte[] reuseRateBytes = BitConverter.GetBytes(reuseRate);
                stream.Write(reuseRateBytes, 0, reuseRateBytes.Length);

                stream.Flush();
                stream.Close();
                BeginInvoke(new Action(() => label6.Text = reuseRate.ToString("0.00") + "%"));
            }

            //同步清理缓存区
            else if(command == 2){
                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data_Fragment_Cache");
                folderPath = Path.GetFullPath(folderPath);
                try
                {
                    //获取当前文件夹下的所有文件
                    string[] files = Directory.GetFiles(folderPath);

                    if (files.Length != 0)
                    {
                        //遍历删除所有文件
                        foreach (string file in files)
                        {
                            File.Delete(file);
                            string fileName = Path.GetFileName(file);
                        }
                    }
                    
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() => label4.Text = "cache cleard unsuccessfully"));
                }
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

                //切片文件目标路径
                string target_fragment = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data_Fragment");
                target_fragment = Path.GetFullPath(target_fragment);
                string filePath_fragment = Path.Combine(target_fragment, _selectFileName);


                if (!File.Exists(filePath))
                {
                    File.Copy(filePath_A, filePath, true);
                    RabinKarpFileSplitter(filePath_A, filePath_fragment);
                    listBox2.Items.Add(_selectFileName);
                    
                }
                else
                {
                    Invoke(new Action(() => label5.Text="already have"));
                }                

            }

        }

        //文件切片--Rabin函数
        private void RabinKarpFileSplitter(string inputFilePath, string OutputDirectory)
        {
            
            //Rabin-Krap的算法参数
            const int WindowSize = 11; // 滑动窗口大小-->导致切片大小
            const ulong Q = 10007; // 大质数，用于取模运算-->降低hash冲突，
            const ulong D = 256; // 基数，用于计算哈希值-->无所dawei
            const int BlockSize = 2 * 1024; // 2KB文件块大小

            //存hash列表对比的文件夹--记得删
            string HashPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\Rabin_Test");
            HashPath = Path.GetFullPath(HashPath);

            // 创建一个列表来存储文件片段的哈希值
            List<string> fragmentHashes = new List<string>();

            // 如果输出目录不存在，则创建
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            // 将输入文件的字节读取到字节数组中
            byte[] inputFileBytes = File.ReadAllBytes(inputFilePath);

            // 初始化变量
            ulong hash = 0;
            ulong Dm = 1;

            // 预计算值，计算每个窗口的hash然后对他进行快速更新
            //for (int i = 0; i < WindowSize - 1; i++)
            {
                //Dm = (Dm * D) % Q;
            }

            // 计算滑动窗口的初始哈希值
            for (int i = 0; i < WindowSize; i++)
            {
                hash = (hash * D + inputFileBytes[i]) % Q;
            }

            int fileCounter = 0;
            int startIndex = 0;

            // 从滑动窗口的结束位置开始遍历输入文件的字节
            for (int i = WindowSize; i < inputFileBytes.Length; i++)
            {
                // 从输入文件字节数组中获取哈希值计算所需的上一个字节
                byte lastByte = inputFileBytes[i - WindowSize];

                // 计算当前滑动窗口的哈希值
                hash = ((hash - lastByte) * D + inputFileBytes[i]) % Q;

                // 判断当前哈希值是否满足分割条件,或者已经到达文件末尾
                if (hash == 0 || i == inputFileBytes.Length - 1)
                {
                    // 保存文件切片
                    string outputPath = Path.Combine(OutputDirectory, $"{Path.GetFileNameWithoutExtension(inputFilePath)}_{fileCounter}.dat");
                    using (FileStream fileSplit = new FileStream(outputPath, FileMode.Create))
                    {
                        fileSplit.Write(inputFileBytes, startIndex, i - startIndex + 1);
                    }

                    // 计算文件片段的 MD5 哈希值
                    byte[] fragmentBytes = new byte[i - startIndex + 1];
                    Array.Copy(inputFileBytes, startIndex, fragmentBytes, 0, i - startIndex + 1);
                    string fragmentHash = CalculateMD5Hash(fragmentBytes);
                    fragmentHashes.Add(fragmentHash);

                    // 更新文件计数器和下一个文件切片的起始位置
                    fileCounter++;
                    startIndex = i + 1;
                }
            }

            // 为哈希值列表文件创建路径
            string hashListFilePath = Path.Combine(HashPath, $"{Path.GetFileNameWithoutExtension(inputFilePath)}hashes.txt");

            // 保存哈希值列表到文件中
            using (StreamWriter sw = new StreamWriter(hashListFilePath, false))
            {
                foreach (string hashValue in fragmentHashes)
                {
                    sw.WriteLine(hashValue);
                }
            }

        }        

        //rabin函数算的hash值不一定能保证唯一性，所以在循环里有调用了一次MD5的函数来计算hash，来返回列表
        private string CalculateMD5Hash(byte[] fragmentBytes)
        {
            using(MD5 md5 = MD5.Create())
            {
                //创建实例
                byte[] hashBytes = md5.ComputeHash(fragmentBytes);
                //创建字符串
                StringBuilder stringBuilder= new StringBuilder();
                //遍历hash字节数组
                for (int i=0;i<hashBytes.Length;i++)
                {
                    //将每个byte转为2为16进制的字符串
                    stringBuilder.Append(hashBytes[i].ToString("X2"));
                }
                return stringBuilder.ToString();
            }
        }
      
    }

}