using System;
using System.Collections.Generic;
using System.IO;

using SB3Utility;

namespace UnityPlugin
{
	public class Shader : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public string m_Script { get; set; }
		public string m_PathName { get; set; }
		public List<PPtr<Shader>> m_Dependencies { get; set; }
		public bool m_ShaderIsBaked { get; set; }

		public Shader(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();
			m_Script = reader.ReadNameA4();
			m_PathName = reader.ReadNameA4();

			int numDependencies = reader.ReadInt32();
			m_Dependencies = new List<PPtr<Shader>>(numDependencies);
			for (int i = 0; i < numDependencies; i++)
			{
				m_Dependencies.Add(new PPtr<Shader>(stream, file));
			}

			m_ShaderIsBaked = reader.ReadBoolean();
			reader.ReadBytes(3);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			writer.WriteNameA4(m_Script);
			writer.WriteNameA4(m_PathName);

			writer.Write(m_Dependencies.Count);
			for (int i = 0; i < m_Dependencies.Count; i++)
			{
				file.WritePPtr(m_Dependencies[i].asset, !(m_Dependencies[i].asset is Shader), stream);
			}

			writer.Write(m_ShaderIsBaked);
			writer.Write(new byte[3]);
		}
	}
}