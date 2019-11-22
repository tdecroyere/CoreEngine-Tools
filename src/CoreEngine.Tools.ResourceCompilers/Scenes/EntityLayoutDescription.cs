using System;
using System.Collections.Generic;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class EntityLayoutDescription
    {
        public EntityLayoutDescription()
        {
            this.Types = new List<string>();
        }

        public int HashCode { get; set; }
        public List<string> Types { get; set; }
    }
}