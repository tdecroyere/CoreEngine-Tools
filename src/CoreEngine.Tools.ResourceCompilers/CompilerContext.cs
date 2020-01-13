using System;

namespace CoreEngine.Tools.ResourceCompilers
{
    public class CompilerContext
    {
        public CompilerContext(string targetPlatform, string targetFilename, string inputDirectory, string outputDirectory)
        {
            this.TargetPlatform = targetPlatform;
            this.SourceFilename = targetFilename;
            this.InputDirectory = inputDirectory;
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

        public string InputDirectory
        {
            get;
        }

        public string? OutputDirectory
        {
            get;
        }
    }
}