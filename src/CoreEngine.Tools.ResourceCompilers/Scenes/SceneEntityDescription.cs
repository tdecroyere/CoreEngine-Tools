using System;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class SceneEntityDescription
    {
        public SceneEntityDescription(string name)
        {
            this.Name = name;
        }
        
        public string Name { get; }
    }
}