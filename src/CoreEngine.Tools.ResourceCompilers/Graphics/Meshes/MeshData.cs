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
        public MeshSubObject()
        {
            this.BoundingBox = new BoundingBox();
        }

        public uint StartIndex { get; set; }
        public uint IndexCount { get; set; }
        public BoundingBox BoundingBox { get; set; }
    }

    public struct MeshVertex
    {
        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }
    }
}