using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using YamlDotNet;
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

        public override async Task<ReadOnlyMemory<byte>?> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            var version = 1;

            this.Logger.WriteMessage("Scene compiler");

            var sceneDescription = new SceneDescription();

            // TODO: Try to avoid the ToArray call that copy the buffer to the MemoryStream
            using var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            var yaml = new YamlStream();
            yaml.Load(reader);

            var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;

            foreach (var node in rootNode.Children)
            {
                this.Logger.WriteMessage($"{node.Key} - {node.Value.NodeType}");

                if (node.Key.ToString() == "Entities")
                {
                    ReadEntities(sceneDescription, (YamlSequenceNode)node.Value);
                }
            }

            // this.Logger.WriteMessage($"Scene Entity Count: {sceneDescription.Entities.Count}");

            return null;
        }

        private void ReadEntities(SceneDescription sceneDescription, YamlSequenceNode entities)
        {
            foreach (YamlMappingNode node in entities.Children)
            {
                var entityName = ((YamlScalarNode)node.Children.First(x => ((YamlScalarNode)x.Key).Value == "Entity").Value).Value;

                this.Logger.WriteMessage($"Entity: {entityName}");

                var entityDescription = new SceneEntityDescription(entityName);

                foreach (var componentNode in node.Children.Where(x => ((YamlScalarNode)x.Key).Value == "Components").Select(x => x.Value))
                {
                    this.Logger.WriteMessage($"{componentNode.NodeType}");
                    //this.Logger.WriteMessage($"{((YamlScalarNode)componentNode.Key).Value}");
                }
            }
        }
    }
}