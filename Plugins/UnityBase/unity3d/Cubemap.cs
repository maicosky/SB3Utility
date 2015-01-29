using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using SB3Utility;

namespace UnityPlugin
{
	public class Cubemap : Component, StoresReferences
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
		public bool m_IsReadable { get; set; }
		public bool m_ReadAllowed { get; set; }
		public int m_ImageCount { get; set; }
		public int m_TextureDimension { get; set; }
		public GLTextureSettings m_TextureSettings { get; set; }
		public int m_LightmapFormat { get; set; }
		public int m_ColorSpace { get; set; }
		public byte[] image_data { get; set; }
		public List<PPtr<Texture2D>> m_SourceTextures { get; set; }

		public Cubemap(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public Cubemap(AssetCabinet file) :
			this(file, 0, UnityClassID.Cubemap, UnityClassID.Cubemap)
		{
			file.ReplaceSubfile(-1, this, null);
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
			m_IsReadable = reader.ReadBoolean();
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

			int numTextures = reader.ReadInt32();
			m_SourceTextures = new List<PPtr<Texture2D>>(numTextures);
			for (int i = 0; i < numTextures; i++)
			{
				m_SourceTextures.Add(new PPtr<Texture2D>(stream));
			}
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
			writer.Write(m_IsReadable);
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

			writer.Write(m_SourceTextures.Count);
			for (int i = 0; i < m_SourceTextures.Count; i++)
			{
				file.WritePPtr(m_SourceTextures[i].asset, false, stream);
			}
		}
	}
}
