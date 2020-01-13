using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using SkiaSharp;
using TeximpNet;
using TeximpNet.Compression;

namespace CoreEngine.Tools.ResourceCompilers.Graphics.Textures
{
    public enum TextureFormat
    {
        Rgba8UnormSrgb,
        Bgra8UnormSrgb,
        Depth32Float,
        Rgba16Float,
        R16Float,
        BC1Srgb,
        BC2Srgb,
        BC3Srgb,
        BC4,
        BC5,
        BC6,
        BC7Srgb
    }

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
                return new string[] { ".png", ".jpg", ".bmp" };
            }
        }

        public override string DestinationExtension
        {
            get
            {
                return ".texture";
            }
        }

        public unsafe override Task<ReadOnlyMemory<ResourceEntry>> CompileAsync(ReadOnlyMemory<byte> sourceData, CompilerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var version = 1;

            bool isNormalMap = context.SourceFilename.Contains("ddn");
            bool isBumpMap = context.SourceFilename.Contains("bump");
            Logger.WriteMessage($"NormalMap: {isNormalMap}");
            Logger.WriteMessage($"BumpMap: {isBumpMap}");

            using var memoryStream = new MemoryStream(sourceData.ToArray());
            var image = Surface.LoadFromStream(memoryStream);
            image.FlipVertically();

            Logger.WriteMessage($"Texture compiler (Width: {image.Width}, Height: {image.Height})");

            using var compressor = new Compressor();
            compressor.Input.GenerateMipmaps = true;
            compressor.Input.SetData(image);
            compressor.Compression.Format = isNormalMap ? CompressionFormat.BC5 : isBumpMap ? CompressionFormat.BC4 : CompressionFormat.BC3;
            compressor.Output.OutputFileFormat = OutputFileFormat.DDS10;
            compressor.Output.IsSRGBColorSpace = true;

            compressor.Process(out var compressedImage);

            if (compressedImage != null)
            {
                Logger.WriteMessage("OK");
            }

            else
            {
                Logger.WriteMessage(compressor.Output.ToString());
            }

            var destinationMemoryStream = new MemoryStream();

            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            streamWriter.Write(new char[] { 'T', 'E', 'X', 'T', 'U', 'R', 'E' });
            streamWriter.Write(version);
            streamWriter.Write(image.Width);
            streamWriter.Write(image.Height);
            streamWriter.Write(isNormalMap ? (int)TextureFormat.BC5 : isBumpMap ? (int)TextureFormat.BC4 : (int)TextureFormat.BC3Srgb);

            var mipLevels = compressedImage.MipChains[0].Count;

            Logger.WriteMessage($"Mip Levels: {mipLevels}");
            streamWriter.Write(mipLevels);

            for (var i = 0; i < mipLevels; i++)
            {
                var mipData = compressedImage.MipChains[0][i];
                var mipRawData = new Span<byte>(mipData.Data.ToPointer(), mipData.SizeInBytes);

                streamWriter.Write(mipData.SizeInBytes);
                streamWriter.Write(mipRawData);
            }

            streamWriter.Flush();
            destinationMemoryStream.Flush();

            var resourceData = new Memory<byte>(destinationMemoryStream.GetBuffer(), 0, (int)destinationMemoryStream.Length);
            var resourceEntry = new ResourceEntry($"{Path.GetFileNameWithoutExtension(context.SourceFilename)}{this.DestinationExtension}", resourceData);

            return Task.FromResult(new ReadOnlyMemory<ResourceEntry>(new ResourceEntry[] { resourceEntry }));
        }

        private static int PixelOffset(int x, int y, int width, int height, int pixelSize = 1)
        {
            if (x < 0)
            {
                x = width - 1;
            }

            else if (x >= width)
            {
                x = 0;
            }

            if (y < 0)
            {
                y = height - 1;
            }

            else if (y >= height)
            {
                y = 0;
            }

            return y * width * pixelSize + x * pixelSize;
        }

        static byte[] NormalToBump(SKColor[] data, int width, int height) 
        {
            var signConvention = 1.0f;
            var laplacian = new float[width * height];

            // Compute the laplacian once; it never changes
            for (int x = 0; x < width; ++x) 
            {
                for (int y = 0; y < height; ++y) 
                {
                    var ddx = data[PixelOffset(x + 1, y, width, height)].Red - data[PixelOffset(x - 1, y, width, height)].Red;
                    var ddy = data[PixelOffset(x, y + 1, width, height)].Green - data[PixelOffset(x, y - 1, width, height)].Green;

                    laplacian[PixelOffset(x, y, width, height)] = (ddx + signConvention * ddy) / 2.0f;
                }
            }

            // Ping-pong buffers
            var src = new float[width * height];
            var dst = new float[width * height];

            for (int x = 0; x < width; ++x) 
            {
                for (int y = 0; y < height; ++y) 
                {
                    dst[PixelOffset(x, y, width, height)] = 0.5f;
                }
            }

            // Number of Poisson iterations
            const int N = 100;

            for (int i = 0; i < N; ++i) 
            {
                // Swap buffers
                var tmp = src;
                src = dst;
                dst = tmp;

                for (int x = 0; x < width; ++x) 
                {
                    for (int y = 0; y < height; ++y) 
                    {
                        var minValue = src[PixelOffset(x - 1, y, width, height)] + src[PixelOffset(x, y - 1, width, height)];
                        var maxValue = src[PixelOffset(x + 1, y, width, height)] + src[PixelOffset(x, y + 1, width, height)];
                        var laplacianValue = laplacian[PixelOffset(x, y, width, height)];

                        dst[PixelOffset(x, y, width, height)] = (minValue + maxValue + laplacianValue) * 0.25f;
                    }
                }

                Console.WriteLine($"On pass {i}/{N}");
            }

            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;

            for (int x = 0; x < width; ++x) 
            {
                for (int y = 0; y < height; ++y) 
                {
                    var v = dst[PixelOffset(x, y, width, height)];

                    lo = MathF.Min(lo, v);
                    hi = MathF.Max(hi, v);
                }
            }

            Console.WriteLine($"Hi: {hi} - Lo: {lo}");

            for (int x = 0; x < width; ++x) 
            {
                for (int y = 0; y < height; ++y) 
                {
                    dst[PixelOffset(x, y, width, height)] = (dst[PixelOffset(x, y, width, height)] - lo) / (hi - lo);
                }
            }

            var final = new byte[width * height];

            for (int x = 0; x < width; ++x) 
            {
                for (int y = 0; y < height; ++y) 
                {
                    final[PixelOffset(x, y, width, height)] = (byte)(dst[PixelOffset(x, y, width, height)] * 255);
                }
            }

            return final;
        }
    }
}