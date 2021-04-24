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
            var useDxil = true;

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

            var regex = new Regex(@"(VertexMain|PixelMain|\[numthreads\(.*void\s(?<entryPoint>[^\(]*)\()", RegexOptions.Singleline);
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

            foreach (var entryPoint in entryPoints)
            {
            }

            var tempFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
            var inputShaderFile = Path.Combine(tempFolder, "tempShader.hlsl");
            var outputShaderFile = Path.Combine(tempFolder, "tempShader.cso");

            await File.WriteAllBytesAsync(inputShaderFile, data.ToArray());
            using var buildProcess = new Process();

            var shaderTable = new Dictionary<string, byte[]>();

            foreach (var entryPoint in entryPoints)
            {
                var target = "cs_6_6";

                if (entryPoint == "VertexMain")
                {
                    target = "vs_6_6";
                }

                else if (entryPoint == "PixelMain")
                {
                    target = "ps_6_6";
                }

                Logger.WriteMessage($"Compiling entry point: {entryPoint} {target}");
                
                if (useDxil)
                {
                    buildProcess.StartInfo.FileName = $".\\dxc\\dxc.exe";
                    buildProcess.StartInfo.Arguments = $"{inputShaderFile} /Zi -T {target} -E {entryPoint} -Fo {outputShaderFile}";
                }

                else
                {
                    buildProcess.StartInfo.FileName = $"{windowsSdkToolPath}fxc.exe";
                    buildProcess.StartInfo.Arguments = $"{inputShaderFile} /nologo /Zi /T {target} /E {entryPoint} /Fo {outputShaderFile}";
                }

                buildProcess.Start();
                buildProcess.WaitForExit();

                if (buildProcess.ExitCode != 0)
                {
                    return null;
                }            

                var shaderData = await File.ReadAllBytesAsync(outputShaderFile);
                shaderTable.Add(entryPoint, shaderData);
            }

            if (useDxil)
            {
                buildProcess.StartInfo.Arguments = $"{inputShaderFile} -T rootsig_1_1 -E RootSignatureDef -Fo {outputShaderFile}";
            }

            else
            {
                buildProcess.StartInfo.Arguments = $"{inputShaderFile} /nologo /T rootsig_1_1 /E RootSignatureDef /Fo {outputShaderFile}";
            }

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }

            var rootSignatureData = await File.ReadAllBytesAsync(outputShaderFile);

            var destinationMemoryStream = new MemoryStream();
            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            streamWriter.Write(rootSignatureData.Length); // TODO: Use span overload?
            streamWriter.Write(rootSignatureData);

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
            return new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
        }
    }
}