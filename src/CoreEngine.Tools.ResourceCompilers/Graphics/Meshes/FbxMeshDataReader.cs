using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using Assimp;
using System.IO;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class FbxMeshDataReader : MeshDataReader
    {
        private string path;

        public FbxMeshDataReader(string path)
        {
            this.path = path;
        }

        public override Task<MeshData?> ReadAsync(ReadOnlyMemory<byte> sourceData)
        {
            var result = new MeshData();

            using var memoryStream = new MemoryStream(sourceData.ToArray());
            using AssimpContext importer = new AssimpContext();
            var scene = importer.ImportFileFromStream(memoryStream, PostProcessSteps.ImproveCacheLocality | 
                                                                    // PostProcessSteps.OptimizeGraph | 
                                                                    // PostProcessSteps.OptimizeMeshes | 
                                                                    //PostProcessSteps.PreTransformVertices | 
                                                                    PostProcessSteps.MakeLeftHanded);

            // foreach (var mesh in scene.Meshes)
            // {
            //     AddMesh(result, scene, mesh, Assimp.Matrix4x4.Identity);
            // }

            ProcessNode(result, scene, scene.RootNode, scene.RootNode.Transform);

            return Task.FromResult<MeshData?>(result);
        }

        private void ProcessNode(MeshData meshData, Scene scene, Node node, Assimp.Matrix4x4 transform)
        {
            Logger.BeginAction($"{node.Name} - {node.MeshCount} - {transform}");

            if (node.MeshCount > 0)
            {
                foreach (var meshIndex in node.MeshIndices)
                {
                    var mesh = scene.Meshes[meshIndex];
                    AddMesh(meshData, scene, mesh, node.Transform * transform);
                }
            }

            foreach (var childNode in node.Children)
            {
                ProcessNode(meshData, scene, childNode, node.Transform * transform);
            }

            Logger.EndAction();
        }

        private void AddMesh(MeshData meshData, Scene scene, Mesh mesh, Assimp.Matrix4x4 transform)
        {
            var subMesh = new MeshSubObject();
            subMesh.StartIndex = (uint)meshData.Indices.Count;
            subMesh.MaterialPath = scene.Materials[mesh.MaterialIndex].Name;
            
            for (var i = 0; i < mesh.FaceCount; i++)
            {
                var face = mesh.Faces[i];

                for (var j = 0; j < face.IndexCount; j++)
                {
                    var vertex = new MeshVertex();

                    var fbxIndex = face.Indices[face.IndexCount - 1 - j];

                    var fbxVertex = transform * mesh.Vertices[fbxIndex];
                    var fbxNormal = (Matrix3x3)transform * mesh.Normals[fbxIndex];

                    vertex.Position = new Vector3(fbxVertex.X, fbxVertex.Y, fbxVertex.Z);
                    vertex.Normal = new Vector3(fbxNormal.X, fbxNormal.Y, fbxNormal.Z);
                    vertex.TextureCoordinates = new Vector2(mesh.TextureCoordinateChannels[0][fbxIndex].X, -mesh.TextureCoordinateChannels[0][fbxIndex].Y);

                    meshData.Indices.Add((uint)meshData.Vertices.Count);
                    meshData.Vertices.Add(vertex);
                }
            }

            subMesh.IndexCount = (uint)meshData.Indices.Count - subMesh.StartIndex;
            meshData.MeshSubObjects.Add(subMesh);
        }
    }
}