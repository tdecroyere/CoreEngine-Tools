using System;
using System.Threading.Tasks;

namespace CoreEngine.ResourceCompilers
{
    public abstract class ResourceDataCompiler
    {
        public abstract string Name
        {
            get;
        }

        public abstract string[] SupportedSourceExtensions
        {
            get;
        }

        public abstract string DestinationExtension
        {
            get;
        }

        public abstract Task<byte[]> CompileAsync(byte[] sourceData);
    }
}
