using System;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Shaders
{
    public class ShaderResourceDataCompiler : ResourceDataCompiler
    {
        public ShaderResourceDataCompiler(Logger logger) : base(logger)
        {

        }
        
        public override string Name
        {
            get
            {
                return "Shader Resource Data Compiler";
            }
        }

        public override string[] SupportedSourceExtensions
        {
            get
            {
                return new string[] { ".hlsl" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".shader";
            }
        }

        public override async Task<ReadOnlyMemory<byte>?> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            var version = 1;

            this.Logger.WriteMessage($"Shader compiler platform: {context.TargetPlatform}");

            // TODO: Add Platform checks, for the moment only compiling metal shaders

            if (context.TargetPlatform == "osx")
            {
                var metalShaderCompiler = new MetalShaderCompiler(Logger);
                var shaderCompiledData = await metalShaderCompiler.CompileMetalShaderAsync(sourceData);

                if (shaderCompiledData != null)
                {
                    var destinationMemoryStream = new MemoryStream();

                    using var streamWriter = new BinaryWriter(destinationMemoryStream);
                    streamWriter.Write(new char[] { 'S', 'H', 'A', 'D', 'E', 'R'});
                    streamWriter.Write(version);
                    streamWriter.Write(shaderCompiledData.Value.Length);
                    streamWriter.Write(shaderCompiledData.Value.ToArray());
                    streamWriter.Flush();

                    destinationMemoryStream.Flush();
                    return new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
                }
            }

            return null;
        }
    }
}