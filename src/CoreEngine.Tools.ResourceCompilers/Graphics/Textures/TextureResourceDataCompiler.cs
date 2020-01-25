using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using SkiaSharp;
using TeximpNet;
using TeximpNet.Compression;
using TeximpNet.DDS;

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
        BC7Srgb,
        Rgba32Float
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
                return new string[] { ".png", ".jpg", ".bmp", ".dds", ".hdr" };
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

            bool isNormalMap = context.SourceFilename.Contains("ddn") || context.SourceFilename.ToLower().Contains("normal");
            bool isBumpMap = context.SourceFilename.Contains("bump");
            bool isMask = context.SourceFilename.Contains("mask");
            bool isDiffuse = context.SourceFilename.Contains("_diff");
            bool isHdr = Path.GetExtension(context.SourceFilename) == ".hdr";
            bool isCubeMap = context.SourceFilename.Contains("cubemap");

            Logger.WriteMessage($"NormalMap: {isNormalMap}");
            Logger.WriteMessage($"BumpMap: {isBumpMap}");
            Logger.WriteMessage($"Hdr: {isHdr}");
            Logger.WriteMessage($"CubeMap: {isCubeMap}");

            if (isMask)
            {
                return Task.FromResult(new ReadOnlyMemory<ResourceEntry>(new ResourceEntry[] {}));
            }

            using var memoryStream = new MemoryStream(sourceData.ToArray());
            DDSContainer? compressedImage = null;

            if (Path.GetExtension(context.SourceFilename) == ".ddsold")
            {
                compressedImage = DDSFile.Read(memoryStream);
            }

            else if (isCubeMap)
            {
                var image = Surface.LoadFromStream(memoryStream);
                image.FlipVertically();
                var result = image.ConvertTo(ImageConversion.ToRGBAF);
                Logger.WriteMessage($"Conversion: {result}");
                image.SaveToFile(ImageFormat.EXR, $"Master.hdr");

                compressedImage = new DDSContainer(DXGIFormat.R32G32B32A32_Float, TextureDimension.Cube);

                for (var i = 0; i < 6; i++)
                {
                    var faceImage = image.Clone(i * image.Height, 0, (i + 1) * image.Height, image.Height);
                    faceImage.SaveToFile(ImageFormat.EXR, $"Master{i}.hdr");

                    var faceMipMaps = new List<Surface>();
                    faceImage.GenerateMipMaps(faceMipMaps, ImageFilter.CatmullRom);

                    var mipChainItem = new MipChain();
                    compressedImage.MipChains.Add(mipChainItem);

                    var counter = 0;
                    foreach (var mipLevel in faceMipMaps)
                    {
                        Logger.WriteMessage($"{mipLevel.Width} {mipLevel.Height}");
                        mipChainItem.Add(new MipData(mipLevel.Width, mipLevel.Height, mipLevel.Pitch, mipLevel.DataPtr));
                        mipLevel.SaveToFile(ImageFormat.EXR, $"MipLevel{i}_{counter}.hdr");

                        counter++;
                    }
                }
            }

            else
            {
                var image = Surface.LoadFromStream(memoryStream);
                image.FlipVertically();
                Logger.WriteMessage($"IsTransparent: {image.IsTransparent}");

                // if (isDiffuse)
                // {
                //     var maskTexturePath = Path.Combine(context.InputDirectory, context.SourceFilename.Replace("_diff", "_mask"));

                //     if (File.Exists(maskTexturePath))
                //     {
                //         Logger.WriteMessage($"Loading mask image {maskTexturePath}");
                //         var maskTexture = Surface.LoadFromFile(maskTexturePath);
                //         maskTexture.FlipVertically();
                //         maskTexture.ConvertTo(ImageConversion.ToGreyscale);

                //         image.ConvertTo(ImageConversion.To32Bits);

                //         var imagePixelSize = image.BitsPerPixel / 8;
                //         Logger.WriteMessage($"Loading mask image {imagePixelSize}");

                //         var maskPixelSize = maskTexture.BitsPerPixel / 8;
                //         Logger.WriteMessage($"Loading mask image {maskPixelSize} - {maskTexture.ColorType} - {maskTexture.PaletteColorCount}");

                //         var newTexture = new Surface(image.Width, image.Height);
                //         var newImagePixelSize = newTexture.BitsPerPixel / 8;

                //         var newImageData = new Span<byte>(newTexture.DataPtr.ToPointer(), newImagePixelSize * newTexture.Width * newTexture.Height);
                //         var imageData = new Span<byte>(image.DataPtr.ToPointer(), imagePixelSize * image.Width * image.Height);
                //         var maskData = new Span<byte>(maskTexture.DataPtr.ToPointer(), maskPixelSize * maskTexture.Width * maskTexture.Height);

                //         for (int i = 0, j = 0, k = 0; i < newTexture.Width * newTexture.Height * newImagePixelSize; i += newImagePixelSize, j+= imagePixelSize, k += maskPixelSize)
                //         {
                //             newImageData[i] = imageData[j];
                //             newImageData[i + 1] = imageData[j + 1];
                //             newImageData[i + 2] = imageData[j + 2];
                //             newImageData[i + 3] = maskData[k];
                //         }

                //         image = newTexture;
                //     }
                // }

                using var compressor = new Compressor();
                compressor.Input.GenerateMipmaps = true;
                compressor.Input.MipmapFilter = MipmapFilter.Kaiser;
                compressor.Input.SetData(image);

                if (image.IsTransparent)
                {
                    compressor.Input.AlphaMode = AlphaMode.Transparency;
                }

                if (isNormalMap)
                {
                    compressor.Input.IsNormalMap = true;
                }

                compressor.Compression.Format = isNormalMap ? CompressionFormat.BC5 : (isBumpMap ? CompressionFormat.BC4 : (isHdr ? CompressionFormat.BC6 : CompressionFormat.BC3));
                compressor.Output.OutputFileFormat = OutputFileFormat.DDS10;
                compressor.Output.IsSRGBColorSpace = true;

                compressor.Process(out compressedImage);

                if (compressedImage != null)
                {
                    Logger.WriteMessage("OK");
                }

                else
                {
                    Logger.WriteMessage($"ERROR: {compressor.LastErrorString}");
                }
            }

            Logger.WriteMessage($"Texture compiler (Width: {compressedImage.MipChains[0][0].Width}, Height: {compressedImage.MipChains[0][0].Height})");

            var destinationMemoryStream = new MemoryStream();

            using var streamWriter = new BinaryWriter(destinationMemoryStream);
            streamWriter.Write(new char[] { 'T', 'E', 'X', 'T', 'U', 'R', 'E' });
            streamWriter.Write(version);
            streamWriter.Write(compressedImage.MipChains[0][0].Width);
            streamWriter.Write(compressedImage.MipChains[0][0].Height);
            streamWriter.Write(isNormalMap ? (int)TextureFormat.BC5 : isBumpMap ? (int)TextureFormat.BC4 : isCubeMap ? (int)TextureFormat.Rgba32Float : (int)TextureFormat.BC3Srgb);

            var faceCount = compressedImage.MipChains.Count;
            var mipLevels = compressedImage.MipChains[0].Count;

            Logger.WriteMessage($"Face Count: {faceCount}");
            Logger.WriteMessage($"Mip Levels: {mipLevels}");
            streamWriter.Write(faceCount);
            streamWriter.Write(mipLevels);

            for (var i = 0; i < faceCount; i++)
            {
                for (var j = 0; j < mipLevels; j++)
                {
                    var mipData = compressedImage.MipChains[i][j];
                    var mipRawData = new Span<byte>(mipData.Data.ToPointer(), mipData.SizeInBytes);

                    streamWriter.Write(mipData.SizeInBytes);
                    streamWriter.Write(mipRawData);
                }
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