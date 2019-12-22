using System;

namespace CoreEngine.Tools.ResourceCompilers
{
    public class CompilerContext
    {
        public CompilerContext(string targetPlatform, string targetFilename, string outputDirectory)
        {
            this.TargetPlatform = targetPlatform;
            this.SourceFilename = targetFilename;
            this.OutputDirectory = outputDirectory;
        }

        public string TargetPlatform
        {
            get;
        }

        public string SourceFilename
        {
            get;
        }

        public string? OutputDirectory
        {
            get;
        }
    }
}