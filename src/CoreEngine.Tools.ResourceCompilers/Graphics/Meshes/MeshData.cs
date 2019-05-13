using System;
using System.Collections.Generic;
using System.Numerics;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class MeshData
    {
        public IList<MeshObject> MeshObjects { get; } = new List<MeshObject>();
    }

    public class MeshObject
    {
        public IList<MeshVertex> Vertices { get; } = new List<MeshVertex>();
        public IList<short> Indices { get; } = new List<short>();
    }

    public class MeshVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
    }
}