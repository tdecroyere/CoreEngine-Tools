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
                return new string[] { ".cematerial" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".material";
            }
        }

        public override Task<ReadOnlyMemory<byte>?> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var version = 1;

            var material = ParseYamlFile(sourceData);
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
                    
                    if (stringValue != string.Empty)
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
            
            for (var i = 0; i < textureResourceList.Count; i++)
            {
                var textureResource = textureResourceList[i];

                streamWriter.Write(textureResource.Offset);
                streamWriter.Write(textureResource.ResourcePath);

                var textureIndex = i + 1;

                MemoryMarshal.Write(materialData.AsSpan().Slice(textureResource.Offset), ref textureIndex);
            }

            streamWriter.Write(materialData.Length);
            streamWriter.Write(materialData);

            Logger.EndAction();
            
            streamWriter.Flush();
            destinationMemoryStream.Flush();

            var result = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
            return Task.FromResult<ReadOnlyMemory<byte>?>(result);
        }

        private MaterialDescription ParseYamlFile(ReadOnlyMemory<byte> sourceData)
        {
            var materialDescription = new MaterialDescription();
            using var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            var yaml = new YamlStream();
            yaml.Load(reader);

            var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;

            foreach (var node in rootNode.Children)
            {
                if (((YamlScalarNode)node.Key).Value == "Material")
                {
                    ReadProperties(materialDescription, ((YamlSequenceNode)node.Value).Children.ToArray());
                }
            }

            return materialDescription;
        }

        private static void ReadProperties(MaterialDescription materialDescription, YamlNode[] materialData)
        {
            foreach (var rootNode in materialData)
            {
                var children = ((YamlMappingNode)rootNode).Children.ToArray();

                foreach (var node in children)
                {
                    var nodeKey = ((YamlScalarNode)node.Key).Value;
                    Logger.WriteMessage($"{node.Key} - {node.Value} ({node.Value.NodeType})", LogMessageTypes.Debug);

                    if (node.Value.NodeType == YamlNodeType.Scalar)
                    {
                        var scalarNode = (YamlScalarNode)node.Value;

                        Logger.WriteMessage($"Scalar node style: {scalarNode.Style}");

                        if (scalarNode.Style == ScalarStyle.Plain)
                        {
                            if (scalarNode.Value == "true" || scalarNode.Value == "false")
                            {
                                var value = bool.Parse(scalarNode.Value);
                                materialDescription.Properties.Add(new MaterialProperty(nodeKey.ToString(), value));
                            }

                            else
                            {
                                var value = float.Parse(scalarNode.Value, CultureInfo.InvariantCulture);
                                materialDescription.Properties.Add(new MaterialProperty(nodeKey.ToString(), value));
                            }
                        }

                        else if (scalarNode.Style == ScalarStyle.SingleQuoted)
                        {
                            materialDescription.Properties.Add(new MaterialProperty(nodeKey.ToString(), scalarNode.Value));
                        }
                    }

                    else if (node.Value.NodeType == YamlNodeType.Sequence)
                    {
                        var sequenceNode = (YamlSequenceNode)node.Value;
                        var arrayValue = sequenceNode.Select(x => float.Parse(((YamlScalarNode)x).Value, CultureInfo.InvariantCulture)).ToArray();
                        
                        materialDescription.Properties.Add(new MaterialProperty(nodeKey.ToString(), arrayValue));
                    }

                    else
                    {
                        Logger.WriteMessage("Warning: Unsupported yaml node type.", LogMessageTypes.Warning);
                    }
                }
            }
        }
    }
}