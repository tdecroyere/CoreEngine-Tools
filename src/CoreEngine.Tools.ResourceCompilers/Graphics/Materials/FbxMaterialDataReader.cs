using System;
using System.Numerics;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using Assimp;
using System.IO;
using System.Collections.Generic;
using TeximpNet;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Materials
{
    public class FbxMaterialDataReader : IMaterialDataReader
    {
        public Span<MaterialDescription> Read(ReadOnlySpan<byte> sourceData, CompilerContext context)
        {
            var materials = new List<MaterialDescription>();

            using var memoryStream = new MemoryStream(sourceData.ToArray());
            using AssimpContext importer = new AssimpContext();
            var scene = importer.ImportFileFromStream(memoryStream);

            foreach (var fbxMaterial in scene.Materials)
            {
                Logger.WriteMessage($"Processing Material '{fbxMaterial.Name}'...");
                var material = new MaterialDescription(fbxMaterial.Name);
                material.IsTransparent = false; //fbxMaterial.Name.ToLower().Contains("blendshader") || fbxMaterial.Name.ToLower().Contains("doublesided");

                var diffuseColor = new float[4];

                if (fbxMaterial.HasColorDiffuse)
                {
                    diffuseColor[0] = fbxMaterial.ColorDiffuse.R;
                    diffuseColor[1] = fbxMaterial.ColorDiffuse.G;
                    diffuseColor[2] = fbxMaterial.ColorDiffuse.B;
                    diffuseColor[3] = fbxMaterial.ColorDiffuse.A;
                }

                var diffuseTexture = string.Empty;

                if (fbxMaterial.HasTextureDiffuse)
                {
                    diffuseTexture = ResolveTexturePath(context, fbxMaterial.TextureDiffuse.FilePath);

                    var texturePath = Path.GetFullPath(Path.Combine(context.InputDirectory, Path.GetDirectoryName(fbxMaterial.TextureDiffuse.FilePath))) + "/" + fbxMaterial.TextureDiffuse.FilePath.Replace('\\', '/');
                    var image = Surface.LoadFromFile(texturePath);
                    material.IsTransparent = image.IsTransparent || fbxMaterial.HasColorTransparent;
                }

                var normalTexture = string.Empty;

                if (fbxMaterial.HasTextureNormal)
                {
                    normalTexture = ResolveTexturePath(context, fbxMaterial.TextureNormal.FilePath);
                }

                var specularTexture = string.Empty;

                if (fbxMaterial.HasTextureSpecular)
                {
                    specularTexture = ResolveTexturePath(context, fbxMaterial.TextureSpecular.FilePath);
                }

                material.Properties.Add(new MaterialProperty("DiffuseColor", diffuseColor));
                material.Properties.Add(new MaterialProperty("DiffuseTexture", diffuseTexture));
                material.Properties.Add(new MaterialProperty("NormalTexture", normalTexture));
                material.Properties.Add(new MaterialProperty("BumpTexture", string.Empty));
                material.Properties.Add(new MaterialProperty("SpecularColor", new float[4] {0, 0, 0, 0}));
                material.Properties.Add(new MaterialProperty("SpecularTexture", specularTexture));

                materials.Add(material);
            }

            return materials.ToArray();
        }

        private string ResolveTexturePath(CompilerContext context, string texturePath)
        {
            if (context.OutputDirectory == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            texturePath = texturePath.Replace("\\", "/");
            
            var rootDirectory = context.RootOutputDirectory;
            var inputDirectory = Path.GetFullPath(Path.Combine(context.OutputDirectory, Path.GetDirectoryName(texturePath)));

            var outputTexturePath = $"{inputDirectory.Replace(rootDirectory, string.Empty)}/{Path.GetFileNameWithoutExtension(texturePath)}.texture";
            return outputTexturePath;
        }
    }
}