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

        public override Task<ReadOnlyMemory<ResourceEntry>> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // TODO: Use premultiplied alpha
            
            var version = 1;

            var bitmap = SKBitmap.Decode(sourceData.ToArray());
            
            Logger.WriteMessage($"Texture compiler (Width: {bitmap.Width}, Height: {bitmap.Height})");

            var destinationMemoryStream = new MemoryStream();

            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            streamWriter.Write(new char[] { 'T', 'E', 'X', 'T', 'U', 'R', 'E' });
            streamWriter.Write(version);
            streamWriter.Write(bitmap.Width);
            streamWriter.Write(bitmap.Height);
            // TODO: Write format
            // TODO: Write mips info

            var mipLevels = (int)MathF.Floor(MathF.Log2(MathF.Max(bitmap.Width, bitmap.Height))) + 1;
            Logger.WriteMessage($"Mip Levels: {mipLevels}");
            streamWriter.Write(mipLevels);

            var bitmapData = bitmap.Pixels;
            var imageDataSize = 4 * bitmap.Width * bitmap.Height;

            streamWriter.Write(imageDataSize);
            
            for (var j = 0; j < bitmapData.Length; j++)
            {
                streamWriter.Write(bitmapData[j].Red);
                streamWriter.Write(bitmapData[j].Green);
                streamWriter.Write(bitmapData[j].Blue);
                streamWriter.Write(bitmapData[j].Alpha);
            }

            var textureWidth = bitmap.Width;
            var textureHeight = bitmap.Height;

            using var paint1 = new SKPaint {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
            };

            for (var i = 1; i < mipLevels; i++)
            {
                textureWidth = (textureWidth > 1) ? textureWidth / 2 : 1;
                textureHeight = (textureHeight > 1) ? textureHeight / 2 : 1;
                imageDataSize = 4 * textureWidth * textureHeight;

                using var destinationBitmap = new SKBitmap(textureWidth, textureHeight, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
                using var canvas = new SKCanvas(destinationBitmap);
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bitmap, new SKRect(0, 0, textureWidth, textureHeight), paint1);
                bitmapData = destinationBitmap.Pixels; // TODO: Use the pixel span method

                streamWriter.Write(imageDataSize);
                
                for (var j = 0; j < bitmapData.Length; j++)
                {
                    streamWriter.Write(bitmapData[j].Red);
                    streamWriter.Write(bitmapData[j].Green);
                    streamWriter.Write(bitmapData[j].Blue);
                    streamWriter.Write(bitmapData[j].Alpha);
                }
            }

            streamWriter.Flush();
            destinationMemoryStream.Flush();

            var resourceData = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
            var resourceEntry = new ResourceEntry($"{Path.GetFileNameWithoutExtension(context.SourceFilename)}{this.DestinationExtension}", resourceData);

            return Task.FromResult(new ReadOnlyMemory<ResourceEntry>(new ResourceEntry[] { resourceEntry }));
        }
    }
}