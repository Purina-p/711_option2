# 711_option2
文件切片

1.将文件进行切片，并计算每个文件碎片的哈希值
用rabin算法，当授权文件时，我把选中的文件进行授权和切割到datafragment，保存到相应的文件夹中

获取文件切片
  2.1 获取服务器段文件的所有片段哈希值--server修改
  --command申请文件时，直接一起申请哈希值列表，让他把文件和hash都返回来
  --在切割文件时，同时进行切割和hash列表的计算（用的MD5--唯一性）
  
2.当缓存请求file1时，服务器将file1的碎片发送给缓存，并将发送的碎片存储在一个特定文件夹中。这样，服务器可以了解缓存中有哪些文件碎片。--ok
2.1 cache同步清理--ok

3.当缓存请求file2时，服务器会将file2的每个碎片与file1的碎片进行哈希值对比。

4.如果相同索引的文件碎片的哈希值相同，服务器将返回哈希值。
如果哈希值不同，服务器将发送对应哈希值的文件碎片给缓存。


5.(cache)
在缓存端，将接收到的文件碎片的文件名更改为对应的哈希值。
缓存端根据接收到的哈希值或文件碎片进行对比，然后将碎片重新组合成完整的文件。


