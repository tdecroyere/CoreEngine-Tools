using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CoreEngine.Compiler
{
    public class HashListFile
    {
        private Dictionary<string, string> hashList;

        public HashListFile(string path)
        {
            this.hashList = new Dictionary<string, string>();
        }

        public bool HasFileChanged(string path)
        {
            var hash = ComputeFileHash(path);

            if (this.hashList.ContainsKey(path) && this.hashList[path] == hash)
            {
                return false;
            }

            if (!this.hashList.ContainsKey(path))
            {
                this.hashList.Add(path, hash);
            }

            else
            {
                this.hashList[path] = hash;
            }

            return true;
        }

        private static string ComputeFileHash(string path)
        {
            // DateTime current = DateTime.Now;
            // string file = @"C:\text.iso";//It's 2.5 Gb file
            // string output;
            // using (var md5 = MD5.Create())
            // {
            //     using (var stream = File.OpenRead(file))
            //     {
            //         byte[] checksum = md5.ComputeHash(stream);
            //         output = BitConverter.ToString(checksum).Replace("-", String.Empty).ToLower();
            //         Console.WriteLine("Total seconds : " + (DateTime.Now - current).TotalSeconds.ToString() + " " + output);
            //     }
            // }
            return "";
        }
    }
}