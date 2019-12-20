using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using SkiaSharp;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Textures
{
    public class TextureResourceDataCompiler : ResourceDataCompiler
    {
        public override string Name
        {
            get
            {
                return "Texture Resource Data Compiler";
            }
        }

        public override IList<string> SupportedSourceExtensions
        {
            get
            {
                return new string[] { ".png", ".jpg" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".texture";
            }
        }

        public override Task<ReadOnlyMemory<byte>?> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // TODO: Use premultiplied alpha
            
            var version = 1;

            var bitmap = SKBitmap.Decode(sourceData.ToArray());
            var bitmapData = bitmap.Pixels; // TODO: Use the pixel span method
            var imageDataSize = 4 * bitmap.Width * bitmap.Height;
            
            Logger.WriteMessage($"Texture compiler (Width: {bitmap.Width}, Height: {bitmap.Height})");

            var destinationMemoryStream = new MemoryStream();

            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            streamWriter.Write(new char[] { 'T', 'E', 'X', 'T', 'U', 'R', 'E' });
            streamWriter.Write(version);
            streamWriter.Write(bitmap.Width);
            streamWriter.Write(bitmap.Height);
            // TODO: Write format
            // TODO: Write mips info
            streamWriter.Write(imageDataSize);
            
            for (var i = 0; i < bitmapData.Length; i++)
            {
                streamWriter.Write(bitmapData[i].Red);
                streamWriter.Write(bitmapData[i].Green);
                streamWriter.Write(bitmapData[i].Blue);
                streamWriter.Write(bitmapData[i].Alpha);
            }

            streamWriter.Flush();

            destinationMemoryStream.Flush();
            return Task.FromResult((ReadOnlyMemory<byte>?)new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length));
        }
    }
}