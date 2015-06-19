using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;
using SlimDX.Direct3D9;

using SB3Utility;

namespace UnityPlugin
{
	public enum TextureFormat
	{
		Alpha8 = 1,
		ARGB4444,
		RGB24,
		RGBA32,
		ARGB32,
		RGB565 = 7,
		DXT1 = 10,
		DXT5 = 12,
		RGBA4444,
		WiiI4 = 20,
		WiiI8,
		WiiIA4,
		WiiIA8,
		WiiRGB565,
		WiiRGB5A3,
		WiiRGBA8,
		WiiCMPR,
		PVRTC_RGB2 = 30,
		PVRTC_RGBA2,
		PVRTC_RGB4,
		PVRTC_RGBA4,
		ETC_RGB4,
		ATC_RGB4,
		ATC_RGBA8,
		BGRA32,
		ATF_RGB_DXT1,
		ATF_RGBA_JPG,
		ATF_RGB_JPG,
		EAC_R,
		EAC_R_SIGNED,
		EAC_RG,
		EAC_RG_SIGNED,
		ETC2_RGB4,
		ETC2_RGB4_PUNCHTHROUGH_ALPHA,
		ETC2_RGBA8,
		ASTC_RGB_4x4,
		ASTC_RGB_5x5,
		ASTC_RGB_6x6,
		ASTC_RGB_8x8,
		ASTC_RGB_10x10,
		ASTC_RGB_12x12,
		ASTC_RGBA_4x4,
		ASTC_RGBA_5x5,
		ASTC_RGBA_6x6,
		ASTC_RGBA_8x8,
		ASTC_RGBA_10x10,
		ASTC_RGBA_12x12
	}

	public class GLTextureSettings : IObjInfo
	{
		public int m_FilterMode;
		public float m_MipBias;
		public int m_Aniso;
		public int m_WrapMode;

		public GLTextureSettings()
		{
			m_FilterMode = 1;
			m_MipBias = 0;
			m_Aniso = 0;
			m_WrapMode = 0;
		}

		public GLTextureSettings(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_FilterMode = reader.ReadInt32();
			m_MipBias = reader.ReadSingle();
			m_Aniso = reader.ReadInt32();
			m_WrapMode = reader.ReadInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_FilterMode);
			writer.Write(m_MipBias);
			writer.Write(m_Aniso);
			writer.Write(m_WrapMode);
		}

