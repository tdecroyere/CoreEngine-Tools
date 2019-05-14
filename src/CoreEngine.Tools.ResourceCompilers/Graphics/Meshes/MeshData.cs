using System;
using System.Collections.Generic;
using System.Numerics;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class MeshData
    {
        public IList<MeshSubObject> MeshSubObjects { get; } = new List<MeshSubObject>();
    }

    public class MeshSubObject
    {
        public IList<MeshVertex> Vertices { get; } = new List<MeshVertex>();
        public IList<ushort> Indices { get; } = new List<ushort>();
    }

    public class MeshVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
    }
}