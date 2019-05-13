using System;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Tools.ResourceCompilers
{
    public class TestResourceDataCompiler : ResourceDataCompiler
    {
        public TestResourceDataCompiler(Logger logger) : base(logger)
        {

        }
        
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

        public override Task<ReadOnlyMemory<byte>?> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            this.Logger.WriteMessage("Test Compiler");
            // TODO: Try to avoid the ToArray call that copy the buffer to the MemoryStream
            using var streamReader = new StreamReader(new MemoryStream(sourceData.ToArray()));
            var inputText = streamReader.ReadToEnd();
            
            var destinationMemoryStream = new MemoryStream();

            using var streamWriter = new StreamWriter(destinationMemoryStream);
            streamWriter.Write(inputText.Replace("Source", "Compiled"));
            streamWriter.Flush();

            return Task.FromResult<ReadOnlyMemory<byte>?>(destinationMemoryStream.ToArray());
        }
    }
}