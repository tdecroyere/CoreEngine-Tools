using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Shaders
{
    public static class MetalShaderCompiler
    {
        public static async Task<ReadOnlyMemory<byte>?> CompileMetalShaderAsync(ReadOnlyMemory<byte> data, bool transpileShader, string includeDirectory)
        {
            if (transpileShader)
            {
                var transpiledMetalShader = await TranspileShaderToMetalAsync(data);

                if (transpiledMetalShader != null)
                {
                    return await CompileMetalShaderSourceAsync(transpiledMetalShader.Value, includeDirectory);
                }
            }

            else
            {
                return await CompileMetalShaderSourceAsync(data, includeDirectory);
            }

            return null;
        }

        private static async Task<ReadOnlyMemory<byte>?> TranspileShaderToMetalAsync(ReadOnlyMemory<byte> data)
        {
            // TODO: Add parameters for vertex and pixel main
            // TODO: Use shader conductor lib instead of command line tool

            var tempFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location), "temp")!;

            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }

            var inputShaderFile = Path.Combine(tempFolder, "tempShader_transpile.hlsl");
            var vsOutputShaderFile = Path.Combine(tempFolder, "vs_tempShader_transpile.metal");
            var psOutputShaderFile = Path.Combine(tempFolder, "ps_tempShader_transpile.metal");

            await File.WriteAllBytesAsync(inputShaderFile, data.ToArray());

            using var buildProcess = new Process();
            buildProcess.StartInfo.FileName = "ShaderConductorCmd";
            buildProcess.StartInfo.Arguments = $"-I {inputShaderFile} -O {vsOutputShaderFile} -S vs -T msl_macos -V 20200 -E VertexMain";

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }

            var vertexShaderData = await File.ReadAllBytesAsync(vsOutputShaderFile);

            buildProcess.StartInfo.Arguments = $"-I {inputShaderFile} -O {psOutputShaderFile} -S ps -T msl_macos -V 20200 -E PixelMain";

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }

            var pixelShaderData = await File.ReadAllBytesAsync(psOutputShaderFile);

            var outputArray = new byte[vertexShaderData.Length + pixelShaderData.Length];
            Array.Copy(vertexShaderData, outputArray, vertexShaderData.Length);
            Array.Copy(pixelShaderData, 0, outputArray, vertexShaderData.Length, pixelShaderData.Length);

            return outputArray;
        }

        private static async Task<ReadOnlyMemory<byte>?> CompileMetalShaderSourceAsync(ReadOnlyMemory<byte> data, string includeDirectory)
        {
            // TODO: Find a way to invoke compilation in-memory
            // TODO: Put intermediate files into temp directory
            // TODO: Remove intermediate files

            var tempFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location), "temp")!;
            
            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }

            var inputShaderFile = Path.Combine(tempFolder, "tempShader.metal");
            var outputAirFile = Path.Combine(tempFolder, "tempShader.air");
            var outputMetalLibFile = Path.Combine(tempFolder, "tempShader.metallib");

            await File.WriteAllBytesAsync(inputShaderFile, data.ToArray());

            using var buildProcess = new Process();
            buildProcess.StartInfo.FileName = "xcrun";
            buildProcess.StartInfo.Arguments = $"-sdk macosx metal -gline-tables-only -MO -I {includeDirectory} -c {inputShaderFile} -o {outputAirFile}";

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