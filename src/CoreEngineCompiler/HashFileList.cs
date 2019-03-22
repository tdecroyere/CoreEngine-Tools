using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CoreEngine.Compiler
{
    public class HashFileList
    {
        private Dictionary<string, byte[]> hashList;

        public HashFileList()
        {
            this.hashList = new Dictionary<string, byte[]>();
        }

        public bool HasFileChanged(string path, ReadOnlySpan<byte> inputData)
        {
            // TODO: Use the file date first to quickly reject the file?
            // TODO: Store the last modified date in the Dictionary

            var hash = ComputeFileHash(inputData);

            if (this.hashList.ContainsKey(path) && hash.SequenceEqual(new ReadOnlySpan<byte>(this.hashList[path])))
            {
                return false;
            }

            if (!this.hashList.ContainsKey(path))
            {
                // TODO: Avoid array copy
                this.hashList.Add(path, hash.ToArray());
            }

            else
            {
                // TODO: Avoid array copy
                this.hashList[path] = hash.ToArray();
            }

            return true;
        }

        public void ReadFile(string path)
        {
            if (File.Exists(path))
            {
                this.hashList.Clear();

                using var stream = new FileStream(path, FileMode.Open);
                using var reader = new BinaryReader(stream);

                var count = reader.ReadInt32();

                for (var i = 0; i < count; i++)
                {
                    var key = reader.ReadString();
                    var hashLength = reader.ReadInt32();
                    var value = reader.ReadBytes(hashLength);

                    this.hashList.Add(key, value);
                }
            }
        }

        public void WriteFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            
            writer.Write(this.hashList.Count);

            foreach (var item in this.hashList)
            {
                writer.Write(item.Key);
                writer.Write(item.Value.Length);
                writer.Write(item.Value);
            }
        }

        private static ReadOnlySpan<byte> ComputeFileHash(ReadOnlySpan<byte> inputData)
        {
            // TODO: Find a way to avoid array copy
            using var md5 = MD5.Create();
            using var stream = new MemoryStream(inputData.ToArray());
            
            return new ReadOnlySpan<byte>(md5.ComputeHash(stream));
        }
    }
}