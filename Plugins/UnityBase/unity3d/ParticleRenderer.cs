using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using SB3Utility;

namespace UnityPlugin
{
	public class UVAnimation : IObjInfo
	{
		int x_Tile { get; set; }
		int y_Tile { get; set; }
		float cycles { get; set; }

		public UVAnimation(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			x_Tile = reader.ReadInt32();
			y_Tile = reader.ReadInt32();
			cycles = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(x_Tile);
			writer.Write(y_Tile);
			writer.Write(cycles);
		}
	}

	public class ParticleRenderer : MeshRenderer, Component
	{
		public float m_CameraVelocityScale { get; set; }
		public int m_StretchParticles { get; set; }
		public float m_LengthScale { get; set; }
		public float m_VelocityScale { get; set; }
		public float m_MaxParticleSize { get; set; }
		public UVAnimation UV_Animation { get; set; }

		public ParticleRenderer(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
			: base(file, pathID, classID1, classID2) { }

		public new void LoadFrom(Stream stream)
		{
			base.LoadFrom(stream);

			BinaryReader reader = new BinaryReader(stream);
			m_CameraVelocityScale = reader.ReadSingle();
			m_StretchParticles = reader.ReadInt32();
			m_LengthScale = reader.ReadSingle();
			m_VelocityScale = reader.ReadSingle();
			m_MaxParticleSize = reader.ReadSingle();
			UV_Animation = new UVAnimation(stream);
		}

		public new void WriteTo(Stream stream)
		{
			base.WriteTo(stream);

			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_CameraVelocityScale);
			writer.Write(m_StretchParticles);
			writer.Write(m_LengthScale);
			writer.Write(m_VelocityScale);
			writer.Write(m_MaxParticleSize);
			UV_Animation.WriteTo(stream);
		}
	}
}
