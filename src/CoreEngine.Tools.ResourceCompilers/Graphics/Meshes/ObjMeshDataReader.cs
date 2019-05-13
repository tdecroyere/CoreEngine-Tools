using System;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class ObjMeshDataReader : MeshDataReader
    {
        public ObjMeshDataReader(Logger logger) : base(logger)
        {

        }
        
        public override Task<byte[]?> ReadAsync(ReadOnlyMemory<byte> sourceData)
        {
            Console.WriteLine("OBJ Loader OK");
            return Task.FromResult<byte[]?>(new byte[0]);
        }
    }
}