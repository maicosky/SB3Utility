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

		public Shader(AssetCabinet file) :
			this(file, 0, UnityClassID.Shader, UnityClassID.Shader)
		{
			file.ReplaceSubfile(-1, this, null);

			m_Dependencies = new List<PPtr<Shader>>();
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

		public Shader Clone(AssetCabinet file)
		{
			HashSet<Component> addedComponents = new HashSet<Component>();
			return Clone(file, addedComponents);
		}

		public Shader Clone(AssetCabinet file, HashSet<Component> addedComponents)
		{
			Component addedAsset = AlreadyAdded(addedComponents, this);
			if (addedAsset != null)
			{
				return (Shader)addedAsset;
			}

			Shader shader = new Shader(file);
			addedComponents.Add(shader);
			shader.m_Name = m_Name;
			shader.m_Script = m_Script;
			shader.m_PathName = m_PathName;
			foreach (PPtr<Shader> asset in m_Dependencies)
			{
				PPtr<Shader> newObject = null;
				if (asset.m_FileID == 0)
				{
					newObject = new PPtr<Shader>(asset.instance.Clone(file, addedComponents));
				}
				else
				{
					switch (asset.asset.classID1)
					{
					case UnityClassID.Material:
						{
							Material mat = (Material)asset.asset;
							Material clone = mat.Clone(file, addedComponents);
							newObject = new PPtr<Shader>(clone);
							break;
						}
					case UnityClassID.Texture2D:
						{
							Texture2D tex = (Texture2D)asset.asset;
							addedAsset = AlreadyAdded(addedComponents, tex);
							if (addedAsset == null)
							{
								tex = tex.Clone(file);
								addedComponents.Add(tex);
								addedAsset = tex;
							}
							newObject = new PPtr<Shader>(addedAsset);
							break;
						}
					default:
						Report.ReportLog("Shader dependency error : Type " + asset.asset.GetType().ToString());
						continue;
					}
				}
				shader.m_Dependencies.Add(newObject);
			}
			shader.m_ShaderIsBaked = m_ShaderIsBaked;
			return shader;
		}

		Component AlreadyAdded(HashSet<Component> addedComponents, Component newAsset)
		{
			foreach (Component asset in addedComponents)
			{
				if (asset.classID1 == newAsset.classID1)
				{
					switch (asset.classID1)
					{
					case UnityClassID.Shader:
						if (((Shader)asset).m_Name == ((Shader)newAsset).m_Name)
						{
							return asset;
						}
						break;
					case UnityClassID.Texture2D:
						if (((Texture2D)asset).m_Name == ((Texture2D)newAsset).m_Name)
						{
							return asset;
						}
						break;
					}
				}
			}
			return null;
		}
	}
}