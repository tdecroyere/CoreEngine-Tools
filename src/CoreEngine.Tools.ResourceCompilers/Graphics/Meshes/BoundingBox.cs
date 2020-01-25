using System;
using System.Numerics;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class BoundingBox
    {
        public BoundingBox() : this(new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity), new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity))
        {
        }

		public BoundingBox(Vector3 minPoint, Vector3 maxPoint)
		{
			this.MinPoint = minPoint;
			this.MaxPoint = maxPoint;
		}

        public Vector3 MinPoint { get; private set; }
        public Vector3 MaxPoint { get; private set; }

        public bool IsEmpty
		{
			get
			{
                return (this.MinPoint.X > this.MaxPoint.X) || (this.MinPoint.Y > this.MaxPoint.Y) || (this.MinPoint.Z > this.MaxPoint.Z);
			}
		}

        public void Add(Vector3 point)
		{
			float minX = (point.X < this.MinPoint.X) ? point.X : this.MinPoint.X;
			float minY = (point.Y < this.MinPoint.Y) ? point.Y : this.MinPoint.Y;
			float minZ = (point.Z < this.MinPoint.Z) ? point.Z : this.MinPoint.Z;

			float maxX = (point.X > this.MaxPoint.X) ? point.X : this.MaxPoint.X;
			float maxY = (point.Y > this.MaxPoint.Y) ? point.Y : this.MaxPoint.Y;
			float maxZ = (point.Z > this.MaxPoint.Z) ? point.Z : this.MaxPoint.Z;
	
			this.MinPoint = new Vector3(minX, minY, minZ);
			this.MaxPoint = new Vector3(maxX, maxY, maxZ);
		}

		public Vector3 Center
		{
			get
			{
                return (this.MinPoint + this.MaxPoint) * 0.5f;
			}
		}

        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "Empty";
            }

            return $"Min: {this.MinPoint}, Max: {this.MaxPoint}";
        }
    }
}