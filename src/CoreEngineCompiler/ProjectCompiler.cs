using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using CoreEngine.Tools.ResourceCompilers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CoreEngine.Compiler
{
    public class ProjectCompiler
    {
        private readonly ResourceCompiler resourceCompiler;
        private readonly Logger logger;

        public ProjectCompiler(ResourceCompiler resourceCompiler, Logger logger)
        {
            this.resourceCompiler = resourceCompiler;
            this.logger = logger;
        }

        public async Task CompileProject(string path, bool isWatchMode, bool rebuildAll)
        {
            var project = OpenProject(path);

            var inputDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            var inputObjDirectory = Path.Combine(inputDirectory, ".coreengine");
            var outputDirectory = Path.GetFullPath(Path.Combine(inputDirectory, project.OutputDirectory));

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            if (!Directory.Exists(inputObjDirectory))
            {
                Directory.CreateDirectory(inputObjDirectory);
            }

            var fileTrackerPath = Path.Combine(inputObjDirectory, "FileTracker");
            var fileTracker = new FileTracker();

            if (!rebuildAll || isWatchMode)
            {
                fileTracker.ReadFile(fileTrackerPath);
            }

            if (!isWatchMode)
            {
                this.logger.WriteMessage($"InputPath: {inputDirectory}", LogMessageType.Debug);
                this.logger.WriteMessage($"OutputPath: {outputDirectory}", LogMessageType.Debug);
            }

            var sourceFiles = SearchSupportedSourceFiles(inputDirectory);
            var remainingDestinationFiles = new List<string>(Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories));
            var compiledFilesCount = 0;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            foreach (var sourceFile in sourceFiles)
            {
                var hasFileChanged = fileTracker.HasFileChanged(sourceFile);

                var sourceFileAbsoluteDirectory = ConstructSourceFileAbsolutDirectory(inputDirectory, sourceFile);
                var destinationPath = ConstructDestinationPath(sourceFileAbsoluteDirectory, sourceFile, outputDirectory);

                remainingDestinationFiles.Remove(destinationPath);

                if ((!isWatchMode && (hasFileChanged || !File.Exists(destinationPath))) || (isWatchMode && hasFileChanged))
                {
                    if (isWatchMode)
                    {
                        this.logger.WriteMessage($"{DateTime.Now.ToString()} - Detected file change for '{sourceFile}'");
                    }
                    
                    var result = await CompileSourceFile(sourceFileAbsoluteDirectory, outputDirectory, sourceFile, destinationPath);
                    
                    if (result)
                    {
                        compiledFilesCount++;
                    }
                }
            }

            stopwatch.Stop();

            if (compiledFilesCount > 0)
            {
                this.logger.WriteLine();
                this.logger.WriteMessage($"Success: Compiled {compiledFilesCount} file(s) in {stopwatch.Elapsed}.", LogMessageType.Success);
            }

            fileTracker.WriteFile(fileTrackerPath);
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

        private string[] SearchSupportedSourceFiles(string inputDirectory)
        {
            var sourceFileExtensions = this.resourceCompiler.GetSupportedSourceFileExtensions();
            var sourceFiles = new List<string>();

            foreach (var fileExtension in sourceFileExtensions)
            {
                var searchPattern = fileExtension.Replace(".", "*.");
                var sourceFilesForExtension = Directory.GetFiles(inputDirectory, searchPattern, SearchOption.AllDirectories);

                sourceFiles.AddRange(sourceFilesForExtension);
            }

            return sourceFiles.ToArray();
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

        private string ConstructDestinationPath(string sourceFileAbsoluteDirectory, string sourceFile, string outputDirectory)
        {
            var destinationFileExtension = this.resourceCompiler.GetDestinationFileExtension(Path.GetExtension(sourceFile));
            var destinationFileName = $"{Path.GetFileNameWithoutExtension(sourceFile)}{destinationFileExtension}";
            var destinationPath = Path.Combine(outputDirectory, sourceFileAbsoluteDirectory, destinationFileName);
            return destinationPath;
        }

        private async Task<bool> CompileSourceFile(string sourceFileAbsoluteDirectory, string outputDirectory, string sourceFile, string destinationPath)
        {
            this.logger.WriteMessage($"Compiling '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileName(sourceFile))}'...", LogMessageType.Action);
            
            // TODO: Get the target platform from the command line arguments
            var targetPlatform = "windows";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                targetPlatform = "osx";
            }

            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                targetPlatform = "linux";
            }

            var resourceCompilerContext = new CompilerContext(targetPlatform, Path.GetFileName(sourceFile));
            
            var result = await this.resourceCompiler.CompileFileAsync(sourceFile, destinationPath, resourceCompilerContext);

            if (result)
            {
                this.logger.WriteMessage($"Compilation of '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileName(destinationPath))}' done.", LogMessageType.Success);
            }

            else
            {
                this.logger.WriteMessage($"Error: Compilation of '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileName(destinationPath))}' failed.", LogMessageType.Error);
            }

            return result;
        }

        private void CleanupOutputDirectory(string outputDirectory, List<string> remainingDestinationFiles)
        {
            foreach (var remainingDestinationFile in remainingDestinationFiles)
            {
                if (Path.GetFileName(remainingDestinationFile)[0] != '.')
                {
                    this.logger.WriteMessage($"Cleaning file '{remainingDestinationFile}...", LogMessageType.Debug);
                    File.Delete(remainingDestinationFile);
                }
            }

            foreach (var directory in Directory.GetDirectories(outputDirectory))
            {
                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    this.logger.WriteMessage($"Cleaning empty directory '{directory}...", LogMessageType.Debug);
                    Directory.Delete(directory, false);
                }
            }
        }
    }
}