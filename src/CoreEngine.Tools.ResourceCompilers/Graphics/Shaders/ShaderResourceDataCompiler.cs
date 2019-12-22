using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Shaders
{
    public class ShaderResourceDataCompiler : ResourceDataCompiler
    {
        public override string Name
        {
            get
            {
                return "Shader Resource Data Compiler";
            }
        }

        public override IList<string> SupportedSourceExtensions
        {
            get
            {
                return new string[] { ".hlsl", ".metal" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".shader";
            }
        }

        public override async Task<ReadOnlyMemory<ResourceEntry>> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var version = 1;

            Logger.WriteMessage($"Shader compiler platform: {context.TargetPlatform}");
            ReadOnlyMemory<byte>? shaderCompiledData = null;

            if (context.TargetPlatform == "osx")
            {
                shaderCompiledData = await MetalShaderCompiler.CompileMetalShaderAsync(sourceData, Path.GetExtension(context.SourceFilename) != ".metal");
            }

            else if (context.TargetPlatform == "windows")
            {
                shaderCompiledData = await DirectXShaderCompiler.CompileDirectXShaderAsync(sourceData);
            }
            
            if (shaderCompiledData != null)
            {
                var destinationMemoryStream = new MemoryStream();

                using var streamWriter = new BinaryWriter(destinationMemoryStream);
                streamWriter.Write(new char[] { 'S', 'H', 'A', 'D', 'E', 'R'});
                streamWriter.Write(version);
                streamWriter.Write(shaderCompiledData.Value.Length); // TODO: Use span overload?
                streamWriter.Write(shaderCompiledData.Value.ToArray());
                streamWriter.Flush();

                destinationMemoryStream.Flush();
                var resourceData = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
                var resourceEntry = new ResourceEntry($"{Path.GetFileNameWithoutExtension(context.SourceFilename)}{this.DestinationExtension}", resourceData);

                return new ReadOnlyMemory<ResourceEntry>(new ResourceEntry[] { resourceEntry });
            }

            return null;
        }
    }
}