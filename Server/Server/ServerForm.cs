using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
            else if (command == 1)
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

                //以防万一，clone空文件没有
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                //建一个存servercache的碎片
                string folderPath_Cache = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\data_Fragment_Cache");
                folderPath_Cache = Path.GetFullPath(folderPath_Cache);

                //建一个存servercache的碎片
                if (!Directory.Exists(folderPath_Cache))
                {
                    Directory.CreateDirectory(folderPath_Cache);
                }

                //获取文件夹中的所有文件,我对应切片文件夹的所有文件
                string[] filePaths = Directory.GetFiles(folderPath);

                //获取初始servercache的文件--用于算计算率，防止后面循环加入文件片导致复用率增加
                string[] file_fragments = Directory.GetFiles(folderPath_Cache);

                //计算复用率的参数初始化
                int unmatchFragment = 0;
                int matchFragment = 0;

                List<string> currentFileFragment = new List<string>();//存储初始文件夹下hash列表


                //计算当前文件夹下的hash列表
                foreach (string file_fragment in file_fragments)
                {
                    byte[] fileContent = File.ReadAllBytes(file_fragment);
                    string fragmentHash = CalculateMD5Hash(fileContent);
                    currentFileFragment.Add(fragmentHash);

                }

                //对比hash计算复用率
                foreach (string filePath in filePaths)
                {
                    byte[] fileContent = File.ReadAllBytes(filePath);
                    string fragmentHash = CalculateMD5Hash(fileContent);

                    if (currentFileFragment.Contains(fragmentHash))
                    {
                        matchFragment++;
                    }
                    else
                    {
                        unmatchFragment++;
                    }
                }

                double reuseRate = (double)matchFragment / (unmatchFragment + matchFragment) * 100;


                //初始化一下我的切片列表，让他按切块顺序循环，方便后面拼接
                List<string> filePathList = Directory.GetFiles(folderPath_Cache).ToList();

                //根据文件的扩展名判断一下类型，在cache那边分别处理文本和图片的拼接
                string fileExtension = Path.GetExtension(folderPath);
                byte fileTypeFlag = (fileExtension == ".txt") ? (byte)0 : (byte)1;
                stream.WriteByte(fileTypeFlag);


                // 提取文件名中的数字并用它对文件路径排序
                Array.Sort(filePaths, (path1, path2) =>
                {
                    int number1 = NumberFileName(Path.GetFileNameWithoutExtension(path1));
                    int number2 = NumberFileName(Path.GetFileNameWithoutExtension(path2));
                    return number1.CompareTo(number2);
                });


                //循环遍历内容读取，存入servercache
                foreach (string filePath in filePaths)
                {
                    //读取文件内容
                    byte[] fileContent = File.ReadAllBytes(filePath);

                    //计算hash
                    string fragmentHash = CalculateMD5Hash(fileContent);
                    //Invoke(new Action(() => listBox3.Items.Add($"File: {Path.GetFileName(filePath)}, Hash: {fragmentHash}")));
                    //Invoke(new Action(() => listBox3.Items.Add($"File: {Path.GetFileName(filePath)}, Size: {fileContent.Length}, Content: {BitConverter.ToString(fileContent)}, Hash: {fragmentHash}")));

                    //检查cache端是否有相同的
                    string[] serverCacheFilePaths = Directory.GetFiles(folderPath_Cache);
                    string matchingServerCacheFilePath = null;

                    foreach (string serverCachePath in serverCacheFilePaths)
                    {
                        byte[] existContent = File.ReadAllBytes(serverCachePath);
                        string existContentHash = CalculateMD5Hash(existContent);

                        if (existContentHash == fragmentHash)
                        {
                            matchingServerCacheFilePath = serverCachePath;
                            //Invoke(new Action(() => listBox3.Items.Add($"Matching Hash: {fragmentHash}")));
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
                        string uniqueFileName = Path.GetFileNameWithoutExtension(filePath) + "_" + fragmentHash + Path.GetExtension(filePath);
                        string serverCacheFilePath = Path.Combine(folderPath_Cache, uniqueFileName);
                        File.WriteAllBytes(serverCacheFilePath, fileContent);



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

                    }
                }

                //发完所有的文件块给出Flag=3，传输完成，并且把复用率传过去
                byte[] endFlag = BitConverter.GetBytes(3);
                stream.Write(endFlag, 0, 4);

                //防止出现0作为分母的情况
                if (matchFragment == 0)
                {
                    double reuseRate_0 = 0.000;
                    byte[] reuseRateBytes = BitConverter.GetBytes(reuseRate_0);
                    stream.Write(reuseRateBytes, 0, reuseRateBytes.Length);
                    //Invoke(new Action(() => label7.Text = $"{reuseRate.ToString("0.000")}%"));

                }

                else
                {
                    //传输复用率
                    byte[] reuseRateBytes = BitConverter.GetBytes(reuseRate);
                    stream.Write(reuseRateBytes, 0, reuseRateBytes.Length);

                    //Invoke(new Action(() => label7.Text = $"{reuseRate.ToString("0.000")}%"));
                    stream.Flush();
                    stream.Close();
                }

            }

            //同步清理缓存区
            else if (command == 2)
            {
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

        /// //////////////////////////////////////////点击按钮授权切片///////////////////////////////////////

        //点击把文件copy一份进data
        private void button1_Click(object sender, EventArgs e)
        {
            if (_selectFileName != null)
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

                string FileType = GetFileType(filePath_A);

                if (FileType == "text")
                {
                    //切片---不同文件进行不同的切片处理--图片和文档
                    RabinKarpFileSplitter_txt(filePath_A, filePath_fragment, 3, 2048, 8567);
                    File.Copy(filePath_A, filePath, true);
                    //BeginInvoke(new Action(() => label7.Text = "text"));

                }
                else if (FileType == "image")
                {
                    //切片---不同文件进行不同的切片处理--图片和文档
                    RabinKarpFileSplitter_txt(filePath_A, filePath_fragment, 96, 1500, 256);
                    File.Copy(filePath_A, filePath, true);
                    //BeginInvoke(new Action(() => label7.Text = "image"));
                }
                


                //实时更新文件名
                if (!listBox2.Items.Contains(_selectFileName))
                {

                    listBox2.Items.Add(_selectFileName);

                }
                else
                {
                    Invoke(new Action(() => label5.Text = "File Replaced"));
                }

            }

        }

        //文件切片--Rabin函数
        private void RabinKarpFileSplitter_txt(string inputFilePath, string OutputDirectory, int WindowSize, int Q, int D)
        {

            //存hash列表对比的文件夹--记得删
            //string HashPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\Rabin_Test");
            //HashPath = Path.GetFullPath(HashPath);

            //命名用
            string inputFileName = Path.GetFileNameWithoutExtension(inputFilePath);

            // 创建一个列表来存储文件片段的哈希值
            List<string> fragmentHashes = new List<string>();

            // 如果输出目录不存在，则创建
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            // 将输入文件的字节读取到字节数组中
            byte[] inputFileBytes = File.ReadAllBytes(inputFilePath);

            //fingerprintes--对应他在输入文件时的位置
            List<int> fingerprints = GetFingerprints(inputFileBytes, WindowSize, Q, D);

            //块计数
            int fileCounter = 0;

            //初始化切片位置
            int previousFingerprint = 0;

            //遍历fingerprint的列表
            foreach (int fingerprint in fingerprints)
            {
                //计算当前快的大小
                int chunkSize = fingerprint - previousFingerprint + 1;

                //创建一个新字节数存储当前块
                byte[] chunk = new byte[chunkSize];

                // 从输入文件内容中复制当前块的内容
                Array.Copy(inputFileBytes, previousFingerprint, chunk, 0, chunkSize);

                // 计算文件片段的 MD5 哈希值
                string fragmentHash = CalculateMD5Hash(chunk);
                fragmentHashes.Add(fragmentHash);

                // 为当前块生成文件名并保存到输出目录中
                string chunkFileName = $"{inputFileName}_chunk_{fileCounter}.dat";
                string chunkFilePath = Path.Combine(OutputDirectory, chunkFileName);
                File.WriteAllBytes(chunkFilePath, chunk);

                // 更新块计数器
                fileCounter++;
                previousFingerprint = fingerprint + 1;
            }

            // 如果文件结尾没有分割点，则添加最后一个块
            if (previousFingerprint < inputFileBytes.Length)
            {
                // 计算最后一个块的大小
                int chunkSize = inputFileBytes.Length - previousFingerprint;

                // 创建一个新的字节数组来存储最后一个块的内容
                byte[] chunk = new byte[chunkSize];

                // 从输入文件内容中复制最后一个块的内容
                Array.Copy(inputFileBytes, previousFingerprint, chunk, 0, chunkSize);

                // 为最后一个块生成文件名并保存到输出目录中
                string chunkFileName = $"{inputFileName}_chunk_{fileCounter}.dat";
                string chunkFilePath = Path.Combine(OutputDirectory, chunkFileName);
                File.WriteAllBytes(chunkFilePath, chunk);
            }

            // 为哈希值列表文件创建路径
            //string hashListFilePath = Path.Combine(HashPath, $"{Path.GetFileNameWithoutExtension(inputFilePath)}hashes.txt");

            // 保存哈希值列表到文件中
            //using (StreamWriter sw = new StreamWriter(hashListFilePath, false))
            //foreach (string hashValue in fragmentHashes)
            //sw.WriteLine(hashValue);

        }

        //计算切片位置
        private static List<int> GetFingerprints(byte[] data, int WindowSize, int Q, int D)
        {

            List<int> fingerprints = new List<int>();
            int hash = 0;

            for (int i = 0; i < data.Length - WindowSize + 1; i++)
            {
                byte[] window = new byte[WindowSize];
                Array.Copy(data, i, window, 0, WindowSize);
                hash = CalculateRabinHash(window, Q, D);

                if (hash % Q == 0)
                {
                    fingerprints.Add(i + WindowSize - 1);
                }
            }

            return fingerprints;
        }

        //计算窗口hash
        private static int CalculateRabinHash(byte[] window, int Q, int D)
        {
            int hash = 0;

            for (int i = 0; i < window.Length; i++)
            {
                hash = (hash * D + window[i]) % Q;
            }

            return hash;
        }


        //rabin函数算的hash值不一定能保证唯一性，所以在循环里有调用了一次MD5的函数来计算hash，来返回列表
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

        //分辨一下文件类型
        private string GetFileType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();

            //图片文件扩展名
            List<string> imageExtension = new List<string> { ".jpg", ".png", ".bmp" };

            //文本文件扩展名
            List<string> textExtension = new List<string> { ".txt" };

            if (imageExtension.Contains(extension))
            {
                return "image";
            }
            else if (textExtension.Contains(extension))
            {
                return "text";
            }
            else
            {
                return "unknow";
            }

        }

        //提取文件名数字用于后续拼接
        private int NumberFileName(string fileName)
        {
            string numberStr = "";
            foreach (char c in fileName)
            {
                if (char.IsDigit(c))
                {
                    numberStr += c;
                }
            }
            return int.Parse(numberStr);
        }
    }

}