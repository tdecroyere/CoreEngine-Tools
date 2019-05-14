using System;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public abstract class MeshDataReader
    {
        protected MeshDataReader(Logger logger)
        {
            this.Logger = logger;
        }

        protected Logger Logger
        {
            get;
            private set;
        }

        public abstract Task<MeshData?> ReadAsync(ReadOnlyMemory<byte> sourceData);
    }
}