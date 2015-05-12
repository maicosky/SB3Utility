using System;
using System.Collections.Generic;
using System.IO;

using SB3Utility;

namespace UnityPlugin
{
	public class ParticleSystemRenderer : MeshRenderer, Component, LinkedByGameObject, StoresReferences
	{
		public int m_RenderMode { get; set; }
		public float m_MaxParticleSize { get; set; }
		public float m_CameraVelocityScale { get; set; }
		public float m_VelocityScale { get; set; }
		public float m_LengthScale { get; set; }
		public float m_SortingFudge { get; set; }
		public float m_NormalDirection { get; set; }
		public int m_SortMode { get; set; }
		public PPtr<Mesh> m_Mesh { get; set; }
		public PPtr<Mesh> m_Mesh1 { get; set; }
		public PPtr<Mesh> m_Mesh2 { get; set; }
		public PPtr<Mesh> m_Mesh3 { get; set; }

		public ParticleSystemRenderer(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
			: base(file, pathID, classID1, classID2) { }

		public new void LoadFrom(Stream stream)
		{
			base.LoadFrom(stream);

			BinaryReader reader = new BinaryReader(stream);
			m_RenderMode = reader.ReadInt32();
			m_MaxParticleSize = reader.ReadSingle();
			m_CameraVelocityScale = reader.ReadSingle();
			m_VelocityScale = reader.ReadSingle();
			m_LengthScale = reader.ReadSingle();
			m_SortingFudge = reader.ReadSingle();
			m_NormalDirection = reader.ReadSingle();
			m_SortMode = reader.ReadInt32();
			m_Mesh = new PPtr<Mesh>(stream, file);
			m_Mesh1 = new PPtr<Mesh>(stream, file);
			m_Mesh2 = new PPtr<Mesh>(stream, file);
			m_Mesh3 = new PPtr<Mesh>(stream, file);
		}

		public new void WriteTo(Stream stream)
		{
			base.WriteTo(stream);

			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_RenderMode);
			writer.Write(m_MaxParticleSize);
			writer.Write(m_CameraVelocityScale);
			writer.Write(m_VelocityScale);
			writer.Write(m_LengthScale);
			writer.Write(m_SortingFudge);
			writer.Write(m_NormalDirection);
			writer.Write(m_SortMode);
			m_Mesh.WriteTo(stream);
			m_Mesh1.WriteTo(stream);
			m_Mesh2.WriteTo(stream);
			m_Mesh3.WriteTo(stream);
		}
	}
}
