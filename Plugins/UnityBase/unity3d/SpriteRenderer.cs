using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public class SpriteRenderer : MeshRenderer, Component
	{
		public PPtr<Sprite> m_Sprite { get; set; }
		public Color4 m_Color { get; set; }

		public SpriteRenderer(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
			: base(file, pathID, classID1, classID2) { }

		public SpriteRenderer(AssetCabinet file) :
			this(file, 0, UnityClassID.SpriteRenderer, UnityClassID.SpriteRenderer)
		{
			file.ReplaceSubfile(-1, this, null);

			base.SetDefaults();
		}

		public new void LoadFrom(Stream stream)
		{
			base.LoadFrom(stream);

			BinaryReader reader = new BinaryReader(stream);
			m_Sprite = new PPtr<Sprite>(stream, file);
			m_Color = reader.ReadColor4();
		}

		public new void WriteTo(Stream stream)
		{
			base.WriteTo(stream);

			BinaryWriter writer = new BinaryWriter(stream);
			m_Sprite.WriteTo(stream);
			writer.Write(m_Color);
		}
	}
}
