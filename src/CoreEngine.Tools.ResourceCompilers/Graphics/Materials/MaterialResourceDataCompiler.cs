using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using YamlDotNet;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Materials
{
    readonly struct TextureEntry
    {
        public TextureEntry(int offset, string resourcePath)
        {
            this.Offset = offset;
            this.ResourcePath = resourcePath;
        }

        public readonly int Offset { get; }
        public readonly string ResourcePath { get; }
    }

    public class MaterialResourceDataCompiler : ResourceDataCompiler
    {
        public MaterialResourceDataCompiler()
        {

        }
        
        public override string Name
        {
            get
            {
                return "Material Resource Data Compiler";
            }
        }

        public override IList<string> SupportedSourceExtensions
        {
            get
            {
                return new string[] { ".cematerial", ".mtl" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".material";
            }
        }

        public override Task<ReadOnlyMemory<ResourceEntry>> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var version = 1;

            IMaterialDataReader materialDataReader;

            if (Path.GetExtension(context.SourceFilename) == ".mtl")
            {
                materialDataReader = new ObjMaterialDataReader();
            }

            else
            {
                materialDataReader = new CoreEngineMaterialDataReader(Path.GetFileNameWithoutExtension(context.SourceFilename));
            }

            var materials = materialDataReader.Read(sourceData.Span, context);
            Logger.WriteMessage($"Materials Count: {materials.Length}");

            var resourceEntries = new ResourceEntry[materials.Length];

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                
                Logger.WriteMessage($"Material Property Count: {material.Properties.Count}", LogMessageTypes.Debug);

                var destinationMemoryStream = new MemoryStream();

                using var streamWriter = new BinaryWriter(destinationMemoryStream);
                streamWriter.Write(new char[] { 'M', 'A', 'T', 'E', 'R', 'I', 'A', 'L'});
                streamWriter.Write(version);

                Logger.BeginAction("Writing Material data");
                var textureResourceList = new List<TextureEntry>();
                
                var materialDataMemoryStream = new MemoryStream();
                using var materialDataStreamWriter = new BinaryWriter(materialDataMemoryStream);

                foreach (var property in material.Properties)
                {
                    if (property.Value.GetType() == typeof(string))
                    {
                        var stringValue = (string)property.Value;
                        
                        if (!string.IsNullOrEmpty(stringValue))
                        {
                            textureResourceList.Add(new TextureEntry((int)materialDataStreamWriter.BaseStream.Position, stringValue));
                        }

                        materialDataStreamWriter.Write(-1);
                    }

                    else if (property.Value.GetType() == typeof(bool))
                    {
                        materialDataStreamWriter.Write((bool)property.Value);
                    }

                    else if (property.Value.GetType() == typeof(float))
                    {
                        materialDataStreamWriter.Write((float)property.Value);
                    }

                    else if (property.Value.GetType() == typeof(float[]))
                    {
                        var floatArray = (float[])property.Value;

                        foreach (var floatValue in floatArray)
                        {
                            materialDataStreamWriter.Write(floatValue);
                        }
                    }
                }

                var materialData = materialDataMemoryStream.ToArray();
                
                streamWriter.Write(textureResourceList.Count);
                
                for (var j = 0; j < textureResourceList.Count; j++)
                {
                    var textureResource = textureResourceList[j];

                    streamWriter.Write(textureResource.Offset);
                    streamWriter.Write(textureResource.ResourcePath);

                    var textureIndex = j + 1;

                    MemoryMarshal.Write(materialData.AsSpan().Slice(textureResource.Offset), ref textureIndex);
                }

                streamWriter.Write(materialData.Length);
                streamWriter.Write(materialData);

                Logger.EndAction();
                
                streamWriter.Flush();
                destinationMemoryStream.Flush();

                var resourceData = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
                var resourceEntry = new ResourceEntry($"{Path.GetFileNameWithoutExtension(material.Name)}{this.DestinationExtension}", resourceData);

                resourceEntries[i] = resourceEntry;
            }

            return Task.FromResult<ReadOnlyMemory<ResourceEntry>>(resourceEntries);
        }
    }
}