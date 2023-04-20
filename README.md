# 711_option2
文件切片

1.文件切片（Server 端）: 在 Server 端，将 Data 中的文件（如 file1、file2）进行切片，并将这些切片存储到另一个文件夹（如 Data_Fragment）中。这些文件可以命名为 file1_1, file1_2, file1_3, file2_1, file2_2 等，以表示它们分别属于哪个原始文件以及它们在原始文件中的位置。

2.获取文件切片（Cache 端）: 当 Cache 请求 file1 时，Server 将返回 file1 的所有切片（例如，file1_1, file1_2, file1_3 等）。Cache 将根据需要存储这些切片。

3.计算复用率（Cache 端）: 当 Cache 请求 file2 时，Server 将返回 file2 的所有切片（例如，file2_1, file2_2 等）。Cache 需要计算已存储的 file1 切片与收到的 file2 切片之间的复用率。您可以通过比较哈希值来实现此功能。

4.替换不同的切片（Cache 端）: 使用 Rabin 指纹算法，找到 file1 切片与 file2 切片之间的不同部分，并替换它们。您可以在 Cache 中维护一个固定大小的缓冲区来存储切片，并使用滑动窗口算法进行替换。

5.拼接文件切片（Cache 端）: 将 file2 的切片拼接起来，形成完整的 file2 文件。

6.将文件传输给客户端（Cache 端）: 将拼接好的 file2 文件发送给客户端。