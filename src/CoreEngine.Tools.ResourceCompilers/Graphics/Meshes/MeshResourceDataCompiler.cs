using System;
using System.Collections.Generic;
using System.Linq;
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
                return new string[] { ".obj", ".fbx" };
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

            else if (Path.GetExtension(context.SourceFilename) == ".fbx")
            {
                meshDataReader = new FbxMeshDataReader(Path.Combine(context.InputDirectory, context.SourceFilename));
            }

            if (meshDataReader != null)
            {
                var meshData = await meshDataReader.ReadAsync(sourceData);

                if (meshData != null)
                {
                    // Compute Bounding Boxes
                    foreach (var subObject in meshData.MeshSubObjects)
                    {
                        for (var i = 0; i < subObject.IndexCount; i++)
                        {
                            var index = meshData.Indices[i + (int)subObject.StartIndex];
                            var vertex = meshData.Vertices[(int)index];

                            subObject.BoundingBox.Add(vertex.Position);
                        }
                    }

                    // TODO: Optimize mesh instances 
                    Logger.WriteMessage($"Before - Mesh SubObjects: {meshData.MeshSubObjects.Count} - Vertex Buffer Count: {meshData.Vertices.Count} - Index Buffer Count: {meshData.Indices.Count}");

                    meshData = OptimizeMesh(meshData);

                    Logger.WriteMessage($"After - Mesh SubObjects: {meshData.MeshSubObjects.Count} - Vertex Buffer Count: {meshData.Vertices.Count} - Index Buffer Count: {meshData.Indices.Count}");
                    
                    // TODO: Optimize mesh indices

                    // Compute Bounding Boxes
                    foreach (var subObject in meshData.MeshSubObjects)
                    {
                        for (var i = 0; i < subObject.IndexCount; i++)
                        {
                            var index = meshData.Indices[i + (int)subObject.StartIndex];
                            var vertex = meshData.Vertices[(int)index];

                            subObject.BoundingBox.Add(vertex.Position);
                        }
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
                        streamWriter.Write(vertex.Normal.X);
                        streamWriter.Write(vertex.Normal.Y);
                        streamWriter.Write(vertex.Normal.Z);
                        streamWriter.Write(vertex.TextureCoordinates.X);
                        streamWriter.Write(vertex.TextureCoordinates.Y);
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

        // TODO: Implement BVH to optimize GPU culling

        private MeshData OptimizeMesh(MeshData mesh)
        {
            // TODO: Compute the max object space based on the global bounding box of the mesh
            var maxObjectSpace = 1000;

            var result = new MeshData();

            var vertexIndexList = new Dictionary<MeshVertex, int>();
            var currentSubMesh = new MeshSubObject();

            foreach (var subMesh in mesh.MeshSubObjects.OrderBy(x => x.MaterialPath))
            {
                // if (currentSubMesh.MaterialPath != subMesh.MaterialPath || ((currentSubMesh.BoundingBox.Center - subMesh.BoundingBox.Center).Length() > maxObjectSpace))
                // {
                    currentSubMesh.BoundingBox = new BoundingBox();
                    currentSubMesh.IndexCount = (uint)(result.Indices.Count - currentSubMesh.StartIndex);

                    if (currentSubMesh.IndexCount > 0)
                    {
                        result.MeshSubObjects.Add(currentSubMesh);
                    }

                    vertexIndexList.Clear();
                    //vertexIndexList = new Dictionary<MeshVertex, int>();
                    currentSubMesh = new MeshSubObject();
                    currentSubMesh.MaterialPath = subMesh.MaterialPath;
                    currentSubMesh.StartIndex = (uint)result.Indices.Count;
                    currentSubMesh.BoundingBox = subMesh.BoundingBox;
                //}

                for (var i = (int)subMesh.StartIndex; i < (subMesh.StartIndex + subMesh.IndexCount); i++)
                {
                    var index = mesh.Indices[i];
                    var vertex = mesh.Vertices[(int)index];

                    if (!vertexIndexList.ContainsKey(vertex))
                    {
                        vertexIndexList.Add(vertex, result.Vertices.Count);
                        result.Indices.Add((uint)result.Vertices.Count);
                        result.Vertices.Add(vertex);
                    }

                    else
                    {
                        result.Indices.Add((uint)vertexIndexList[vertex]);
                    }
                }
            }

            currentSubMesh.IndexCount = (uint)(result.Indices.Count - currentSubMesh.StartIndex);

            if (currentSubMesh.IndexCount > 0)
            {
                result.MeshSubObjects.Add(currentSubMesh);
            }

            return result;
        }
    }
}