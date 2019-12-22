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
        private IDictionary<string, ResourceDataCompiler> dataCompilers;

        public ResourceCompiler()
        {
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
        public async ValueTask<Memory<string>> CompileFileAsync(string inputPath, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            
            var sourceFileExtension = Path.GetExtension(inputPath);

            if (!this.dataCompilers.ContainsKey(sourceFileExtension))
            {
                throw new ArgumentException($"Source file extension: {sourceFileExtension} is not supported by the compiler");
            }

            var dataCompiler = this.dataCompilers[sourceFileExtension];

            try
            {
                // TODO: Find a way to avoid copy data when using FileStream?
                var inputData = new ReadOnlyMemory<byte>(await File.ReadAllBytesAsync(inputPath));
                var outputResources = await dataCompiler.CompileAsync(inputData, context);
                
                if (outputResources.Length > 0)
                {
                    var result = new string[outputResources.Length];

                    if (!Directory.Exists(context.OutputDirectory))
                    {
                        Directory.CreateDirectory(context.OutputDirectory);
                    }

                    for (var i = 0; i < outputResources.Length; i++)
                    {
                        var outputResource = outputResources.Span[i];
                        var outputPath = Path.Combine(context.OutputDirectory, outputResource.Filename);
                        result[i] = outputPath;

                        await File.WriteAllBytesAsync(outputPath, outputResource.Data.ToArray());
                    }

                    return result;
                }
            }

            catch (Exception e)
            {
                Logger.WriteMessage($"Error: {e.ToString()}", LogMessageTypes.Error);
            }

            return new Memory<string>();
        }

        private void AddInternalDataCompilers()
        {
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsSubclassOf(typeof(ResourceDataCompiler)))
                {
                    var dataCompiler = (ResourceDataCompiler?)Activator.CreateInstance(type);

                    if (dataCompiler != null)
                    {
                        foreach (var supportedExtension in dataCompiler.SupportedSourceExtensions)
                        {
                            this.dataCompilers.Add(supportedExtension, dataCompiler);
                        }
                    }
                }
            }
        }
    }
}