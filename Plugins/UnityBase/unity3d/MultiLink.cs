using System;
using System.Collections.Generic;
using System.IO;

using SB3Utility;

namespace UnityPlugin
{
	public class MultiLink : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public PPtr<GameObject> m_GameObject { get; set; }
		public float[] Unknown1 { get; set; }
		public List<PPtr<Object>> Links { get; set; }
		public float[] Unknown2 { get; set; }

		public MultiLink(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public MultiLink(AssetCabinet file) :
			this(file, 0, UnityClassID.MultiLink, UnityClassID.MultiLink)
		{
			file.ReplaceSubfile(-1, this, null);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			long pos = stream.Position;
			m_GameObject = new PPtr<GameObject>(stream, file);
			Unknown1 = reader.ReadSingleArray(10);

			int numLinks = 1 + reader.ReadInt32();
			Links = new List<PPtr<Object>>(numLinks);
			for (int i = 0; i < numLinks; i++)
			{
				Links.Add(new PPtr<Object>(stream, file));
			}

			Unknown2 = reader.ReadSingleArray(10);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			m_GameObject.WriteTo(stream);
			writer.Write(Unknown1);

			writer.Write(Links.Count - 1);
			for (int i = 0; i < Links.Count; i++)
			{
				Links[i].WriteTo(stream);
			}

			writer.Write(Unknown2);
		}
	}
}
