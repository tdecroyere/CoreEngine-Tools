using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using Microsoft.Win32;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Shaders
{
    public class DirectXShaderCompiler
    {
        private readonly Logger logger;

        public DirectXShaderCompiler(Logger logger)
        {
            this.logger = logger;
        }

        public async Task<ReadOnlyMemory<byte>?> CompileDirectXShaderAsync(ReadOnlyMemory<byte> data)
        {
            this.logger.WriteMessage("Compiling DirectX shader with command line tools");

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

            var tempFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var inputShaderFile = Path.Combine(tempFolder, "tempShader.hlsl");
            var vsOutputShaderFile = Path.Combine(tempFolder, "vs_tempShader.cso");
            var psOutputShaderFile = Path.Combine(tempFolder, "ps_tempShader.cso");

            await File.WriteAllBytesAsync(inputShaderFile, data.ToArray());

            var buildProcess = new Process();
            buildProcess.StartInfo.FileName = $"{windowsSdkToolPath}dxc.exe";
            buildProcess.StartInfo.Arguments = $"{inputShaderFile} -T vs_6_0 -E VertexMain -Fo {vsOutputShaderFile}";

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }            

            var vertexShaderData = await File.ReadAllBytesAsync(vsOutputShaderFile);

            buildProcess.StartInfo.Arguments = $"{inputShaderFile} -T ps_6_0 -E PixelMain -Fo {psOutputShaderFile}";

            buildProcess.Start();
            buildProcess.WaitForExit();

            if (buildProcess.ExitCode != 0)
            {
                return null;
            }

            var pixelShaderData = await File.ReadAllBytesAsync(psOutputShaderFile);

            var destinationMemoryStream = new MemoryStream();
            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            streamWriter.Write(vertexShaderData.Length);
            streamWriter.Write(vertexShaderData);
            streamWriter.Write(pixelShaderData.Length); // TODO: Use span overload?
            streamWriter.Write(pixelShaderData);
            streamWriter.Flush();

            destinationMemoryStream.Flush();
            return new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
        }
    }
}