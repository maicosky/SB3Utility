using System;
using System.Collections.Generic;
using System.IO;

using SB3Utility;

namespace UnityPlugin
{
	public class AssetInfo : IObjInfo
	{
		public int preloadIndex { get; set; }
		public int preloadSize { get; set; }
		public PPtr<Object> asset { get; set; }

		private AssetCabinet file;

		public AssetInfo(AssetCabinet file, Stream stream)
		{
			this.file = file;
			BinaryReader reader = new BinaryReader(stream);
			preloadIndex = reader.ReadInt32();
			preloadSize = reader.ReadInt32();
			PPtr<Object> objPtr = new PPtr<Object>(stream);
			if (objPtr.m_PathID != 0)
			{
				Component comp = file.FindComponent(objPtr.m_PathID);
				asset = new PPtr<Object>(comp);
			}
			else
			{
				asset = objPtr;
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(preloadIndex);
			writer.Write(preloadSize);
			file.WritePPtr(asset.asset, false, stream);
		}
	}

	public class AssetBundleScriptInfo : IObjInfo
	{
		public string className { get; set; }
		public string nameSpace { get; set; }
		public string assemblyName { get; set; }
		public uint hash { get; set; }

		public AssetBundleScriptInfo(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			className = reader.ReadNameA4();
			nameSpace = reader.ReadNameA4();
			assemblyName = reader.ReadNameA4();
			hash = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(className);
			writer.WriteNameA4(nameSpace);
			writer.WriteNameA4(assemblyName);
			writer.Write(hash);
		}
	}

	public class AssetBundle : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public List<PPtr<Object>> m_PreloadTable { get; set; }
		public List<KeyValuePair<string, AssetInfo>> m_Container { get; set; }
		public AssetInfo m_MainAsset { get; set; }
		public AssetBundleScriptInfo[] m_ScriptCompatibility { get; set; }
		public KeyValuePair<int, uint>[] m_ClassCompatibility { get; set; }
		public uint m_RuntimeCompatibility { get; set; }

		public AssetBundle(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
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

			int numObjects = reader.ReadInt32();
			m_PreloadTable = new List<PPtr<Object>>(numObjects);
			for (int i = 0; i < numObjects; i++)
			{
				PPtr<Object> objPtr = new PPtr<Object>(stream);
				Component comp = file.FindComponent(objPtr.m_PathID);
				if (comp == null)
				{
					comp = new NotLoaded();
				}
				PPtr<Object> assetPtr = new PPtr<Object>(comp);
				m_PreloadTable.Add(assetPtr);
			}

			int numAInfos = reader.ReadInt32();
			m_Container = new List<KeyValuePair<string, AssetInfo>>(numAInfos);
			for (int i = 0; i < numAInfos; i++)
			{
				m_Container.Add
				(
					new KeyValuePair<string, AssetInfo>
					(
						reader.ReadNameA4(), new AssetInfo(file, stream)
					)
				);
			}

			m_MainAsset = new AssetInfo(file, stream);

			int numScriptComps = reader.ReadInt32();
			m_ScriptCompatibility = new AssetBundleScriptInfo[numScriptComps];
			for (int i = 0; i < numScriptComps; i++)
			{
				m_ScriptCompatibility[i] = new AssetBundleScriptInfo(stream);
			}

			int numClassComps = reader.ReadInt32();
			m_ClassCompatibility = new KeyValuePair<int, uint>[numClassComps];
			for (int i = 0; i < numClassComps; i++)
			{
				m_ClassCompatibility[i] = new KeyValuePair<int, uint>
				(
					reader.ReadInt32(), reader.ReadUInt32()
				);
			}

			m_RuntimeCompatibility = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);

			writer.Write(m_PreloadTable.Count);
			for (int i = 0; i < m_PreloadTable.Count; i++)
			{
				file.WritePPtr(m_PreloadTable[i].asset, false, stream);
			}

			writer.Write(m_Container.Count);
			for (int i = 0; i < m_Container.Count; i++)
			{
				writer.WriteNameA4(m_Container[i].Key);
				m_Container[i].Value.WriteTo(stream);
			}

			m_MainAsset.WriteTo(stream);

			writer.Write(m_ScriptCompatibility.Length);
			for (int i = 0; i < m_ScriptCompatibility.Length; i++)
			{
				m_ScriptCompatibility[i].WriteTo(stream);
			}

			writer.Write(m_ClassCompatibility.Length);
			for (int i = 0; i < m_ClassCompatibility.Length; i++)
			{
				writer.Write(m_ClassCompatibility[i].Key);
				writer.Write(m_ClassCompatibility[i].Value);
			}

			writer.Write(m_RuntimeCompatibility);
		}

		public void DeleteComponent(int pathID)
		{
			for (int i = 0; i < m_PreloadTable.Count; i++)
			{
				if (m_PreloadTable[i] != null && m_PreloadTable[i].asset.pathID == pathID)
				{
					m_PreloadTable.RemoveAt(i--);
				}
			}

			for (int i = 0; i < m_Container.Count; i++)
			{
				if (m_Container[i].Value.asset.asset.pathID == pathID)
				{
					m_Container.RemoveAt(i--);
				}
			}
		}
	}
}
