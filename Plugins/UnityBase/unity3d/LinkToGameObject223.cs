using System;
using System.Collections.Generic;
using System.IO;

namespace UnityPlugin
{
	public class LinkToGameObject223 : Component, LinkedByGameObject, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public PPtr<GameObject> m_GameObject { get; set; }
		public byte[] Data { get; set; }

		public LinkToGameObject223(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public LinkToGameObject223(AssetCabinet file) :
			this(file, 0, UnityClassID.LinkToGameObject223, UnityClassID.LinkToGameObject223)
		{
			file.ReplaceSubfile(-1, this, null);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_GameObject = new PPtr<GameObject>(stream, file);
			Data = reader.ReadBytes(30);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			m_GameObject.WriteTo(stream);
			writer.Write(Data);
		}
	}
}
