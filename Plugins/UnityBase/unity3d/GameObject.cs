using System;
using System.Collections.Generic;
using System.IO;

using SB3Utility;

namespace UnityPlugin
{
	public interface LinkedByGameObject : Component
	{
		PPtr<GameObject> m_GameObject { get; set; }
	}

	public class GameObject : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public List<KeyValuePair<UnityClassID, PPtr<Component>>> m_Component { get; set; }
		public uint m_Layer { get; set; }
		public string m_Name { get; set; }
		public UInt16 m_Tag { get; set; }
		public bool m_isActive { get; set; }

		public GameObject(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public GameObject(AssetCabinet file) :
			this(file, 0, UnityClassID.GameObject, UnityClassID.GameObject)
		{
			file.ReplaceSubfile(-1, this, null);
			m_Component = new List<KeyValuePair<UnityClassID, PPtr<Component>>>(1);
			m_Layer = 20;
			m_isActive = true;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numComponents = reader.ReadInt32();
			m_Component = new List<KeyValuePair<UnityClassID, PPtr<Component>>>(numComponents);
			for (int i = 0; i < numComponents; i++)
			{
				m_Component.Add
				(
					new KeyValuePair<UnityClassID, PPtr<Component>>
					(
						(UnityClassID)reader.ReadInt32(),
						new PPtr<Component>(stream, file)
					)
				);
			}

			m_Layer = reader.ReadUInt32();
			m_Name = reader.ReadNameA4();
			m_Tag = reader.ReadUInt16();
			m_isActive = reader.ReadBoolean();
		}

		public static string LoadName(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numComponents = reader.ReadInt32();
			for (int i = 0; i < numComponents; i++)
			{
				reader.ReadInt32();
				new PPtr<Component>(stream);
			}

			reader.ReadUInt32();
			return reader.ReadNameA4();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_Component.Count);
			for (int i = 0; i < m_Component.Count; i++)
			{
				writer.Write((int)m_Component[i].Key);
				file.WritePPtr(m_Component[i].Value.asset, false, stream);
			}

			writer.Write(m_Layer);
			writer.WriteNameA4(m_Name);
			writer.Write(m_Tag);
			writer.Write(m_isActive);
		}

		public dynamic FindLinkedComponent(UnityClassID classID)
		{
			for (int i = 0; i < m_Component.Count; i++)
			{
				if (m_Component[i].Value.asset != null && m_Component[i].Value.asset.classID1 == classID)
				{
					return m_Component[i].Value.asset;
				}
			}
			return null;
		}

		public void AddLinkedComponent(LinkedByGameObject asset)
		{
			m_Component.Add(new KeyValuePair<UnityClassID, PPtr<Component>>(asset.classID1, new PPtr<Component>(asset)));
			asset.m_GameObject = new PPtr<GameObject>(this);
		}

		public void RemoveLinkedComponent(LinkedByGameObject asset)
		{
			for (int i = 0; i < m_Component.Count; i++)
			{
				if (m_Component[i].Value.asset == asset)
				{
					m_Component.RemoveAt(i);
					break;
				}
			}
			if (m_Component.Count == 0)
			{
				file.RemoveSubfile(this);
			}
			asset.m_GameObject = new PPtr<GameObject>((LinkedByGameObject)null);
		}

		public void UpdateComponentRef(Component component)
		{
			for (int i = 0; i < m_Component.Count; i++)
			{
				if (m_Component[i].Value == component)
				{
					return;
				}
				if (m_Component[i].Value.asset.pathID == component.pathID)
				{
					var newRef =
						new KeyValuePair<UnityClassID, PPtr<Component>>
						(
							m_Component[i].Key,
							new PPtr<Component>(component)
						);
					m_Component.RemoveAt(i);
					m_Component.Insert(i, newRef);
					return;
				}
			}
		}
	}
}
