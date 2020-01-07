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
    public class CoreEngineMaterialDataReader : IMaterialDataReader
    {
        private string filename;

        public CoreEngineMaterialDataReader(string filename)
        {
            this.filename = filename;
        }
   
        public Span<MaterialDescription> Read(ReadOnlySpan<byte> sourceData, CompilerContext context)
        {
            var materialDescription = new MaterialDescription(this.filename);
            
            using var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            var yaml = new YamlStream();
            yaml.Load(reader);

            var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;

            foreach (var node in rootNode.Children)
            {
                if (((YamlScalarNode)node.Key).Value == "Material")
                {
                    foreach (var subNode in ((YamlMappingNode)node.Value).Children)
                    {
                        if (((YamlScalarNode)subNode.Key).Value == "Properties")
                        {
                            ReadProperties(materialDescription, ((YamlSequenceNode)subNode.Value).Children.ToArray());
                        }

                        else if (subNode.Key.ToString() == "IsTransparent")
                        {
                            materialDescription.IsTransparent = bool.Parse(((YamlScalarNode)subNode.Value).Value);
                        }
                    }
                }
            }

            return new MaterialDescription[] { materialDescription };
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