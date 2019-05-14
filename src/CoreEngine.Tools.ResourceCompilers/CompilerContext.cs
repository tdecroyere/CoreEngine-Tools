using System;

namespace CoreEngine.Tools.ResourceCompilers
{
    public class CompilerContext
    {
        public CompilerContext(string targetPlatform, string targetFilename)
        {
            this.TargetPlatform = targetPlatform;
            this.TargetFilename = targetFilename;
        }

        public string TargetPlatform
        {
            get;
        }

        public string TargetFilename
        {
            get;
        }

    }
}