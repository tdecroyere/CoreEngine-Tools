using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Materials
{
    public class ObjMaterialDataReader : IMaterialDataReader
    {
        public ObjMaterialDataReader()
        {

        }
   
        public Span<MaterialDescription> Read(ReadOnlySpan<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var materials = new List<MaterialDescription>();
            MaterialDescription? currentMaterial = null;

            var currentDiffuseColor = new float[4];
            var currentDiffuseTexture = string.Empty;
            var currentNormalTexture = string.Empty;
            var currentBumpTexture = string.Empty;
            
            using var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine()!;

                // TODO: Wait for the Span<char> split method that is currenctly in dev
                var lineParts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (lineParts.Length > 1)
                {
                    if (lineParts[0] == "newmtl")
                    {
                        if (currentMaterial != null)
                        {
                            currentMaterial.Properties.Add(new MaterialProperty("DiffuseColor", currentDiffuseColor));
                            currentMaterial.Properties.Add(new MaterialProperty("DiffuseTexture", currentDiffuseTexture));
                            currentMaterial.Properties.Add(new MaterialProperty("NormalTexture", currentNormalTexture));
                            currentMaterial.Properties.Add(new MaterialProperty("BumpTexture", currentBumpTexture));

                            materials.Add(currentMaterial);

                            currentDiffuseColor = new float[4];
                            currentDiffuseTexture = string.Empty;
                            currentNormalTexture = string.Empty;
                            currentBumpTexture = string.Empty;
                        }

                        var materialName = lineParts[1];

                        Logger.WriteMessage($"Reading Material: {materialName}");
                        currentMaterial = new MaterialDescription(materialName);
                    }

                    if (lineParts[0].ToLower() == "map_disp")
                    {
                        var texturePath = lineParts[1];
                        var rootDirectory = Path.GetDirectoryName(context.OutputDirectory);
                        currentNormalTexture = $"{context.OutputDirectory.Replace(rootDirectory, string.Empty)}/{Path.GetDirectoryName(texturePath)}/{Path.GetFileNameWithoutExtension(texturePath)}.texture";
                    }

                    else if (lineParts[0].ToLower() == "map_bump")
                    {
                        var texturePath = lineParts[1];
                        var rootDirectory = Path.GetDirectoryName(context.OutputDirectory);
                        currentBumpTexture = $"{context.OutputDirectory.Replace(rootDirectory, string.Empty)}/{Path.GetDirectoryName(texturePath)}/{Path.GetFileNameWithoutExtension(texturePath)}.texture";
                    }

                    else if (lineParts[0].ToLower() == "map_kd")
                    {
                        var texturePath = lineParts[1];
                        var rootDirectory = Path.GetDirectoryName(context.OutputDirectory);
                        currentDiffuseTexture = $"{context.OutputDirectory.Replace(rootDirectory, string.Empty)}/{Path.GetDirectoryName(texturePath)}/{Path.GetFileNameWithoutExtension(texturePath)}.texture";
                    }

                    else if (lineParts[0].ToLower() == "kd")
                    {
                        var red = float.Parse(lineParts[1], CultureInfo.InvariantCulture);
                        var green = float.Parse(lineParts[2], CultureInfo.InvariantCulture);
                        var blue = float.Parse(lineParts[3], CultureInfo.InvariantCulture);
                        currentDiffuseColor = new float[4] { red, green, blue, 1.0f };
                    }

                    else if (lineParts[0].ToLower() == "map_d")
                    {
                        currentMaterial.IsTransparent = true;
                    }
                }
            }

            if (currentMaterial != null)
            {
                currentMaterial.Properties.Add(new MaterialProperty("DiffuseColor", currentDiffuseColor));
                currentMaterial.Properties.Add(new MaterialProperty("DiffuseTexture", currentDiffuseTexture));
                currentMaterial.Properties.Add(new MaterialProperty("NormalTexture", currentNormalTexture));
                currentMaterial.Properties.Add(new MaterialProperty("BumpTexture", currentBumpTexture));

                materials.Add(currentMaterial);
            }

            return materials.ToArray();
        }
    }
}