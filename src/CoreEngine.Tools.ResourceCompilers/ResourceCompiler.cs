using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers
{
    public class ResourceCompiler
    {
        private readonly Logger logger;
        private IDictionary<string, ResourceDataCompiler> dataCompilers;

        public ResourceCompiler(Logger logger)
        {
            this.logger = logger;
            this.dataCompilers = new Dictionary<string, ResourceDataCompiler>();

            AddInternalDataCompilers();
        }

        public IList<string> GetSupportedSourceFileExtensions()
        {
            return new List<string>(this.dataCompilers.Keys);
        }

        public string GetDestinationFileExtension(string sourceFileExtension)
        {
            if (!this.dataCompilers.ContainsKey(sourceFileExtension))
            {
                throw new ArgumentException($"Extension: {sourceFileExtension} is not supported by the compiler");
            }

            return this.dataCompilers[sourceFileExtension].DestinationExtension;
        }

        // TODO: Replace parameters by structs
        public async Task<bool> CompileFileAsync(string inputPath, string output)
        {
            var outputDirectory = Path.GetDirectoryName(output);
            var sourceFileExtension = Path.GetExtension(inputPath);
            var destinationFileExtension = Path.GetExtension(output);

            if (!this.dataCompilers.ContainsKey(sourceFileExtension))
            {
                throw new ArgumentException($"Source file extension: {sourceFileExtension} is not supported by the compiler");
            }

            var dataCompiler = this.dataCompilers[sourceFileExtension];

            if (destinationFileExtension != dataCompiler.DestinationExtension)
            {
                throw new ArgumentException($"Destination file extension: {destinationFileExtension} is not supported by the compiler");
            }

            try
            {
                // TODO: Find a way to avoid copy data when using FileStream?
                var inputData = new ReadOnlyMemory<byte>(await File.ReadAllBytesAsync(inputPath));
                var outputData = await dataCompiler.CompileAsync(inputData);
                
                if (outputData != null)
                {
                    if (!Directory.Exists(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    await File.WriteAllBytesAsync(output, outputData.Value.ToArray());
                    return true;
                }
            }

            catch(Exception e)
            {
                logger.WriteMessage($"Error: {e.Message}", LogMessageType.Error);
            }

            return false;
        }

        private void AddInternalDataCompilers()
        {
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsSubclassOf(typeof(ResourceDataCompiler)))
                {
                    var dataCompiler = (ResourceDataCompiler)Activator.CreateInstance(type, this.logger);

                    foreach (var supportedExtension in dataCompiler.SupportedSourceExtensions)
                    {
                        this.dataCompilers.Add(supportedExtension, dataCompiler);
                    }
                }
            }
        }
    }
}