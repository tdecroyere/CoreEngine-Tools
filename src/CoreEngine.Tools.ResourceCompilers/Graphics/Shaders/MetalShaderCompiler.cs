using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Shaders
{
    public class MetalShaderCompiler
    {
        private readonly Logger logger;

        public MetalShaderCompiler(Logger logger)
        {
            this.logger = logger;
        }

        public async Task<byte[]> CompileMetalShaderAsync(ReadOnlyMemory<byte> data)
        {
            this.logger.WriteMessage("Compiling metal shader with command line tools");

            // TODO: Find a way to invoke compilation in-memory
            // TODO: Put intermediate files into temp directory
            // TODO: Remove intermediate files
            // TODO: Process errors

            var tempFolder = ".";
            var inputShaderFile = Path.Combine(tempFolder, "tempShader.metal");
            var outputAirFile = Path.Combine(tempFolder, "tempShader.air");
            var outputMetalLibFile = Path.Combine(tempFolder, "tempShader.metallib");

            await File.WriteAllBytesAsync(inputShaderFile, data.ToArray());

            var buildProcess = new Process();
            buildProcess.StartInfo.FileName = "xcrun";
            buildProcess.StartInfo.Arguments = $"-sdk macosx metal -ffast-math -gline-tables-only -MO -c {inputShaderFile} -o {outputAirFile}";

            buildProcess.Start();
            buildProcess.WaitForExit();

            buildProcess.StartInfo.Arguments = $"-sdk macosx metallib {outputAirFile} -o {outputMetalLibFile}";

            buildProcess.Start();
            buildProcess.WaitForExit();

            var metalShaderData = await File.ReadAllBytesAsync(outputMetalLibFile);
            return metalShaderData;
        } 
    }
}