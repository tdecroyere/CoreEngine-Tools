using System;
using System.Collections.Generic;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class SceneDescription
    {
        public IList<SceneEntityDescription> Entities { get; set; } = new List<SceneEntityDescription>();
    }
}