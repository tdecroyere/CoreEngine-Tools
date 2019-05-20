using System;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class MeshResourceDataCompiler : ResourceDataCompiler
    {
        public MeshResourceDataCompiler(Logger logger) : base(logger)
        {

        }
        
        public override string Name
        {
            get
            {
                return "Mesh Resource Data Compiler";
            }
        }

        public override string[] SupportedSourceExtensions
        {
            get
            {
                return new string[] { ".obj" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".mesh";
            }
        }

        public override async Task<ReadOnlyMemory<byte>?> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            var version = 1;

            this.Logger.WriteMessage("Mesh compiler OK");

            // TODO: Add extension to the parameters in order to do a factory here base on the file extension

            MeshDataReader? meshDataReader = null;

            if (Path.GetExtension(context.TargetFilename) == ".obj")
            {
                meshDataReader = new ObjMeshDataReader(this.Logger);
            }

            // TODO: Optimize mesh indices

            if (meshDataReader != null)
            {
                var meshData = await meshDataReader.ReadAsync(sourceData);

                if (meshData != null)
                {
                    var destinationMemoryStream = new MemoryStream();

                    using var streamWriter = new BinaryWriter(destinationMemoryStream);
                    streamWriter.Write(new char[] { 'M', 'E', 'S', 'H'});
                    streamWriter.Write(version);
                    streamWriter.Write(meshData.MeshSubObjects.Count);

                    foreach (var subObject in meshData.MeshSubObjects)
                    {
                        streamWriter.Write(subObject.Vertices.Count);
                        streamWriter.Write(subObject.Indices.Count);
                        
                        foreach (var vertex in subObject.Vertices)
                        {
                            streamWriter.Write(vertex.Position.X);
                            streamWriter.Write(vertex.Position.Y);
                            streamWriter.Write(vertex.Position.Z);
                            streamWriter.Write(vertex.Normal.X);
                            streamWriter.Write(vertex.Normal.Y);
                            streamWriter.Write(vertex.Normal.Z);
                        }

                        foreach (var index in subObject.Indices)
                        {
                            streamWriter.Write(index);
                        }
                    }

                    streamWriter.Flush();

                    destinationMemoryStream.Flush();
                    return new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
                }
            }

            return null;
        }
    }
}