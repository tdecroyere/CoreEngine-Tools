using System;

namespace CoreEngine.Compiler
{
    public class Project
    {
        public Project()
        {
            this.OutputDirectory = ".";
        }
        
        public string OutputDirectory { get; set; }
    }
}