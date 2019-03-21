using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.ResourceCompilers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CoreEngine.Compiler
{
    public class ProjectCompiler
    {
        public ProjectCompiler()
        {

        }

        public async Task CompileProject(string path)
        {
            // TODO: Only re-compile changed source files
            // TODO: Add a watcher functionnality
            // TODO: Proper logging with verbose mode on/off

            var project = OpenProject(path);

            var inputDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            var inputObjDirectory = Path.Combine(inputDirectory, "obj");
            var outputDirectory = Path.GetFullPath(Path.Combine(inputDirectory, project.OutputDirectory));

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            if (!Directory.Exists(inputObjDirectory))
            {
                Directory.CreateDirectory(inputObjDirectory);
            }

            var hashListPath = Path.Combine(inputObjDirectory, "HashList");
            var hashListFile = new HashListFile(hashListPath);

            Console.WriteLine($"InputPath: {inputDirectory}");
            Console.WriteLine($"OutputPath: {outputDirectory}");

            var resourceCompiler = new ResourceCompiler();
            var sourceFiles = SearchSupportedSourceFiles(resourceCompiler, inputDirectory);
            var remainingDestinationFiles = new List<string>(Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories));

            foreach (var sourceFile in sourceFiles)
            {
                var hasFileChanged = hashListFile.HasFileChanged(sourceFile);
                Console.WriteLine(hasFileChanged);

                var sourceFileAbsoluteDirectory = ConstructSourceFileAbsolutDirectory(inputDirectory, sourceFile);
                var destinationPath = ConstructDestinationPath(resourceCompiler, sourceFileAbsoluteDirectory, sourceFile, outputDirectory);

                remainingDestinationFiles.Remove(destinationPath);

                await CompileSourceFile(resourceCompiler, sourceFileAbsoluteDirectory, outputDirectory, sourceFile, destinationPath);
            }

            CleanupOutputDirectory(outputDirectory, remainingDestinationFiles);
        }

        private Project OpenProject(string path)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException("Project file doesn't exist.", "path");
            }

            if (Path.GetExtension(path) != ".ceproj")
            {
                throw new ArgumentException("Project file is not a CoreEngine project.", "path");
            }

            var content = File.ReadAllText(path);
            var input = new StringReader(content);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            return deserializer.Deserialize<Project>(input);
        }

        private static string[] SearchSupportedSourceFiles(ResourceCompiler resourceCompiler, string inputDirectory)
        {
            var sourceFileExtensions = resourceCompiler.GetSupportedSourceFileExtensions();
            var searchPattern = string.Join("|", sourceFileExtensions).Replace(".", "*.");

            var sourceFiles = Directory.GetFiles(inputDirectory, searchPattern, SearchOption.AllDirectories);
            return sourceFiles;
        }

        private static string ConstructSourceFileAbsolutDirectory(string inputDirectory, string sourceFile)
        {
            var sourceFileAbsoluteDirectory = Path.GetDirectoryName(sourceFile).Replace(inputDirectory, string.Empty);

            if (!string.IsNullOrEmpty(sourceFileAbsoluteDirectory))
            {
                sourceFileAbsoluteDirectory = sourceFileAbsoluteDirectory.Substring(1);
            }

            return sourceFileAbsoluteDirectory;
        }

        private static string ConstructDestinationPath(ResourceCompiler resourceCompiler, string sourceFileAbsoluteDirectory, string sourceFile, string outputDirectory)
        {
            var destinationFileExtension = resourceCompiler.GetDestinationFileExtension(Path.GetExtension(sourceFile));
            var destinationFileName = $"{Path.GetFileNameWithoutExtension(sourceFile)}{destinationFileExtension}";
            var destinationPath = Path.Combine(outputDirectory, sourceFileAbsoluteDirectory, destinationFileName);
            return destinationPath;
        }

        private static async Task CompileSourceFile(ResourceCompiler resourceCompiler, string sourceFileAbsoluteDirectory, string outputDirectory, string sourceFile, string destinationPath)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Compiling '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileNameWithoutExtension(sourceFile))}'...");
            Console.ForegroundColor = ConsoleColor.Gray;

            await resourceCompiler.CompileFileAsync(sourceFile, destinationPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Compilation of '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileName(destinationPath))}' done.");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private static void CleanupOutputDirectory(string outputDirectory, List<string> remainingDestinationFiles)
        {
            foreach (var remainingDestinationFile in remainingDestinationFiles)
            {
                Console.WriteLine($"Cleaning file '{remainingDestinationFile}...");
                File.Delete(remainingDestinationFile);
            }

            foreach (var directory in Directory.GetDirectories(outputDirectory))
            {
                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    Console.WriteLine($"Cleaning empty directory '{directory}...");
                    Directory.Delete(directory, false);
                }
            }
        }
    }
}