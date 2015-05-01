using System;
using System.Collections.Generic;
using System.IO;

using SB3Utility;

namespace UnityPlugin
{
	public class MonoScript : Component
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public int m_ExecutionOrder { get; set; }
		public uint m_PropertiesHash { get; set; }
		public string m_ClassName { get; set; }
		public string m_Namespace { get; set; }
		public string m_AssemblyName { get; set; }
		public bool m_IsEditorScript { get; set; }

		public MonoScript(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public MonoScript(AssetCabinet file) :
			this(file, 0, UnityClassID.MonoScript, UnityClassID.MonoScript)
		{
			file.ReplaceSubfile(-1, this, null);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();
			m_ExecutionOrder = reader.ReadInt32();
			m_PropertiesHash = reader.ReadUInt32();
			m_ClassName = reader.ReadNameA4();
			m_Namespace = reader.ReadNameA4();
			m_AssemblyName = reader.ReadNameA4();
			m_IsEditorScript = reader.ReadBoolean();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			writer.Write(m_ExecutionOrder);
			writer.Write(m_PropertiesHash);
			writer.WriteNameA4(m_ClassName);
			writer.WriteNameA4(m_Namespace);
			writer.WriteNameA4(m_AssemblyName);
			writer.Write(m_IsEditorScript);
		}

		public MonoScript Clone(AssetCabinet file)
		{
			file.MergeTypeDefinition(this.file, UnityClassID.MonoScript);

			MonoScript monos = new MonoScript(file);
			using (MemoryStream mem = new MemoryStream())
			{
				WriteTo(mem);
				mem.Position = 0;
				monos.LoadFrom(mem);
			}
			return monos;
		}
	}
}
