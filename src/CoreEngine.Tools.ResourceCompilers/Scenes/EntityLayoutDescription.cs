using System;
using System.Collections.Generic;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class EntityLayoutDescription
    {
        public int HashCode { get; set; }
        public List<string> Types { get; set; } = new List<string>();
    }
}