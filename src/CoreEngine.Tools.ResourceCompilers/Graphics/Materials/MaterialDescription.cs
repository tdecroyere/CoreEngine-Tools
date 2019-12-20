using System.Collections.Generic;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Materials
{
    public class MaterialProperty
    {
        public MaterialProperty(string name, object value)
        {
            this.Name = name;
            this.Value = value;
        }

        public string Name { get;}
        public object Value { get; }
    }
    
    public class MaterialDescription
    {
        public IList<MaterialProperty> Properties { get; } = new List<MaterialProperty>();
    }
}