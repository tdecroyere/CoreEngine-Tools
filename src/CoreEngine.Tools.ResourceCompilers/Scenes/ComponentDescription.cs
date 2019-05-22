using System;
using System.Collections.Generic;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class ComponentDescription
    {
        public ComponentDescription(string componentType)
        {
            this.ComponentType = componentType;
            this.ComponentValues = new Dictionary<string, object>();
        }
        
        public string ComponentType { get; }
        public Dictionary<string, object> ComponentValues { get; }
    }
}