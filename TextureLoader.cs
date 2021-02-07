using System;
using System.IO;
using OpenTK.Graphics.OpenGL;

namespace AssetsLoaders
{
    public static class TextureLoader
    {
        /// <summary>
        /// We assume that textures without alpha use DXT1, with alpha use DXT5; albedo textures are read as SRGB.
        /// Don't forget that texture will be flipped vertically by OpenGL
        /// </summary>
        /// <param name="path"></param>
        /// <param name="textureID"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="SRGB"></param>
        /// <returns></returns>
        public static bool TryLoadDDS(string path, bool SRGB, out int textureID, out int width, out int height)
        {
            textureID = width = height = -1;
            var fsSource = new FileStream(path, FileMode.Open, FileAccess.Read);
            var header = new byte[128];
            fsSource.Read(header, 0, 128);

            // first 4 bytes should be 'DDS '
            // 84-87 bytes should be 'DXTn'
            // BC4U/BC4S/ATI2/BC55/R8G8_B8G8/G8R8_G8B8/UYVY-packed/YUY2-packed formats are unsupported
            if (!(header[0] == 'D' && header[1] == 'D' && header[2] == 'S' && header[3] == ' ' && header[84] == 'D'))
            {
                goto exit;
            }

            height = header[12] | (header[13] << 8) | (header[14] << 16) | (header[15] << 24);
            width = header[16] | (header[17] << 8) | (header[18] << 16) | (header[19] << 24);
            var mipMapCount = header[28] | (header[29] << 8) | (header[30] << 16) | (header[31] << 24);

            InternalFormat format;
            int blockSize;
            switch ((char) header[87])
            {
                case '1': // DXT1
                    format = SRGB ? InternalFormat.CompressedSrgbS3tcDxt1Ext : InternalFormat.CompressedRgbS3tcDxt1Ext;
                    blockSize = 8;
                    break;
                case '3': // DXT3
                    format = SRGB ? InternalFormat.CompressedSrgbAlphaS3tcDxt3Ext : InternalFormat.CompressedRgbaS3tcDxt3Ext;
                    blockSize = 16;
                    break;
                case '5': // DXT5
                    format = SRGB ? InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext : InternalFormat.CompressedRgbaS3tcDxt5Ext;
                    blockSize = 16;
                    break;
                default: goto exit;
            }

            // int32 should be enough
            var fileSize = (int) fsSource.Length;
            var buffer = new byte[fileSize - 128];
            fsSource.Read(buffer, 0, fileSize - 128);

            GL.GenTextures(1, out textureID);
            GL.BindTexture(TextureTarget.Texture2D, textureID);

            unsafe
            {
                fixed (byte* p = buffer)
                {
                    var ptr = (IntPtr) p;

                    for (int i = 0, w = width, h = height, offset = 0; i < mipMapCount; ++i, w /= 2, h /= 2)
                    {
                        // discard any odd mipmaps with 0x1, 0x2 resolutions
                        if (w == 0 || h == 0)
                        {
                            mipMapCount--;
                            continue;
                        }

                        var size = ((w + 3) / 4) * ((h + 3) / 4) * blockSize;
                        GL.CompressedTexImage2D(TextureTarget.Texture2D, i, format, w, h, 0, size, ptr + offset);
                        offset += size;
                    }
                }
            }

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, mipMapCount - 1);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMinFilter.LinearMipmapLinear);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            exit:
            fsSource.Close();
            // false if file is not DDS with DXTn compression
            return textureID != -1;
        }

    }
}
