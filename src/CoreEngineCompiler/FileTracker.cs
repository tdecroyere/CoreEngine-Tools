using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CoreEngine.Compiler
{
    public class FileTracker
    {
        private Dictionary<string, long> fileTracker;

        public FileTracker()
        {
            this.fileTracker = new Dictionary<string, long>();
        }

        public bool HasFileChanged(string path)
        {
            var lastWriteTime = File.GetLastWriteTime(path).ToBinary();

            if (this.fileTracker.ContainsKey(path) && lastWriteTime <= (this.fileTracker[path]))
            {
                return false;
            }

            if (!this.fileTracker.ContainsKey(path))
            {
                this.fileTracker.Add(path, lastWriteTime);
            }

            else
            {
                this.fileTracker[path] = lastWriteTime;
            }

            return true;
        }

        public void ReadFile(string path)
        {
            if (File.Exists(path))
            {
                this.fileTracker.Clear();

                using var stream = new FileStream(path, FileMode.Open);
                using var reader = new BinaryReader(stream);

                var count = reader.ReadInt32();

                for (var i = 0; i < count; i++)
                {
                    var key = reader.ReadString();
                    var value = reader.ReadInt64();

                    this.fileTracker.Add(key, value);
                }
            }
        }

        public void WriteFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            
            writer.Write(this.fileTracker.Count);

            foreach (var item in this.fileTracker)
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }

            writer.Flush();
        }
    }
}