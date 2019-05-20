using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using CoreEngine.Tools.Common;
using System.Numerics;
using System.Globalization;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    public class ObjMeshDataReader : MeshDataReader
    {
        public ObjMeshDataReader(Logger logger) : base(logger)
        {

        }
        
        public override Task<MeshData?> ReadAsync(ReadOnlyMemory<byte> sourceData)
        {
            var result = new MeshData();

            // Process whole file as a single sub object for now
            var currentSubObject = new MeshSubObject();
            result.MeshSubObjects.Add(currentSubObject);

            var currentIndex = 0;
            
            this.Logger.WriteMessage("OBJ Loader OK");

            // TODO: Try to avoid the ToArray call that copy the buffer to the MemoryStream
            using var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                // TODO: Wait for the Span<char> split method that is currenctly in dev
                var lineParts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (lineParts.Length > 1)
                {
                    if (lineParts[0] == "v")
                    {
                        ParseVertex(currentSubObject, currentIndex, line.AsSpan());
                        currentIndex++;
                    }

                    else if (lineParts[0] == "vn")
                    {
                        ParseVertexNormal(currentSubObject, currentIndex, line.AsSpan());
                        currentIndex++;
                    }

                    else if (lineParts[0] == "f")
                    {
                        ParseFace(currentSubObject, line.AsSpan());
                    }
                }

                else
                {
                    currentIndex = 0;
                }
            }

            this.Logger.WriteMessage($"Readed vertices: {currentSubObject.Vertices.Count}");
            this.Logger.WriteMessage($"Readed Indices: {currentSubObject.Indices.Count}");

            return Task.FromResult<MeshData?>(result);
        }

        private static void ParseVertex(MeshSubObject meshSubObject, int currentVertexIndex, ReadOnlySpan<char> line)
        {
            // TODO: Wait for the Span<char> split method that is currenctly in dev

            var stringLine = line.ToString();
            var lineParts = stringLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (lineParts.Length < 4)
            {
                throw new InvalidDataException("Invalid obj vertex line");
            }

            var x = float.Parse(lineParts[1], CultureInfo.InvariantCulture);
            var y = float.Parse(lineParts[2], CultureInfo.InvariantCulture);
            var z = float.Parse(lineParts[3], CultureInfo.InvariantCulture);

            if (meshSubObject.Vertices.Count <= currentVertexIndex)
            {
                meshSubObject.Vertices.Add(new MeshVertex());
            }

            var vertex = meshSubObject.Vertices[currentVertexIndex];
            vertex.Position = new Vector3(x, y , z);
        }

        private static void ParseVertexNormal(MeshSubObject meshSubObject, int currentVertexIndex, ReadOnlySpan<char> line)
        {
            // TODO: Wait for the Span<char> split method that is currenctly in dev

            var stringLine = line.ToString();
            var lineParts = stringLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (lineParts.Length < 4)
            {
                throw new InvalidDataException("Invalid obj vertex line");
            }

            var x = float.Parse(lineParts[1], CultureInfo.InvariantCulture);
            var y = float.Parse(lineParts[2], CultureInfo.InvariantCulture);
            var z = float.Parse(lineParts[3], CultureInfo.InvariantCulture);

            if (meshSubObject.Vertices.Count <= currentVertexIndex)
            {
                meshSubObject.Vertices.Add(new MeshVertex());
            }

            var vertex = meshSubObject.Vertices[currentVertexIndex];
            vertex.Normal = new Vector3(x, y , z);
        }

        private static void ParseFace(MeshSubObject meshSubObject, ReadOnlySpan<char> line)
        {
            // TODO: Wait for the Span<char> split method that is currenctly in dev

            var stringLine = line.ToString();
            var lineParts = stringLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (lineParts.Length < 4)
            {
                throw new InvalidDataException("Invalid obj vertex line");
            }

            var vertex1 = uint.Parse(lineParts[1].Split('/')[0], CultureInfo.InvariantCulture);
            var vertex2 = uint.Parse(lineParts[2].Split('/')[0], CultureInfo.InvariantCulture);
            var vertex3 = uint.Parse(lineParts[3].Split('/')[0], CultureInfo.InvariantCulture);

            meshSubObject.Indices.Add((uint)(vertex1 - 1));
            meshSubObject.Indices.Add((uint)(vertex2 - 1));
            meshSubObject.Indices.Add((uint)(vertex3 - 1));
        }
    }
}