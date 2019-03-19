using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace CoreEngine.ResourceCompilers
{
    public class ResourceCompiler
    {
        private IDictionary<string, ResourceDataCompiler> dataCompilers;

        public ResourceCompiler()
        {
            this.dataCompilers = new Dictionary<string, ResourceDataCompiler>();

            AddInternalDataCompilers();
        }

        // TODO: Replace parameters by structs
        public async Task CompileFileAsync(string input, string output)
        {
            var sourceFileExtension = Path.GetExtension(input);
            var destinationFileExtension = Path.GetExtension(output);

            if (!this.dataCompilers.ContainsKey(sourceFileExtension))
            {
                throw new ArgumentException($"Extension: {sourceFileExtension} is not supported by the compiler");
            }

            var dataCompiler = this.dataCompilers[sourceFileExtension];

            if (destinationFileExtension != dataCompiler.DestinationExtension)
            {
                throw new ArgumentException($"Extension: {destinationFileExtension} is not supported by the compiler");
            }

            // TODO: Check if file exists

            var inputData = await File.ReadAllBytesAsync(input);
            var outputData = await dataCompiler.CompileAsync(inputData);

            await File.WriteAllBytesAsync(output, outputData);
        }

        private void AddInternalDataCompilers()
        {
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsSubclassOf(typeof(ResourceDataCompiler)))
                {
                    var dataCompiler = (ResourceDataCompiler)Activator.CreateInstance(type);

                    foreach (var supportedExtension in dataCompiler.SupportedSourceExtensions)
                    {
                        this.dataCompilers.Add(supportedExtension, dataCompiler);
                    }
                }
            }
        }
    }
}