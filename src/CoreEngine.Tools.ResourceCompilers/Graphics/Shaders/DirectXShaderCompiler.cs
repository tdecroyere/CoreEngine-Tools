using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Shaders
{
    public static class DirectXShaderCompiler
    {
        public static async Task<ReadOnlyMemory<byte>?> CompileDirectXShaderAsync(ReadOnlyMemory<byte> data)
        {
            Logger.WriteMessage("Compiling DirectX shader with command line tools");

            string? windowsSdkToolPath = null;

            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots");
            
            if (key != null)
            {
                var windowsSdkPath = key.GetValue("KitsRoot10")?.ToString();
                var windowsSdkVersion = key.GetSubKeyNames().OrderByDescending(x => x).FirstOrDefault();

                windowsSdkToolPath = $@"{windowsSdkPath}bin\{windowsSdkVersion}\x64\";
            }

            if (windowsSdkToolPath == null)
            {
                return null;
            }

            // TODO: Find a way to invoke compilation in-memory
            // TODO: Put intermediate files into temp directory
            // TODO: Remove intermediate files
            // TODO: Check debug profile

            var entryPoints = new List<string>();
            var shaderContent = System.Text.Encoding.UTF8.GetString(data.ToArray());

            var regex = new Regex(@"(VertexMain|PixelMain|AmplificationMain|MeshMain|ComputeMain|\[numthreads\(.*void\s(?<entryPoint>[^\(]*)\()", RegexOptions.Singleline);
            var matches = regex.Matches(shaderContent);

            foreach (Match match in matches)
            {
                if (!string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    entryPoints.Add(match.Groups[2].Value);
                }

                else
                {
                    entryPoints.Add(match.Groups[0].Value);
                }
            }

            var rootParameterRegex = new Regex(@"(?:RootSignatureDefinition|RootSignatureDefinitionWithSampler)\(([0-9]+)", RegexOptions.Singleline);
            var rootParameterMatch = rootParameterRegex.Match(shaderContent);
            var parameterCount = 0;

            if (rootParameterMatch.Success)
            {
                parameterCount = int.Parse(rootParameterMatch.Groups[1].Value);
                Logger.WriteMessage($"Parameter Count: {rootParameterMatch.Groups[1].Value}");
            }
            // TODO: Parse local thread count

            var tempFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
            var inputShaderFile = Path.Combine(tempFolder, "tempShader.hlsl");
            var outputShaderFile = Path.Combine(tempFolder, "tempShader.cso");

            await File.WriteAllBytesAsync(inputShaderFile, data.ToArray());

            var shaderTable = new Dictionary<string, byte[]>();
            var sprivShaderTable = new Dictionary<string, byte[]>();

            foreach (var entryPoint in entryPoints)
            {
                var shaderData = await CompileShaderEntryPoint(entryPoint, inputShaderFile, outputShaderFile, isSpirv: false);
                shaderTable.Add(entryPoint, shaderData);

                shaderData = await CompileShaderEntryPoint(entryPoint, inputShaderFile, outputShaderFile, isSpirv: true);

                if (shaderData != null)
                {
                    sprivShaderTable.Add(entryPoint, shaderData);
                }

                else
                {
                    sprivShaderTable.Add(entryPoint, Array.Empty<byte>());
                }
            }

            using var buildProcess = new Process();
            buildProcess.StartInfo.FileName = $".\\dxc\\dxc.exe";
            buildProcess.StartInfo.Arguments = $"{inputShaderFile} -all-resources-bound -I ..\\..\\TestData\\System\\Shaders\\Lib\\ -T rootsig_1_1 -E RootSignatureDef -Fo {outputShaderFile}";

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }

            var rootSignatureData = await File.ReadAllBytesAsync(outputShaderFile);

            var dxilOutput = WriteShaderTable(shaderTable, rootSignatureData, parameterCount);
            var sprivOutput = WriteShaderTable(sprivShaderTable, null, parameterCount);

            var destinationMemoryStream = new MemoryStream();
            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            
            streamWriter.Write(dxilOutput.Length);
            streamWriter.Write(dxilOutput);
            streamWriter.Write(sprivOutput);

            destinationMemoryStream.Flush();
            return new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
        }

        private static Task<byte[]?> CompileShaderEntryPoint(string entryPoint, string inputShaderFile, string outputShaderFile, bool isSpirv)
        {
            // TODO: Use the DXC lib instead of the exe
            
            var target = "cs_6_6";

            if (entryPoint == "VertexMain")
            {
                target = "vs_6_6";
            }

            else if (entryPoint == "PixelMain")
            {
                target = "ps_6_6";
            }

            else if (entryPoint == "AmplificationMain")
            {
                target = "as_6_6";
            }

            else if (entryPoint == "MeshMain")
            {
                target = "ms_6_6";
            }

            Logger.WriteMessage($"Compiling entry point: {entryPoint} {target} (OutputFormat: {(isSpirv ? "SPIR-V" : "DXIL")})");
            
            using var buildProcess = new Process();
            buildProcess.StartInfo.FileName = $".\\dxc\\dxc.exe";

            if (!isSpirv)
            {
                buildProcess.StartInfo.Arguments = $"{inputShaderFile} -Zpr -all-resources-bound -Wno-ignored-attributes -I ..\\..\\TestData\\System\\Shaders\\Lib\\ -T {target} -E {entryPoint} -Fo {outputShaderFile}";
            }

            else
            {
                buildProcess.StartInfo.Arguments = $"{inputShaderFile} -spirv -D VULKAN -Zpr -fspv-target-env=vulkan1.1 -fvk-use-dx-layout -all-resources-bound -I ..\\..\\TestData\\System\\Shaders\\Lib\\ -T {target} -E {entryPoint} -Fo {outputShaderFile}";
            }

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return Task.FromResult<byte[]?>(null);
            }            

            return File.ReadAllBytesAsync(outputShaderFile);
        }

        private static byte[] WriteShaderTable(Dictionary<string, byte[]> shaderTable, byte[]? rootSignatureData, int parameterCount)
        {
            var destinationMemoryStream = new MemoryStream();
            using var streamWriter = new BinaryWriter(destinationMemoryStream);

            streamWriter.Write(parameterCount);

            if (rootSignatureData != null)
            {
                streamWriter.Write(rootSignatureData.Length); // TODO: Use span overload?
                streamWriter.Write(rootSignatureData);
            }

            streamWriter.Write(shaderTable.Keys.Count);

            foreach (var shaderEntry in shaderTable)
            {
                streamWriter.Write(shaderEntry.Key.Length);
                streamWriter.Write(shaderEntry.Key.ToCharArray());
                streamWriter.Write(shaderEntry.Value.Length);
                streamWriter.Write(shaderEntry.Value);
            }

            streamWriter.Flush();
            destinationMemoryStream.Flush();
            return destinationMemoryStream.ToArray();
        }
    }
}