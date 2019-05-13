using System;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers
{
    public abstract class ResourceDataCompiler
    {
        protected ResourceDataCompiler(Logger logger)
        {
            this.Logger = logger;
        }
        
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

        protected Logger Logger
        {
            get;
            private set;
        }

        public abstract Task<ReadOnlyMemory<byte>?> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context);
    }
}
