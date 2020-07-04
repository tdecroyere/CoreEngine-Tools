using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreEngineInteropGenerator
{
    class Program
    {   
        static async Task Main(string[] args)
        {
            var inputPath = "../../../CoreEngine/src/CoreEngine/HostServices";
            var outputPath = "../../../CoreEngine/src/CoreEngine/HostServices/Interop";
            var swiftProtocolsOutputPath = "../../../CoreEngine/src/Host/Apple/CoreEngineCommon/HostServices";
            var swiftInteropOutputPath = "../../../CoreEngine/src/Host/Apple/CoreEngineCommon/HostServices/Interop";
            var cHeaderOutputPath = "../../../CoreEngine/src/Host/Common";
            var cppOutputPath = "../../../CoreEngine/src/Host/Windows/Interop";


            if (args.Length > 0)
            {
                inputPath = args[0];
            }

            if (!Directory.Exists(inputPath))
            {
                Console.WriteLine("ERROR: Input path is not a directory.");
                return;
            }

            if (!Directory.Exists(outputPath))
            {
                Console.WriteLine("ERROR: Output path is not a directory.");
                return;
            }

            if (!Directory.Exists(swiftProtocolsOutputPath))
            {
                Console.WriteLine("ERROR: Swift Protocols Output path is not a directory.");
                return;
            }

            if (!Directory.Exists(swiftInteropOutputPath))
            {
                Console.WriteLine("ERROR: Swift Interop Output path is not a directory.");
                return;
            }

            if (!Directory.Exists(cHeaderOutputPath))
            {
                Console.WriteLine("ERROR: C Header Output path is not a directory.");
                return;
            }

            if (!Directory.Exists(cppOutputPath))
            {
                Console.WriteLine("ERROR: Cpp Output path is not a directory.");
                return;
            }

            foreach (var inputFile in Directory.GetFiles(inputPath))
            {
                if (Path.GetFileName(inputFile) == "HostPlatform.cs")
                {
                    continue;
                }

                var code = await File.ReadAllTextAsync(inputFile);
                var tree = CSharpSyntaxTree.ParseText(code);

                if (!(await tree.GetRootAsync() is CompilationUnitSyntax compilationUnit))
                {
                    Console.WriteLine("ERROR: Root node of C# file is not a CompilationUnit.");
                    return;
                }

                // Generate C# code
                var output = CSharpCodeGenerator.GenerateCode(compilationUnit);
                //Console.WriteLine(output);

                var outputFileName = Path.GetFileName(inputFile).Substring(1);
                await File.WriteAllTextAsync(Path.Combine(outputPath, outputFileName), output);

                // Generate Swift Code
                output = SwiftCodeGenerator.GenerateProtocolCode(compilationUnit);

                outputFileName = Path.GetFileName(inputFile).Substring(1).Replace(".cs", ".swift");
                await File.WriteAllTextAsync(Path.Combine(swiftProtocolsOutputPath, outputFileName), output);

                var swiftInteropImplementationTypes = new Dictionary<string, string>()
                {
                    { "IGraphicsService", "MetalGraphicsService" },
                    { "IGraphicsBufferEncoder", "MetalGraphicsBufferEncoder" },
                    { "IInputsService", "InputsManager" }
                };

                output = SwiftCodeGenerator.GenerateInteropCode(compilationUnit, swiftInteropImplementationTypes);

                outputFileName = Path.GetFileName(inputFile).Substring(1).Replace(".cs", "Interop.swift");
                await File.WriteAllTextAsync(Path.Combine(swiftInteropOutputPath, outputFileName), output);

                // Generate C Header Code
                output = CHeaderCodeGenerator.GenerateHeaderCode(compilationUnit);

                outputFileName = Path.GetFileName(inputFile).Substring(1).Replace(".cs", ".h");
                await File.WriteAllTextAsync(Path.Combine(cHeaderOutputPath, outputFileName), output);

                // Generate Cpp Code
                var cppImplementationTypes = new Dictionary<string, string>()
                {
                    { "IGraphicsService", "WindowsDirect3D12Renderer" },
                    { "IInputsService", "InputsManager" }
                };

                output = CppCodeGenerator.GenerateInteropCode(compilationUnit, cppImplementationTypes);
                
                outputFileName = Path.GetFileName(inputFile).Substring(1).Replace(".cs", "Interop.h");
                await File.WriteAllTextAsync(Path.Combine(cppOutputPath, outputFileName), output);
            }
        }
    }
}
