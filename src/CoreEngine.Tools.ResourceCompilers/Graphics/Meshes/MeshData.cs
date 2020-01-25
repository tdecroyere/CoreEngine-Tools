using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
            this.MaterialPath = string.Empty;
        }

        public uint StartIndex { get; set; }
        public uint IndexCount { get; set; }
        public BoundingBox BoundingBox { get; set; }
        public string MaterialPath { get; set; }
    }

    public struct MeshVertex
    {
        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }
        public Vector2 TextureCoordinates { get; set; }

        public override int GetHashCode() 
        {
            return this.Position.GetHashCode() ^ 
                   this.Normal.GetHashCode() ^ 
                   this.TextureCoordinates.GetHashCode();
        }

        public override bool Equals(Object? obj) 
        {
            return obj is MeshVertex && this == (MeshVertex)obj;
        }

        public bool Equals(MeshVertex other)
        {
            return this == other;
        }

        public static bool operator ==(MeshVertex vertex1, MeshVertex vertex2) 
        {
            return vertex1.Position == vertex2.Position && vertex1.Normal == vertex2.Normal && vertex1.TextureCoordinates == vertex2.TextureCoordinates;
        }

        public static bool operator !=(MeshVertex layout1, MeshVertex layout2) 
        {
            return !(layout1 == layout2);
        }
    }
}