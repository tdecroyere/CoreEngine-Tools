using System;
using System.IO;
using System.Threading.Tasks;

namespace CoreEngine.ResourceCompilers
{
    public class TestResourceDataCompiler : ResourceDataCompiler
    {
        public override string Name
        {
            get
            {
                return "Test Resource Data Compiler";
            }
        }

        public override string[] SupportedSourceExtensions
        {
            get
            {
                return new string[] { ".txt" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".tst";
            }
        }

        public override Task<byte[]> CompileAsync(byte[] sourceData)
        {
            using (var streamReader = new StreamReader(new MemoryStream(sourceData)))
            {
                var inputText = streamReader.ReadToEnd();
                
                var destinationMemoryStream = new MemoryStream();

                using (var streamWriter = new StreamWriter(destinationMemoryStream))
                {
                    streamWriter.Write(inputText.Replace("Source", "Compiled"));
                }

                return Task.FromResult(destinationMemoryStream.ToArray());
            }
        }
    }
}