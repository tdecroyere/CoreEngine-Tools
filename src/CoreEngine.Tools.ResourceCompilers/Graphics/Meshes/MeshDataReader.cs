using System;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public abstract class MeshDataReader
    {
        public abstract Task<MeshData?> ReadAsync(ReadOnlyMemory<byte> sourceData);
    }
}