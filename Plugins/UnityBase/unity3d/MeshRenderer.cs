using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public class MeshRenderer : Component, LinkedByGameObject, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public PPtr<GameObject> m_GameObject { get; set; }
		public bool m_Enabled { get; set; }
		public bool m_CastShadows { get; set; }
		public bool m_ReceiveShadows { get; set; }
		public byte m_LightmapIndex { get; set; }
		public Vector4 m_LightmapTilingOffset { get; set; }
		public List<PPtr<Material>> m_Materials { get; set; }
		public uint[] m_SubsetIndices { get; set; }
		public PPtr<Transform> m_StaticBatchRoot { get; set; }
		public bool m_UseLightProbes { get; set; }
		public PPtr<Transform> m_LightProbeAnchor { get; set; }
		public uint m_SortingLayerID { get; set; }
		public short m_SortingOrder { get; set; }

		public MeshRenderer(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public MeshRenderer(AssetCabinet file) :
			this(file, 0, UnityClassID.MeshRenderer, UnityClassID.MeshRenderer)
		{
			file.ReplaceSubfile(-1, this, null);

			SetDefaults();
		}

		protected void SetDefaults()
		{
			m_Enabled = true;
			m_CastShadows = true;
			m_ReceiveShadows = true;
			m_LightmapIndex = 255;
			m_LightmapTilingOffset = new Vector4(1, 1, 0, 0);
			m_Materials = new List<PPtr<Material>>(1);
			m_SubsetIndices = new uint[0];
			m_StaticBatchRoot = new PPtr<Transform>((Component)null);
			m_LightProbeAnchor = new PPtr<Transform>((Component)null);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_GameObject = new PPtr<GameObject>(stream, file);
			m_Enabled = reader.ReadBoolean();
			m_CastShadows = reader.ReadBoolean();
			m_ReceiveShadows = reader.ReadBoolean();
			m_LightmapIndex = reader.ReadByte();
			m_LightmapTilingOffset = reader.ReadVector4();

			int numMaterials = reader.ReadInt32();
			m_Materials = new List<PPtr<Material>>(numMaterials);
			for (int i = 0; i < numMaterials; i++)
			{
				m_Materials.Add(new PPtr<Material>(stream, file));
			}

			int numSubsetIndices = reader.ReadInt32();
			m_SubsetIndices = reader.ReadUInt32Array(numSubsetIndices);

			m_StaticBatchRoot = new PPtr<Transform>(stream, file);
			m_UseLightProbes = reader.ReadBoolean();
			reader.ReadBytes(3);
			m_LightProbeAnchor = new PPtr<Transform>(stream, file);
			m_SortingLayerID = reader.ReadUInt32();
			m_SortingOrder = reader.ReadInt16();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			file.WritePPtr(m_GameObject.asset, false, stream);
			writer.Write(m_Enabled);
			writer.Write(m_CastShadows);
			writer.Write(m_ReceiveShadows);
			writer.Write(m_LightmapIndex);
			writer.Write(m_LightmapTilingOffset);

			writer.Write(m_Materials.Count);
			for (int i = 0; i < m_Materials.Count; i++)
			{
				file.WritePPtr(m_Materials[i].asset, false, stream);
			}

			writer.Write(m_SubsetIndices.Length);
			writer.Write(m_SubsetIndices);

			file.WritePPtr(m_StaticBatchRoot.asset, false, stream);
			writer.Write(m_UseLightProbes);
			writer.Write(new byte[3]);
			file.WritePPtr(m_LightProbeAnchor.asset, false, stream);
			writer.Write(m_SortingLayerID);
			writer.Write(m_SortingOrder);
			writer.Write(new byte[2]);
		}
	}
}
