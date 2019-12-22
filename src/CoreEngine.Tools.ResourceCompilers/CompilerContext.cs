using System;

namespace CoreEngine.Tools.ResourceCompilers
{
    public class CompilerContext
    {
        public CompilerContext(string targetPlatform, string targetFilename)
        {
            this.TargetPlatform = targetPlatform;
            this.SourceFilename = targetFilename;
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
            set;
        }
    }
}