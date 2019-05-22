using System;
using System.Collections.Generic;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class SceneDescription
    {
        public List<EntityDescription> Entities { get; } = new List<EntityDescription>();
    }
}