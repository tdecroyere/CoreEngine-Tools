using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using SkiaSharp;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Textures
{
    public struct GlyphInfo
    {
        public int AsciiCode { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int BearingLeft { get; set; }
        public int BearingRight { get; set; }
        public float TextureMinPointX { get; set; }
        public float TextureMinPointY { get; set; }
        public float TextureMaxPointX { get; set; }
        public float TextureMaxPointY { get; set; }
    }

    public class FontResourceDataCompiler : ResourceDataCompiler
    {
        public override string Name
        {
            get
            {
                return "Font Resource Data Compiler";
            }
        }

        public override IList<string> SupportedSourceExtensions
        {
            get
            {
                return new string[] { ".ttf" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".font";
            }
        }

        public override Task<ReadOnlyMemory<ResourceEntry>> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var version = 1;
            var glyphCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-/\\\"'#&!., :$<>[]()";

            using var fontManager = SKFontManager.CreateDefault();

            using var data = SKData.CreateCopy(sourceData.Span);
            var typeFace = fontManager.CreateTypeface(data);

            Logger.WriteMessage($"Font compiler (Width: {typeFace.FontWidth}, Height: {typeFace.FontStyle})");

            // TODO: Use premultiplied alpha

            var textureSize = 512;

            using var bitmap = new SKBitmap(textureSize, textureSize, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(SKColors.Transparent);

            using var paint1 = new SKPaint {
                TextSize = 34.0f,
                IsAntialias = true,
                //LcdRenderText = true,
                Color = SKColors.White,
                Style = SKPaintStyle.Fill
            };

            var lineSpacing = paint1.GetFontMetrics(out var fontMetrics);
            var glyphWidths = paint1.GetGlyphWidths(glyphCharacters);
            var glyphPositions = new SKPoint[glyphWidths.Length];
            var currentXPosition = 0.0f;
            var currentYPosition = 0.0f;

            var glyphInfos = new GlyphInfo[glyphCharacters.Length];

            // TODO: Glyph Bearing needs to be taken into account

            for (var i = 0; i < glyphWidths.Length; i++)
            {
                if (currentXPosition + glyphWidths[i] + 10 > textureSize)
                {
                    currentXPosition = 0;
                    currentYPosition += fontMetrics.Descent - fontMetrics.Ascent + 10;
                }

                glyphInfos[i] = new GlyphInfo
                {
                    AsciiCode = glyphCharacters[i],
                    Width = (int)glyphWidths[i],
                    Height = (int)(fontMetrics.Descent - fontMetrics.Ascent),
                    TextureMinPointX = currentXPosition / textureSize,
                    TextureMinPointY = currentYPosition / textureSize,
                    TextureMaxPointX = (currentXPosition + (int)glyphWidths[i]) / textureSize,
                    TextureMaxPointY = (currentYPosition + (fontMetrics.Descent - fontMetrics.Ascent)) / textureSize
                };

                Logger.WriteMessage($"GlyphInfo for '{(char)glyphInfos[i].AsciiCode}': Width: {glyphInfos[i].Width} - Height: {glyphInfos[i].Height} - TexMin: ({glyphInfos[i].TextureMinPointX}, {glyphInfos[i].TextureMinPointY}) - TexMax: ({glyphInfos[i].TextureMaxPointX}, {glyphInfos[i].TextureMaxPointY})");

                glyphPositions[i] = new SKPoint(currentXPosition, (currentYPosition - fontMetrics.Ascent));
                currentXPosition += glyphWidths[i] + 10;
            }

            canvas.DrawPositionedText(glyphCharacters, glyphPositions, paint1);


            var bitmapData = bitmap.Pixels; // TODO: Use the pixel span method
            var imageDataSize = 4 * bitmap.Width * bitmap.Height;
            
            var destinationMemoryStream = new MemoryStream();

            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            streamWriter.Write(new char[] { 'F', 'O', 'N', 'T' });
            streamWriter.Write(version);

            streamWriter.Write(glyphInfos.Length);

            for (var i = 0; i < glyphInfos.Length; i++)
            {
                streamWriter.Write(glyphInfos[i].AsciiCode);
                streamWriter.Write(glyphInfos[i].Width);
                streamWriter.Write(glyphInfos[i].Height);
                streamWriter.Write(glyphInfos[i].BearingLeft);
                streamWriter.Write(glyphInfos[i].BearingRight);
                streamWriter.Write(glyphInfos[i].TextureMinPointX);
                streamWriter.Write(glyphInfos[i].TextureMinPointY);
                streamWriter.Write(glyphInfos[i].TextureMaxPointX);
                streamWriter.Write(glyphInfos[i].TextureMaxPointY);
            }

            streamWriter.Write(bitmap.Width);
            streamWriter.Write(bitmap.Height);
            // TODO: Write format
            // TODO: Write mips info
            streamWriter.Write(imageDataSize);
            
            for (var i = 0; i < bitmapData.Length; i++)
            {
                streamWriter.Write((byte)255);
                streamWriter.Write((byte)255);
                streamWriter.Write((byte)255);
                streamWriter.Write(bitmapData[i].Alpha);
            }

            streamWriter.Flush();

            destinationMemoryStream.Flush();

            var resourceData = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
            var resourceEntry = new ResourceEntry($"{Path.GetFileNameWithoutExtension(context.SourceFilename)}{this.DestinationExtension}", resourceData);

            return Task.FromResult(new ReadOnlyMemory<ResourceEntry>(new ResourceEntry[] { resourceEntry }));
        }
    }
}