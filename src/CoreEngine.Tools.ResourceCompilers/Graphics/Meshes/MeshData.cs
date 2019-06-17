using System;
using System.Collections.Generic;
using System.Numerics;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class MeshData
    {
        public List<MeshVertex> Vertices { get; } = new List<MeshVertex>();
        public List<uint> Indices { get; } = new List<uint>();
        public IList<MeshSubObject> MeshSubObjects { get; } = new List<MeshSubObject>();
    }

    public class MeshSubObject
    {
        public uint StartIndex;
        public uint IndexCount;
    }

    public struct MeshVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
    }
}