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
        public SceneResourceDataCompiler()
        {

        }
        
        public override string Name
        {
            get
            {
                return "Scene Resource Data Compiler";
            }
        }

        public override IList<string> SupportedSourceExtensions
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

        public override Task<ReadOnlyMemory<ResourceEntry>> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var version = 1;

            var sceneDescription = ParseYamlFile(sourceData);
            Logger.WriteMessage($"Scene Entity Count: {sceneDescription.Entities.Count}", LogMessageTypes.Debug);

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

            Logger.BeginAction("Writing Scene data");
            
            foreach (var entity in sceneDescription.Entities)
            {
                streamWriter.Write(entity.Name);
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

                        else if (componentValue.Value.GetType() == typeof(bool))
                        {
                            streamWriter.Write((bool)componentValue.Value);
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

            Logger.EndAction();

            streamWriter.Flush();
            destinationMemoryStream.Flush();

            var resourceData = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
            var resourceEntry = new ResourceEntry($"{Path.GetFileNameWithoutExtension(context.SourceFilename)}{this.DestinationExtension}", resourceData);

            return Task.FromResult(new ReadOnlyMemory<ResourceEntry>(new ResourceEntry[] { resourceEntry }));
        }

        private SceneDescription ParseYamlFile(ReadOnlyMemory<byte> sourceData)
        {
            var sceneDescription = new SceneDescription();
            using var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
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

        private static void ReadEntities(SceneDescription sceneDescription, YamlSequenceNode entities)
        {
            foreach (YamlMappingNode node in entities.Children)
            {
                var entityName = ((YamlScalarNode)node.Children.First(x => ((YamlScalarNode)x.Key).Value == "Entity").Value).Value;

                Logger.BeginAction($"Reading Entity: {entityName}");

                var entityDescription = new EntityDescription(entityName);
                sceneDescription.Entities.Add(entityDescription);

                var entityLayoutDescription = new EntityLayoutDescription();

                foreach (var componentNode in node.Children.Where(x => ((YamlScalarNode)x.Key).Value == "Components").Select(x => x.Value))
                {
                    foreach (YamlMappingNode componentNodeElement in ((YamlSequenceNode)componentNode).Children)
                    {
                        var componentDescription = ReadComponent(componentNodeElement.Children.ToArray());
                     
                        if (componentDescription != null)
                        {
                            entityDescription.Components.Add(componentDescription);
                            entityLayoutDescription.Types.Add(componentDescription.ComponentType);
                        }
                    }
                }

                Logger.EndAction();

                entityDescription.EntityLayoutIndex = sceneDescription.AddEntityLayoutDescription(entityLayoutDescription);
            }
        }

        private static ComponentDescription? ReadComponent(KeyValuePair<YamlNode, YamlNode>[] componentsData)
        {
            ComponentDescription? componentDescription = null;

            foreach (var node in componentsData)
            {
                var nodeKey = ((YamlScalarNode)node.Key).Value;
                Logger.WriteMessage($"{node.Key} - {node.Value} ({node.Value.NodeType})", LogMessageTypes.Debug);

                if (nodeKey == "Component")
                {
                    componentDescription = new ComponentDescription(((YamlScalarNode)node.Value).Value);
                }

                else
                {
                    if (node.Value.NodeType == YamlNodeType.Scalar)
                    {
                        var scalarNode = (YamlScalarNode)node.Value;

                        Logger.WriteMessage($"Scalar node style: {scalarNode.Style}");

                        if (scalarNode.Style == ScalarStyle.Plain)
                        {
                            if (scalarNode.Value == "true" || scalarNode.Value == "false")
                            {
                                var value = bool.Parse(scalarNode.Value);

                                if (componentDescription != null)
                                {
                                    componentDescription.ComponentValues.Add(nodeKey, value);
                                }
                            }

                            else
                            {
                                var value = float.Parse(scalarNode.Value, CultureInfo.InvariantCulture);

                                if (componentDescription != null)
                                {
                                    componentDescription.ComponentValues.Add(nodeKey, value);
                                }
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
                        Logger.WriteMessage("Warning: Unsupported yaml node type.", LogMessageTypes.Warning);
                    }
                }
            }

            return componentDescription;
        }
    }
}