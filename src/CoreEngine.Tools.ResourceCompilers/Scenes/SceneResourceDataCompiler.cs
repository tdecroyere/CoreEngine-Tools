using System;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
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

            // TODO: Try to avoid the ToArray call that copy the buffer to the MemoryStream
            using var reader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            var yaml = new YamlStream();
            yaml.Load(reader);

            // Examine the stream
            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

            foreach (var entry in mapping.Children)
            {
                this.Logger.WriteMessage($"{entry.Key} - {entry.Value}");

                var sequence = (YamlSequenceNode)entry.Value;
                this.Logger.WriteMessage($"{sequence.Children.Count}");
            }

            // this.Logger.WriteMessage($"Scene Entity Count: {sceneDescription.Entities.Count}");

            return null;
        }
    }
}