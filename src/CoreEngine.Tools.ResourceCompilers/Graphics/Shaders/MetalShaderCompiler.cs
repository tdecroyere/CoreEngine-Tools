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

        public async Task<byte[]?> CompileMetalShaderAsync(ReadOnlyMemory<byte> data)
        {
            this.logger.WriteMessage("Compiling metal shader with command line tools");

            var transpiledMetalShader = await TranspileShaderToMetalAsync(data);
            var metalShaderData = await CompileMetalShaderSourceAsync(transpiledMetalShader);

            return metalShaderData;
        }

        private static async Task<byte[]?> TranspileShaderToMetalAsync(ReadOnlyMemory<byte> data)
        {
            // TODO: Add parameters for vertex and pixel main
            // TODO: Use shader conductor lib instead of command line tool

            var tempFolder = ".";
            var inputShaderFile = Path.Combine(tempFolder, "tempShader_transpile.hlsl");
            var outputShaderFile = Path.Combine(tempFolder, "tempShader_transpile.metal");

            await File.WriteAllBytesAsync(inputShaderFile, data.ToArray());

            var buildProcess = new Process();
            buildProcess.StartInfo.FileName = "ShaderConductorCmd";
            buildProcess.StartInfo.Arguments = $"-I {inputShaderFile} -O {outputShaderFile} -S vs -T msl_macos -E VertexMain";

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }

            var vertexShaderData = await File.ReadAllBytesAsync(outputShaderFile);

            buildProcess.StartInfo.Arguments = $"-I {inputShaderFile} -O {outputShaderFile} -S ps -T msl_macos -E PixelMain";

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }

            var pixelShaderData = await File.ReadAllBytesAsync(outputShaderFile);

            var outputArray = new byte[vertexShaderData.Length + pixelShaderData.Length];
            Array.Copy(vertexShaderData, outputArray, vertexShaderData.Length);
            Array.Copy(pixelShaderData, 0, outputArray, vertexShaderData.Length, pixelShaderData.Length);

            return outputArray;
        }

        private static async Task<byte[]?> CompileMetalShaderSourceAsync(ReadOnlyMemory<byte> data)
        {
            // TODO: Find a way to invoke compilation in-memory
            // TODO: Put intermediate files into temp directory
            // TODO: Remove intermediate files

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

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }            

            buildProcess.StartInfo.Arguments = $"-sdk macosx metallib {outputAirFile} -o {outputMetalLibFile}";

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            } 

            return await File.ReadAllBytesAsync(outputMetalLibFile);
        }
    }
}