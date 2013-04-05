using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace PPD_Preview_Clothes
{
	public class sviexParser : IWriteFile
	{
		public string Name { get; set; }

		public int version;
		public List<SubmeshSection> sections;

		public sviexParser()
		{
			version = 100;
			sections = new List<SubmeshSection>();
		}

		public sviexParser(Stream stream, string name)
			: this(stream)
		{
			this.Name = name;
		}

		public sviexParser(Stream stream)
		{
			using (BinaryReader reader = new BinaryReader(stream))
			{
				version = (int)reader.ReadInt32();
				if (version != 100)
				{
					throw new Exception("SVIEX bad version: " + version);
				}

				int numSections = reader.ReadInt32();
				sections = new List<SubmeshSection>(numSections);
				for (int secIdx = 0; secIdx < numSections; secIdx++)
				{
					SubmeshSection section = new SubmeshSection(reader.BaseStream);
					sections.Add(section);
				}
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(version);
			writer.Write(sections.Count);
			foreach (SubmeshSection sec in sections)
			{
				sec.WriteTo(stream);
			}
		}

		public class SubmeshSection : IWriteFile
		{
			public int unknown1;
			public string Name { get; set; }
			public int submeshIdx;
			public ushort[] indices;
			public byte[] unknown3;
			public Vector3[] normals;
			public byte[] unknown4;

			public SubmeshSection()
			{
				unknown1 = 100;
				unknown3 = new byte[] { 0x00, 0x00, 0x01 };
				unknown4 = new byte[] { 0x00, 0x00 };
			}

			public SubmeshSection(Stream stream)
			{
				BinaryReader reader = new BinaryReader(stream);

				unknown1 = reader.ReadInt32();
				if (unknown1 != 100)
				{
					throw new Exception("SVIEX submesh section bad beginning: 0x" + unknown1.ToString("X"));
				}
				Name = reader.ReadName();
				submeshIdx = reader.ReadInt32();
				int numIndices = reader.ReadInt32();
				indices = reader.ReadUInt16Array(numIndices);
				unknown3 = reader.ReadBytes(3);
				normals = new Vector3[numIndices];
				for (int i = 0; i < numIndices; i++)
				{
					normals[i] = reader.ReadVector3();
				}
				unknown4 = reader.ReadBytes(2);
			}

			public void WriteTo(Stream stream)
			{
				BinaryWriter writer = new BinaryWriter(stream);
				writer.Write(unknown1);
				writer.WriteName(Name);
				writer.Write(submeshIdx);
				writer.Write(indices.Length);
				writer.Write(indices);
				writer.Write(unknown3);
				for (int i = 0; i < normals.Length; i++)
				{
					writer.Write(normals[i]);
				}
				writer.Write(unknown4);
			}
		}
	}
}
