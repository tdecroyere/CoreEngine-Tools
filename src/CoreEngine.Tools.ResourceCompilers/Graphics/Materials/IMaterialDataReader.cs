using System;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Materials
{
    public interface IMaterialDataReader
    {
        Span<MaterialDescription> Read(ReadOnlySpan<byte> sourceData, CompilerContext context);
    }
}