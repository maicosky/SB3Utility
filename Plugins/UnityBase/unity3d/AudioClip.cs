using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using SB3Utility;

namespace UnityPlugin
{
	public class AudioClip : Component
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public int m_Format { get; set; }
		public int m_Type { get; set; }
		public bool m_3D { get; set; }
		public bool m_UseHardware { get; set; }
		public int m_Stream { get; set; }
		public byte[] m_AudioData { get; set; }

		public AudioClip(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
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
			m_Format = reader.ReadInt32();
			m_Type = reader.ReadInt32();
			m_3D = reader.ReadBoolean();
			m_UseHardware = reader.ReadBoolean();
			reader.ReadBytes(2);
			m_Stream = reader.ReadInt32();
			m_AudioData = reader.ReadBytes(reader.ReadInt32());
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			writer.Write(m_Format);
			writer.Write(m_Type);
			writer.Write(m_3D);
			writer.Write(m_UseHardware);
			writer.Write(new byte[2]);
			writer.Write(m_Stream);

			writer.Write(m_AudioData.Length);
			writer.Write(m_AudioData);
		}

		public void Export(string path)
		{
			DirectoryInfo dirInfo = new DirectoryInfo(path);
			if (!dirInfo.Exists)
			{
				dirInfo.Create();
			}

			bool isOgg = m_AudioData[0] == (byte)'O' && m_AudioData[1] == (byte)'g' && m_AudioData[2] == (byte)'g' && m_AudioData[3] == (byte)'S';
			using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(path + "\\" + m_Name + "." + (isOgg ? "ogg" : UnityClassID.AudioClip.ToString()))))
			{
				writer.Write(m_AudioData);
				writer.BaseStream.SetLength(writer.BaseStream.Position);
			}
		}

		public static AudioClip Import(string filePath)
		{
			AudioClip ac = new AudioClip(null, 0, UnityClassID.AudioClip, UnityClassID.AudioClip);
			ac.m_Name = Path.GetFileNameWithoutExtension(filePath);
			using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath)))
			{
				ac.m_AudioData = reader.ReadBytes((int)reader.BaseStream.Length);
			}
			return ac;
		}
	}
}
