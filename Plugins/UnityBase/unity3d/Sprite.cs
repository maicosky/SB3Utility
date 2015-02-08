using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public class Rectf : IObjInfo
	{
		public float x { get; set; }
		public float y { get; set; }
		public float width { get; set; }
		public float height { get; set; }

		public Rectf(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			x = reader.ReadSingle();
			y = reader.ReadSingle();
			width = reader.ReadSingle();
			height = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(x);
			writer.Write(y);
			writer.Write(width);
			writer.Write(height);
		}
	}

	public class SpriteVertex : IObjInfo
	{
		public Vector3 pos { get; set; }

		public SpriteVertex(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			pos = reader.ReadVector3();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(pos);
		}
	}

	public class SpriteRenderData : IObjInfo
	{
		public PPtr<Texture2D> texture { get; set; }
		public SpriteVertex[] vertices { get; set; }
		public ushort[] indices { get; set; }
		public Rectf textureRect { get; set; }
		public Vector2 textureRectOffset { get; set; }
		public uint settingsRaw { get; set; }
		public Vector4 uvTransform { get; set; }

		private AssetCabinet file;

		public SpriteRenderData(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			texture = new PPtr<Texture2D>(stream, file);

			int numVertices = reader.ReadInt32();
			vertices = new SpriteVertex[numVertices];
			for (int i = 0; i < numVertices; i++)
			{
				vertices[i] = new SpriteVertex(stream);
			}

			int numIndices = reader.ReadInt32();
			indices = reader.ReadUInt16Array(numIndices);
			if ((numIndices & 1) > 0)
			{
				reader.ReadBytes(2);
			}

			textureRect = new Rectf(stream);
			textureRectOffset = reader.ReadVector2();
			settingsRaw = reader.ReadUInt32();
			uvTransform = reader.ReadVector4();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			file.WritePPtr(texture.asset, false, stream);
			
			writer.Write(vertices.Length);
			for (int i = 0; i < vertices.Length; i++)
			{
				vertices[i].WriteTo(stream);
			}

			writer.Write(indices.Length);
			writer.Write(indices);
			if ((indices.Length & 1) > 0)
			{
				writer.Write((ushort)0);
			}

			textureRect.WriteTo(stream);
			writer.Write(textureRectOffset);
			writer.Write(settingsRaw);
			writer.Write(uvTransform);
		}
	}

	public class Sprite : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public Rectf m_Rect { get; set; }
		public Vector2 m_Offset { get; set; }
		public Vector4 m_Border { get; set; }
		public float m_PixelsToUnits { get; set; }
		public uint m_Extrude { get; set; }
		public SpriteRenderData m_RD { get; set; }

		public Sprite(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public Sprite(AssetCabinet file) :
			this(file, 0, UnityClassID.Sprite, UnityClassID.Sprite)
		{
			file.ReplaceSubfile(-1, this, null);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();
			m_Rect = new Rectf(stream);
			m_Offset = reader.ReadVector2();
			m_Border = reader.ReadVector4();
			m_PixelsToUnits = reader.ReadSingle();
			m_Extrude = reader.ReadUInt32();
			m_RD = new SpriteRenderData(file, stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			m_Rect.WriteTo(stream);
			writer.Write(m_Offset);
			writer.Write(m_Border);
			writer.Write(m_PixelsToUnits);
			writer.Write(m_Extrude);
			m_RD.WriteTo(stream);
		}
	}
}
