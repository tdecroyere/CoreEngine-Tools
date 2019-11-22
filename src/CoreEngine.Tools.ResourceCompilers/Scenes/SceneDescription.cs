using System;
using System.Collections.Generic;
using System.Globalization;

namespace CoreEngine.Tools.ResourceCompilers.Scenes
{
    public class SceneDescription
    {
        public List<EntityLayoutDescription> EntityLayouts { get; } = new List<EntityLayoutDescription>();
        public List<EntityDescription> Entities { get; } = new List<EntityDescription>();

        public int AddEntityLayoutDescription(EntityLayoutDescription entityLayout)
        {
            if (entityLayout == null)
            {
                throw new ArgumentNullException(nameof(entityLayout));
            }

            var index = -1;

            var result = 0;
            var sortedList = new SortedList<int, string>();

            for (var i = 0; i < entityLayout.Types.Count; i++)
            {
                var typeHashCode = entityLayout.Types[i].GetHashCode();
                sortedList.Add(typeHashCode, entityLayout.Types[i]);
                result |= typeHashCode;
            }

            entityLayout.Types = new List<string>(sortedList.Values);

            for (var i = 0; i < this.EntityLayouts.Count; i++)
            {
                if (this.EntityLayouts[i].HashCode == result)
                {
                    index = i;
                }
            }

            if (index == -1)
            {
                index = this.EntityLayouts.Count;
                entityLayout.HashCode = result;
                this.EntityLayouts.Add(entityLayout);
            }

            return index;
        }
    }
}