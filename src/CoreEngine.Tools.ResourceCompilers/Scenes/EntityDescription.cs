using System;
using System.Collections.Generic;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class EntityDescription
    {
        public EntityDescription(string name)
        {
            this.Name = name;
            this.Components = new List<ComponentDescription>();
        }
        
        public string Name { get; }
        public int EntityLayoutIndex { get; set; }
        public List<ComponentDescription> Components { get; }
    }
}