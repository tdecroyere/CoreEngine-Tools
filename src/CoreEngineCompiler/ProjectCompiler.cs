using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

        public ProjectCompiler(ResourceCompiler resourceCompiler)
        {
            this.resourceCompiler = resourceCompiler;
        }

        public async Task CompileProject(string path, string? searchPattern, bool isWatchMode, bool rebuildAll)
        {
            var project = OpenProject(path);

            var inputDirectory = Path.GetDirectoryName(Path.GetFullPath(path));

            if (inputDirectory == null)
            {
                Logger.WriteMessage($"Input path is not a directory.", LogMessageTypes.Error);
                return;
            }

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
                Logger.WriteMessage($"InputPath: {inputDirectory}", LogMessageTypes.Debug);
                Logger.WriteMessage($"OutputPath: {outputDirectory}", LogMessageTypes.Debug);
            }

            var sourceFiles = SearchSupportedSourceFiles(inputDirectory, searchPattern);
            var remainingDestinationFiles = new List<string>(Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories));
            var compiledFilesCount = 0;

            if (searchPattern != null)
            {
                remainingDestinationFiles.Clear();
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            foreach (var sourceFile in sourceFiles)
            {
                var hasFileChanged = fileTracker.HasFileChanged(sourceFile) || searchPattern != null;

                var sourceFileAbsoluteDirectory = ConstructSourceFileAbsolutDirectory(inputDirectory, sourceFile);
                var destinationPath = ConstructDestinationPath(sourceFileAbsoluteDirectory, sourceFile, outputDirectory);

                remainingDestinationFiles.Remove(destinationPath);

                if ((!isWatchMode && (hasFileChanged || !File.Exists(destinationPath))) || (isWatchMode && hasFileChanged))
                {
                    if (isWatchMode)
                    {
                        Logger.WriteMessage($"{DateTime.Now.ToString(CultureInfo.InvariantCulture)} - Detected file change for '{sourceFile}'");
                    }
                    
                    var result = await CompileSourceFile(sourceFileAbsoluteDirectory, sourceFile, destinationPath);
                    
                    if (result)
                    {
                        compiledFilesCount++;
                    }
                }
            }

            stopwatch.Stop();

            if (compiledFilesCount > 0)
            {
                Logger.WriteLine();
                Logger.WriteMessage($"Success: Compiled {compiledFilesCount} file(s) in {stopwatch.Elapsed}.", LogMessageTypes.Success);
            }

            // TODO: Remove deleted files from file tracker
            CleanupOutputDirectory(outputDirectory, remainingDestinationFiles);
            fileTracker.WriteFile(fileTrackerPath);
        }

        private static Project OpenProject(string path)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException("Project file doesn't exist.", nameof(path));
            }

            if (Path.GetExtension(path) != ".ceproj")
            {
                throw new ArgumentException("Project file is not a CoreEngine project.", nameof(path));
            }

            var content = File.ReadAllText(path);
            using var input = new StringReader(content);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            return deserializer.Deserialize<Project>(input);
        }

        private string[] SearchSupportedSourceFiles(string inputDirectory, string? searchPattern)
        {
            var sourceFileExtensions = this.resourceCompiler.GetSupportedSourceFileExtensions();
            var sourceFiles = new List<string>();

            if (searchPattern == null)
            {
                foreach (var fileExtension in sourceFileExtensions)
                {
                    var fileSearchPattern = fileExtension.Replace(".", "*.");
                    var sourceFilesForExtension = Directory.GetFiles(inputDirectory, fileSearchPattern, SearchOption.AllDirectories);

                    sourceFiles.AddRange(sourceFilesForExtension);
                }
            }

            else
            {
                var sourceFilesForExtension = Directory.GetFiles(inputDirectory, searchPattern, SearchOption.AllDirectories);
                sourceFiles.AddRange(sourceFilesForExtension);
            }

            return sourceFiles.ToArray();
        }

        private static string ConstructSourceFileAbsolutDirectory(string inputDirectory, string sourceFile)
        {
            var directoryName = Path.GetDirectoryName(sourceFile);

            if (directoryName == null)
            {
                throw new ArgumentException("Input is not a directory.", nameof(inputDirectory));
            }

            var sourceFileAbsoluteDirectory = directoryName.Replace(inputDirectory, string.Empty);

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

        private async Task<bool> CompileSourceFile(string sourceFileAbsoluteDirectory, string sourceFile, string destinationPath)
        {
            Logger.WriteMessage($"Compiling '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileName(sourceFile))}'...", LogMessageTypes.Action);
            
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
            
            try
            {
                var result = await this.resourceCompiler.CompileFileAsync(sourceFile, destinationPath, resourceCompilerContext);

                if (result)
                {
                    Logger.WriteMessage($"Compilation of '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileName(destinationPath))}' done.", LogMessageTypes.Success);
                }

                else
                {
                    Logger.WriteMessage($"Error: Compilation of '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileName(destinationPath))}' failed.", LogMessageTypes.Error);
                }

                return result;
            }

            catch
            {
                Logger.WriteMessage($"Critical Error: Compilation of '{Path.Combine(sourceFileAbsoluteDirectory, Path.GetFileName(destinationPath))}' failed.", LogMessageTypes.Error);
                return false;
            }
        }

        private static void CleanupOutputDirectory(string outputDirectory, List<string> remainingDestinationFiles)
        {
            foreach (var remainingDestinationFile in remainingDestinationFiles)
            {
                if (Path.GetFileName(remainingDestinationFile)[0] != '.')
                {
                    Logger.WriteMessage($"Cleaning file '{remainingDestinationFile}...", LogMessageTypes.Debug);
                    File.Delete(remainingDestinationFile);
                }
            }

            foreach (var directory in Directory.GetDirectories(outputDirectory))
            {
                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    Logger.WriteMessage($"Cleaning empty directory '{directory}...", LogMessageTypes.Debug);
                    Directory.Delete(directory, false);
                }
            }
        }
    }
}