using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class MeshResourceDataCompiler : ResourceDataCompiler
    {
        public override string Name
        {
            get
            {
                return "Mesh Resource Data Compiler";
            }
        }

        public override IList<string> SupportedSourceExtensions
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

        public override async Task<ReadOnlyMemory<ResourceEntry>> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var version = 1;

            // TODO: Add extension to the parameters in order to do a factory here base on the file extension

            MeshDataReader? meshDataReader = null;

            if (Path.GetExtension(context.SourceFilename) == ".obj")
            {
                meshDataReader = new ObjMeshDataReader();
            }

            if (meshDataReader != null)
            {
                var meshData = await meshDataReader.ReadAsync(sourceData);

                if (meshData != null)
                {
                    // TODO: Optimize mesh indices

                    // Compute Bounding Boxes
                    foreach (var subObject in meshData.MeshSubObjects)
                    {
                        Logger.BeginAction($"Computing Bounding Box");

                        for (var i = 0; i < subObject.IndexCount; i++)
                        {
                            var index = meshData.Indices[i + (int)subObject.StartIndex];
                            var vertex = meshData.Vertices[(int)index];

                            subObject.BoundingBox.Add(vertex.Position);
                        }

                        Logger.WriteMessage($"Bounding Box: {subObject.BoundingBox}");
                        Logger.EndAction();
                    }

                    var destinationMemoryStream = new MemoryStream();

                    using var streamWriter = new BinaryWriter(destinationMemoryStream);
                    streamWriter.Write(new char[] { 'M', 'E', 'S', 'H'});
                    streamWriter.Write(version);

                    streamWriter.Write(meshData.Vertices.Count);
                    streamWriter.Write(meshData.Indices.Count);
                    
                    // TODO: Currently we add a padding 0 to be aligned
                    foreach (var vertex in meshData.Vertices)
                    {
                        streamWriter.Write(vertex.Position.X);
                        streamWriter.Write(vertex.Position.Y);
                        streamWriter.Write(vertex.Position.Z);
                        streamWriter.Write(0.0f);
                        streamWriter.Write(vertex.Normal.X);
                        streamWriter.Write(vertex.Normal.Y);
                        streamWriter.Write(vertex.Normal.Z);
                        streamWriter.Write(0.0f);
                        streamWriter.Write(vertex.TextureCoordinates.X);
                        streamWriter.Write(vertex.TextureCoordinates.Y);
                        streamWriter.Write(0.0f);
                        streamWriter.Write(0.0f);
                    }

                    foreach (var index in meshData.Indices)
                    {
                        streamWriter.Write(index);
                    }

                    streamWriter.Write(meshData.MeshSubObjects.Count);

                    foreach (var subObject in meshData.MeshSubObjects)
                    {
                        // TODO: Replace that real material path
                        if (string.IsNullOrEmpty(subObject.MaterialPath))
                        {
                            streamWriter.Write($"");
                        }

                        else
                        {
                            streamWriter.Write($"{subObject.MaterialPath}.material");
                        }

                        streamWriter.Write(subObject.StartIndex);
                        streamWriter.Write(subObject.IndexCount);
                        streamWriter.Write(subObject.BoundingBox.MinPoint.X);
                        streamWriter.Write(subObject.BoundingBox.MinPoint.Y);
                        streamWriter.Write(subObject.BoundingBox.MinPoint.Z);
                        streamWriter.Write(subObject.BoundingBox.MaxPoint.X);
                        streamWriter.Write(subObject.BoundingBox.MaxPoint.Y);
                        streamWriter.Write(subObject.BoundingBox.MaxPoint.Z);
                    }

                    streamWriter.Flush();

                    destinationMemoryStream.Flush();
                    var resourceData = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
                    var resourceEntry = new ResourceEntry($"{Path.GetFileNameWithoutExtension(context.SourceFilename)}{this.DestinationExtension}", resourceData);

                    return new ReadOnlyMemory<ResourceEntry>(new ResourceEntry[] { resourceEntry });
                }
            }

            return null;
        }
    }
}