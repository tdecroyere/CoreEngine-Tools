using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers
{
    public abstract class ResourceDataCompiler
    {
        public abstract string Name
        {
            get;
        }

        public abstract IList<string> SupportedSourceExtensions
        {
            get;
        }

        public abstract string DestinationExtension
        {
            get;
        }

        public virtual string? MultipleOutputDirectory
        {
            get
            {
                return null;
            }
        }

        public abstract Task<ReadOnlyMemory<ResourceEntry>> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context);
    }
}