		public void CopyTo(GLTextureSettings dest)
		{
			dest.m_FilterMode = m_FilterMode;
			dest.m_MipBias = m_MipBias;
			dest.m_Aniso = m_Aniso;
			dest.m_WrapMode = m_WrapMode;
		}
	}

	public class Texture2D : Component
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public int m_Width { get; set; }
		public int m_Height { get; set; }
		public int m_CompleteImageSize { get; set; }
		public TextureFormat m_TextureFormat { get; set; }
		public bool m_MipMap { get; set; }
		public bool m_isReadable { get; set; }
		public bool m_ReadAllowed { get; set; }
		public int m_ImageCount { get; set; }
		public int m_TextureDimension { get; set; }
		public GLTextureSettings m_TextureSettings { get; set; }
		public int m_LightmapFormat { get; set; }
		public int m_ColorSpace { get; set; }
		public byte[] image_data { get; set; }

		public Texture2D(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public Texture2D(AssetCabinet file) :
			this(file, 0, UnityClassID.Texture2D, UnityClassID.Texture2D)
		{
			file.ReplaceSubfile(-1, this, null);
			file.Parser.Textures.Add(this);
			m_TextureSettings = new GLTextureSettings();
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();
			m_Width = reader.ReadInt32();
			m_Height = reader.ReadInt32();
			m_CompleteImageSize = reader.ReadInt32();
			m_TextureFormat = (TextureFormat)reader.ReadInt32();
			m_MipMap = reader.ReadBoolean();
			m_isReadable = reader.ReadBoolean();
			m_ReadAllowed = reader.ReadBoolean();
			reader.ReadByte();
			m_ImageCount = reader.ReadInt32();
			m_TextureDimension = reader.ReadInt32();
			m_TextureSettings = new GLTextureSettings(stream);
			m_LightmapFormat = reader.ReadInt32();
			m_ColorSpace = reader.ReadInt32();
			int size = reader.ReadInt32();
			image_data = reader.ReadBytes(size);
			reader.ReadBytes(4 - size & 3);
		}

		public static string LoadName(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			return reader.ReadNameA4();
		}

		public void LoadFrom(ImportedTexture tex)
		{
			m_Name = Path.GetFileNameWithoutExtension(tex.Name);

			Device dev = new Device(new Direct3D(), 0, DeviceType.Hardware, new IntPtr(), CreateFlags.SoftwareVertexProcessing, new PresentParameters());
			ImageInformation imageInfo;
			using (Texture renderTexture = Texture.FromMemory(dev, tex.Data, 0, 0, -1, Usage.None, Format.Unknown, Pool.Managed, Filter.Default, Filter.Default, 0, out imageInfo))
			{
				m_Width = imageInfo.Width;
				m_Height = imageInfo.Height;

				TextureFormat tf;
				if (TextureFormat.TryParse(imageInfo.Format.ToString(), true, out tf))
				{
					m_TextureFormat = tf;
				}
				else if (imageInfo.Format == Format.R8G8B8)
				{
					m_TextureFormat = TextureFormat.RGB24;
				}
				else if (imageInfo.Format == Format.A8R8G8B8)
				{
					m_TextureFormat = TextureFormat.ARGB32;
				}
				else
				{
					throw new Exception("Unknown format " + imageInfo.Format);
				}

				m_MipMap = imageInfo.MipLevels > 1;
				m_ReadAllowed = true;
				m_ImageCount = 1;
				m_TextureDimension = 2;
				m_TextureSettings = new GLTextureSettings();
				m_ColorSpace = 1;

				int bytesPerPixel = 0, originY = 0;
				using (BinaryReader reader = new BinaryReader(new MemoryStream(tex.Data)))
				{
					switch (m_TextureFormat)
					{
					case TextureFormat.DXT1:
					case TextureFormat.DXT5:
						reader.BaseStream.Position = 0x80;
						m_CompleteImageSize = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
						break;
					case TextureFormat.RGB24:
					case TextureFormat.ARGB32:
						reader.BaseStream.Position = 0x0A;
						originY = reader.ReadInt16();
						reader.BaseStream.Position = 0x12;
						bytesPerPixel = m_TextureFormat == TextureFormat.RGB24 ? 3 : 4;
						m_CompleteImageSize = m_Width * m_Height * bytesPerPixel;
						break;
					}
					image_data = reader.ReadBytes(m_CompleteImageSize);
				}
				switch (m_TextureFormat)
				{
				case TextureFormat.RGB24:
					for (int i = 0, j = 2; j < m_CompleteImageSize; i += 3, j += 3)
					{
						byte b = image_data[j];
						image_data[j] = image_data[i];
						image_data[i] = b;
					}
					break;
				case TextureFormat.ARGB32:
					for (int i = 0, j = 3, k = 1, l = 2; j < m_CompleteImageSize; i += 4, j += 4, k += 4, l += 4)
					{
						byte b = image_data[j];
						image_data[j] = image_data[i];
						image_data[i] = b;
						b = image_data[l];
						image_data[l] = image_data[k];
						image_data[k] = b;
					}
					break;
				}
				if (bytesPerPixel > 0 && originY > 0)
				{
					for (int srcIdx = 0, dstIdx = (originY - 1) * m_Width * bytesPerPixel; srcIdx < dstIdx; srcIdx += m_Width * bytesPerPixel, dstIdx -= m_Width * bytesPerPixel)
					{
						for (int i = 0; i < m_Width * bytesPerPixel; i++)
						{
							byte b = image_data[srcIdx + i];
							image_data[srcIdx + i] = image_data[dstIdx + i];
							image_data[dstIdx + i] = b;
						}
					}
				}
			}
			dev.Direct3D.Dispose();
			dev.Dispose();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			writer.Write(m_Width);
			writer.Write(m_Height);
			writer.Write(m_CompleteImageSize);
			writer.Write((int)m_TextureFormat);
			writer.Write(m_MipMap);
			writer.Write(m_isReadable);
			writer.Write(m_ReadAllowed);
			writer.Write((byte)0);
			writer.Write(m_ImageCount);
			writer.Write(m_TextureDimension);
			m_TextureSettings.WriteTo(stream);
			writer.Write(m_LightmapFormat);
			writer.Write(m_ColorSpace);
			writer.Write(image_data.Length);
			writer.Write(image_data);
			writer.Write(new byte[4 - image_data.Length & 3]);
		}

		public Texture2D Clone(AssetCabinet file)
		{
			Component tex = file.Bundle != null
				? file.Bundle.FindComponent(m_Name, UnityClassID.Texture2D)
				: file.Parser.GetTexture(m_Name);
			if (tex == null)
			{
				file.MergeTypeDefinition(this.file, UnityClassID.Texture2D);

				tex = new Texture2D(file);
				if (file.Bundle != null)
				{
					file.Bundle.AddComponent(m_Name, tex);
				}
				CopyAttributesTo((Texture2D)tex);
				CopyImageTo((Texture2D)tex);
			}
			else if (tex is NotLoaded)
			{
				NotLoaded notLoaded = (NotLoaded)tex;
				if (notLoaded.replacement != null)
				{
					tex = notLoaded.replacement;
				}
				else
				{
					tex = file.LoadComponent(file.SourceStream, notLoaded);
				}
			}
			return (Texture2D)tex;
		}

		public void CopyAttributesTo(Texture2D dst)
		{
			dst.m_MipMap = m_MipMap;
			dst.m_isReadable = m_isReadable;
			dst.m_ReadAllowed = m_ReadAllowed;
			m_TextureSettings.CopyTo(dst.m_TextureSettings);
			dst.m_LightmapFormat = m_LightmapFormat;
			dst.m_ColorSpace = m_ColorSpace;
		}

		public void CopyImageTo(Texture2D dst)
		{
			dst.m_Name = m_Name;
			dst.m_Width = m_Width;
			dst.m_Height = m_Height;
			dst.m_CompleteImageSize = m_CompleteImageSize;
			dst.m_TextureFormat = m_TextureFormat;
			dst.m_ImageCount = m_ImageCount;
			dst.m_TextureDimension = m_TextureDimension;
			dst.image_data = (byte[])image_data.Clone();
		}

		public void Export(string path)
		{
			DirectoryInfo dirInfo = new DirectoryInfo(path);
			if (!dirInfo.Exists)
			{
				dirInfo.Create();
			}

			string extension = m_TextureFormat == TextureFormat.DXT1 || m_TextureFormat == TextureFormat.DXT5 ? ".dds" : ".tga";
			using (Stream stream = File.OpenWrite(path + "\\" + m_Name + extension))
			{
				Export(stream);
			}
		}

		public void Export(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			byte[] buffer = null;
			switch (m_TextureFormat)
			{
			case TextureFormat.DXT1:
			case TextureFormat.DXT5:
				byte[] dds_header = DDS.CreateHeader(m_Width, m_Height, 32, m_MipMap ? 2 : 0,
					m_TextureFormat == TextureFormat.DXT1
					? (byte)'D' | ((byte)'X' << 8) | ((byte)'T' << 16) | ((byte)'1' << 24)
					: (byte)'D' | ((byte)'X' << 8) | ((byte)'T' << 16) | ((byte)'5' << 24));
				writer.Write(dds_header);
				buffer = image_data;
				break;
			case TextureFormat.RGB24:
				byte[] tga_header = new byte[0x12]
				{
					0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0,
					(byte)m_Width, (byte)(m_Width >> 8), (byte)m_Height, (byte)(m_Height >> 8), 24, 0
				};
				writer.Write(tga_header);
				buffer = new byte[image_data.Length];
				for (int i = 0, j = 2; j < m_CompleteImageSize; i += 3, j += 3)
				{
					byte b = image_data[j];
					buffer[j] = image_data[i];
					buffer[i] = b;
					buffer[i + 1] = image_data[i + 1];
				}
				break;
			case TextureFormat.ARGB32:
				tga_header = new byte[0x12]
				{
					0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0,
					(byte)m_Width, (byte)(m_Width >> 8), (byte)m_Height, (byte)(m_Height >> 8), 32, 0
				};
				writer.Write(tga_header);
				buffer = new byte[image_data.Length];
				for (int i = 0, j = 3, k = 1, l = 2; j < m_CompleteImageSize; i += 4, j += 4, k += 4, l += 4)
				{
					byte b = image_data[j];
					buffer[j] = image_data[i];
					buffer[i] = b;
					b = image_data[l];
					buffer[l] = image_data[k];
					buffer[k] = b;
				}
				break;
			case TextureFormat.Alpha8:
				tga_header = new byte[0x12]
				{
					0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0,
					(byte)m_Width, (byte)(m_Width >> 8), (byte)m_Height, (byte)(m_Height >> 8), 8, 0
				};
				writer.Write(tga_header);
				buffer = (byte[])image_data.Clone();
				break;
			default:
				throw new Exception("Unhandled Texture2D format: " + m_TextureFormat);
			}
			writer.Write(buffer);
		}

		static class DDS
		{
			enum DDS_HEADER_FLAGS
			{
				DDSD_CAPS			= 0x00000001,
				DDSD_HEIGHT			= 0x00000002,
				DDSD_WIDTH			= 0x00000004,
				DDSD_PITCH			= 0x00000008,
				DDSD_PIXELFORMAT	= 0x00001000,
				DDSD_MIPMAPCOUNT	= 0x00020000,
				DDSD_LINEARSIZE		= 0x00080000,
				DDSD_DEPTH			= 0x00800000
			}
			enum DDS_PIXEL_FORMAT
			{
				DDPF_ALPHAPIXELS	= 0x00000001,
				DDPF_FOURCC			= 0x00000004,
				DDPF_RGB			= 0x00000040,
			}
			enum DDS_CAPS
			{
				DDSCAPS_COMPLEX		= 0x00000008,
				DDSCAPS_TEXTURE		= 0x00001000,
				DDSCAPS_MIPMAP		= 0x00400000,
				DDSCAPS2_CUBEMAP			= 0x00000200,
				DDSCAPS2_CUBEMAP_POSITIVEX	= 0x00000400,
				DDSCAPS2_CUBEMAP_NEGATIVEX	= 0x00000800,
				DDSCAPS2_CUBEMAP_POSITIVEY	= 0x00001000,
				DDSCAPS2_CUBEMAP_NEGATIVEY	= 0x00002000,
				DDSCAPS2_CUBEMAP_POSITIVEZ	= 0x00004000,
				DDSCAPS2_CUBEMAP_NEGATIVEZ	= 0x00008000,
				DDSCAPS2_VOLUME				= 0x00200000
			}

			public static byte[] CreateHeader(int width, int height, int rgbBitCount, int mipMaps, int textureFormat)
			{
				uint[] header = new uint[32];
				header[0] = (byte)'D' | ((byte)'D' << 8) | ((byte)'S' << 16) | ((byte)' ' << 24);
				header[1] = 124; // sizeof DDS_HEADER
				header[2] = (uint)(DDS_HEADER_FLAGS.DDSD_CAPS | DDS_HEADER_FLAGS.DDSD_HEIGHT | DDS_HEADER_FLAGS.DDSD_WIDTH | DDS_HEADER_FLAGS.DDSD_PIXELFORMAT | DDS_HEADER_FLAGS.DDSD_LINEARSIZE | (mipMaps > 0 ? DDS_HEADER_FLAGS.DDSD_MIPMAPCOUNT : 0));
				header[3] = (uint)height;
				header[4] = (uint)width;
				header[5] = (uint)Math.Max(1, ((width + 3) / 4)) * 2048/*block_size*/;
				header[7] = (uint)mipMaps;

				header[19] = 32; // sizeof DDS_PIXELFORMAT
				header[20] = (uint)DDS_PIXEL_FORMAT.DDPF_FOURCC;
				header[21] = (uint)textureFormat;
				header[22] = (uint)rgbBitCount;
				header[23] = 0x00ff0000;
				header[24] = 0x0000ff00;
				header[25] = 0x000000ff;
				header[26] = rgbBitCount == 32 ? 0xff000000 : 0;

				header[27] = (uint)(DDS_CAPS.DDSCAPS_COMPLEX | DDS_CAPS.DDSCAPS_TEXTURE | DDS_CAPS.DDSCAPS_MIPMAP);

				return ConvertToByteArray(header);
			}

			private static byte[] ConvertToByteArray(uint[] array)
			{
				byte[] result = new byte[array.Length * 4];
				using (DataStream ds = new DataStream(result, true, true))
				{
					ds.WriteRange(array);
				}
				return result;
			}
		}
	}
}
