using System;

namespace CoreEngine.Tools.ResourceCompilers
{
    public class ResourceEntry
    {
        public ResourceEntry(string filename, ReadOnlyMemory<byte> data)
        {
            this.Filename = filename;
            this.Data = data;
        }

        public string Filename { get; }
        public ReadOnlyMemory<byte> Data { get; }
    }
}