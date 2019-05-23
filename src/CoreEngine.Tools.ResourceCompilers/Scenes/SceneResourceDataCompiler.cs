using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using YamlDotNet;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class SceneResourceDataCompiler : ResourceDataCompiler
    {
        public SceneResourceDataCompiler(Logger logger) : base(logger)
        {

        }
        
        public override string Name
        {
            get
            {
                return "Scene Resource Data Compiler";
            }
        }

        public override string[] SupportedSourceExtensions
        {
            get
            {
                return new string[] { ".cescene" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".scene";
            }
        }

        public override Task<ReadOnlyMemory<byte>?> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            var version = 1;

            this.Logger.WriteMessage("Scene compiler", LogMessageType.Debug);

            var sceneDescription = ParseYamlFile(sourceData);
            this.Logger.WriteMessage($"Scene Entity Count: {sceneDescription.Entities.Count}", LogMessageType.Debug);

            var destinationMemoryStream = new MemoryStream();

            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            streamWriter.Write(new char[] { 'S', 'C', 'E', 'N', 'E'});
            streamWriter.Write(version);

            streamWriter.Write(sceneDescription.EntityLayouts.Count);
            streamWriter.Write(sceneDescription.Entities.Count);

            foreach (var entityLayout in sceneDescription.EntityLayouts)
            {
                streamWriter.Write(entityLayout.Types.Count);

                foreach (var type in entityLayout.Types)
                {
                    streamWriter.Write(type);
                }
            }

            foreach (var entity in sceneDescription.Entities)
            {
                streamWriter.Write(entity.EntityLayoutIndex);
                streamWriter.Write(entity.Components.Count);

                foreach (var component in entity.Components)
                {
                    streamWriter.Write(component.ComponentType);
                    streamWriter.Write(component.ComponentValues.Count);

                    foreach (var componentValue in component.ComponentValues)
                    {
                        streamWriter.Write(componentValue.Key);
                        streamWriter.Write(componentValue.Value.GetType().ToString());

                        if (componentValue.Value.GetType() == typeof(string))
                        {
                            streamWriter.Write((string)componentValue.Value);
                        }

                        else if (componentValue.Value.GetType() == typeof(float))
                        {
                            streamWriter.Write((float)componentValue.Value);
                        }

                        else if (componentValue.Value.GetType() == typeof(float[]))
                        {
                            var floatArray = (float[])componentValue.Value;

                            streamWriter.Write(floatArray.Length);

                            foreach (var floatValue in floatArray)
                            {
                                streamWriter.Write(floatValue);
                            }
                        }
                    }
                }
            }

            streamWriter.Flush();
            destinationMemoryStream.Flush();

            var result = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
            return Task.FromResult<ReadOnlyMemory<byte>?>(result);
        }

        private SceneDescription ParseYamlFile(ReadOnlyMemory<byte> sourceData)
        {
            var sceneDescription = new SceneDescription();
            var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            var yaml = new YamlStream();
            yaml.Load(reader);

            var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;

            foreach (var node in rootNode.Children)
            {
                if (((YamlScalarNode)node.Key).Value == "Entities")
                {
                    ReadEntities(sceneDescription, (YamlSequenceNode)node.Value);
                }
            }

            return sceneDescription;
        }

        private void ReadEntities(SceneDescription sceneDescription, YamlSequenceNode entities)
        {
            foreach (YamlMappingNode node in entities.Children)
            {
                var entityName = ((YamlScalarNode)node.Children.First(x => ((YamlScalarNode)x.Key).Value == "Entity").Value).Value;

                this.Logger.WriteMessage($"Entity: {entityName}", LogMessageType.Debug);

                var entityDescription = new EntityDescription(entityName);
                sceneDescription.Entities.Add(entityDescription);

                var entityLayoutDescription = new EntityLayoutDescription();

                foreach (var componentNode in node.Children.Where(x => ((YamlScalarNode)x.Key).Value == "Components").Select(x => x.Value))
                {
                    foreach (YamlMappingNode componentNodeElement in ((YamlSequenceNode)componentNode).Children)
                    {
                        var componentDescription = ReadComponent(entityDescription, componentNodeElement.Children.ToArray());
                     
                        if (componentDescription != null)
                        {
                            entityDescription.Components.Add(componentDescription);
                            entityLayoutDescription.Types.Add(componentDescription.ComponentType);
                        }
                    }
                }

                entityDescription.EntityLayoutIndex = sceneDescription.AddEntityLayoutDescription(entityLayoutDescription);
            }
        }

        private ComponentDescription? ReadComponent(EntityDescription entityDescription, KeyValuePair<YamlNode, YamlNode>[] componentsData)
        {
            ComponentDescription? componentDescription = null;

            foreach (var node in componentsData)
            {
                var nodeKey = ((YamlScalarNode)node.Key).Value;
                this.Logger.WriteMessage($"{node.Key} - {node.Value} ({node.Value.NodeType})", LogMessageType.Debug);

                if (nodeKey == "Component")
                {
                    componentDescription = new ComponentDescription(((YamlScalarNode)node.Value).Value);
                }

                else
                {
                    if (node.Value.NodeType == YamlNodeType.Scalar)
                    {
                        var scalarNode = (YamlScalarNode)node.Value;

                        this.Logger.WriteMessage($"Scalar node style: {scalarNode.Style}");

                        if (scalarNode.Style == ScalarStyle.Plain)
                        {
                            var value = float.Parse(scalarNode.Value, CultureInfo.InvariantCulture);

                            if (componentDescription != null)
                            {
                                componentDescription.ComponentValues.Add(nodeKey, value);
                            }
                        }

                        else if (scalarNode.Style == ScalarStyle.SingleQuoted)
                        {
                            if (componentDescription != null)
                            {
                                componentDescription.ComponentValues.Add(nodeKey, scalarNode.Value);
                            }
                        }
                    }

                    else if (node.Value.NodeType == YamlNodeType.Sequence)
                    {
                        var sequenceNode = (YamlSequenceNode)node.Value;
                        var arrayValue = sequenceNode.Select(x => float.Parse(((YamlScalarNode)x).Value, CultureInfo.InvariantCulture)).ToArray();
                    
                        if (componentDescription != null)
                        {
                            componentDescription.ComponentValues.Add(nodeKey, arrayValue);
                        }
                    }

                    else
                    {
                        this.Logger.WriteMessage("Warning: Unsupported yaml node type.", LogMessageType.Warning);
                    }
                }
            }

            return componentDescription;
        }
    }
}