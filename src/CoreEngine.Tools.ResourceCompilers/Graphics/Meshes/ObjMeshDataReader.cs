using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using CoreEngine.Tools.Common;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Meshes
{
    struct FaceElement
    {
        public uint VertexIndex;
        public uint TextureCoordinatesIndex;
        public uint NormalIndex;
    }

    public class ObjMeshDataReader : MeshDataReader
    {
        public ObjMeshDataReader(Logger logger) : base(logger)
        {

        }
        
        public override Task<MeshData?> ReadAsync(ReadOnlyMemory<byte> sourceData)
        {
            var result = new MeshData();

            var vertexDictionary = new Dictionary<MeshVertex, uint>();
            var vertexList = new List<Vector3>();
            var vertexNormalList = new List<Vector3>();

            MeshSubObject? currentSubObject = null;

            // TODO: Try to avoid the ToArray call that copy the buffer to the MemoryStream
            using var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                // TODO: Wait for the Span<char> split method that is currenctly in dev
                var lineParts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (lineParts.Length > 1)
                {
                    if (lineParts[0] == "g")
                    {
                        if (currentSubObject != null && currentSubObject.Indices.Count > 0)
                        {
                            this.Logger.WriteMessage($"Readed vertices: {currentSubObject.Vertices.Count}");
                            this.Logger.WriteMessage($"Readed Indices: {currentSubObject.Indices.Count}");

                            result.MeshSubObjects.Add(currentSubObject);
                        }

                        this.Logger.WriteMessage($"Reading sub-object: {(lineParts.Length > 1 ? lineParts[1] : "no-name")}");
                        currentSubObject = new MeshSubObject();
                    }

                    if (lineParts[0] == "v")
                    {
                        ParseVectorElement(vertexList, line.AsSpan());
                    }

                    else if (lineParts[0] == "vn")
                    {
                        ParseVectorElement(vertexNormalList, line.AsSpan());
                    }

                    else if (lineParts[0] == "f")
                    {
                        ParseFace(currentSubObject!, vertexDictionary, vertexList, vertexNormalList, line.AsSpan());
                    }
                }
            }

            if (currentSubObject != null && currentSubObject.Indices.Count > 0)
            {
                this.Logger.WriteMessage($"Readed vertices: {currentSubObject.Vertices.Count}");
                this.Logger.WriteMessage($"Readed Indices: {currentSubObject.Indices.Count}");

                result.MeshSubObjects.Add(currentSubObject);

                vertexList.Clear();
                vertexNormalList.Clear();
            }

            return Task.FromResult<MeshData?>(result);
        }

        private static void ParseVectorElement(List<Vector3> vectorList, ReadOnlySpan<char> line)
        {
            // TODO: Wait for the Span<char> split method that is currenctly in dev

            var stringLine = line.ToString();
            var lineParts = stringLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (lineParts.Length < 4)
            {
                throw new InvalidDataException("Invalid obj vertor line");
            }

            var x = float.Parse(lineParts[1], CultureInfo.InvariantCulture);
            var y = float.Parse(lineParts[2], CultureInfo.InvariantCulture);
            var z = float.Parse(lineParts[3], CultureInfo.InvariantCulture);

            vectorList.Add(new Vector3(x, y , z));
        }

        private static void ParseFace(MeshSubObject meshSubObject, Dictionary<MeshVertex, uint> vertexDictionary, List<Vector3> vertexList, List<Vector3> vertexNormalList, ReadOnlySpan<char> line)
        {
            // TODO: Wait for the Span<char> split method that is currenctly in dev

            var stringLine = line.ToString();
            var lineParts = stringLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (lineParts.Length < 4)
            {
                throw new InvalidDataException("Invalid obj vertex line");
            }

            var element1 = ParceFaceElement(lineParts[1]);
            var element2 = ParceFaceElement(lineParts[2]);
            var element3 = ParceFaceElement(lineParts[3]);

            AddFaceElement(meshSubObject, vertexDictionary, vertexList, vertexNormalList, element1);
            AddFaceElement(meshSubObject, vertexDictionary, vertexList, vertexNormalList, element2);
            AddFaceElement(meshSubObject, vertexDictionary, vertexList, vertexNormalList, element3);
        }

        private static void AddFaceElement(MeshSubObject meshSubObject, Dictionary<MeshVertex, uint> vertexDictionary, List<Vector3> vertexList, List<Vector3> vertexNormalList, FaceElement faceElement)
        {
            var vertex = ConstructVertex(vertexList, vertexNormalList, faceElement);

            if (!vertexDictionary.ContainsKey(vertex))
            {
                meshSubObject.Indices.Add((uint)meshSubObject.Vertices.Count);
                vertexDictionary.Add(vertex, (uint)meshSubObject.Vertices.Count);
                meshSubObject.Vertices.Add(vertex);
            }

            else
            {
                var vertexIndex = vertexDictionary[vertex];
                meshSubObject.Indices.Add((uint)vertexIndex);
            }
        }

        private static FaceElement ParceFaceElement(string faceElement)
        {
            var result = new FaceElement();

            var faceElements = faceElement.Split('/');

            result.VertexIndex = uint.Parse(faceElements[0], CultureInfo.InvariantCulture);

            if (faceElements.Length > 1 && !string.IsNullOrEmpty(faceElements[1]))
            {
                result.TextureCoordinatesIndex = uint.Parse(faceElements[1], CultureInfo.InvariantCulture);
            }

            if (faceElements.Length > 2 && !string.IsNullOrEmpty(faceElements[2]))
            {
                result.NormalIndex = uint.Parse(faceElements[2], CultureInfo.InvariantCulture);
            }

            return result;
        }
        
        private static MeshVertex ConstructVertex(List<Vector3> vertexList, List<Vector3> vertexNormalList, FaceElement faceElement)
        {
            var result = new MeshVertex();

            if (faceElement.VertexIndex != 0)
            {
                result.Position = vertexList[(int)faceElement.VertexIndex - 1];
            }

            if (faceElement.NormalIndex != 0)
            {
                result.Normal = vertexNormalList[(int)faceElement.NormalIndex - 1];
            }

            return result;
        }
    }
}