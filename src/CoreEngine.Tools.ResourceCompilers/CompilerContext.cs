using System;

namespace CoreEngine.Tools.ResourceCompilers
{
    public class CompilerContext
    {
        public CompilerContext(string targetPlatform)
        {
            this.TargetPlatform = targetPlatform;
        }

        public string TargetPlatform
        {
            get;
        }
    }
}