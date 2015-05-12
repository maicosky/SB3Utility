using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using SB3Utility;

namespace UnityPlugin
{
	public class Cubemap : Texture2D, Component, StoresReferences
	{
		public List<PPtr<Texture2D>> m_SourceTextures { get; set; }

		public Cubemap(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
			: base(file, pathID, classID1, classID2) { }

		public Cubemap(AssetCabinet file) : base(file)
		{
			classID1 = classID2 = UnityClassID.Cubemap;
			m_SourceTextures = new List<PPtr<Texture2D>>();
		}

		public new void LoadFrom(Stream stream)
		{
			base.LoadFrom(stream);

			BinaryReader reader = new BinaryReader(stream);
			int numTextures = reader.ReadInt32();
			m_SourceTextures = new List<PPtr<Texture2D>>(numTextures);
			for (int i = 0; i < numTextures; i++)
			{
				m_SourceTextures.Add(new PPtr<Texture2D>(stream));
			}
		}

		public new void WriteTo(Stream stream)
		{
			base.WriteTo(stream);

			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_SourceTextures.Count);
			for (int i = 0; i < m_SourceTextures.Count; i++)
			{
				m_SourceTextures[i].WriteTo(stream);
			}
		}

		public new Cubemap Clone(AssetCabinet file)
		{
			Component cubemap = file.Bundle.FindComponent(m_Name, UnityClassID.Cubemap);
			if (cubemap == null)
			{
				file.MergeTypeDefinition(this.file, UnityClassID.Cubemap);

				Cubemap clone = new Cubemap(file);
				file.Bundle.AddComponent(m_Name, clone);
				CopyAttributesTo(clone);
				CopyImageTo(clone);
				foreach (PPtr<Texture2D> texPtr in m_SourceTextures)
				{
					if (texPtr.asset != null)
					{
						clone.m_SourceTextures.Add(new PPtr<Texture2D>(texPtr.instance.Clone(file)));
					}
				}
				return clone;
			}
			else if (cubemap is NotLoaded)
			{
				NotLoaded notLoaded = (NotLoaded)cubemap;
				if (notLoaded.replacement != null)
				{
					cubemap = notLoaded.replacement;
				}
				else
				{
					cubemap = file.LoadComponent(file.SourceStream, notLoaded);
				}
			}
			return (Cubemap)cubemap;
		}
	}
}
