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
				version = reader.ReadInt32();
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
			public int version;
			public string Name { get; set; }
			public int submeshIdx;
			public ushort[] indices;
			public byte positionsPresent;
			public Vector3[] positions;
			public byte bonesPresent;
			public float[][] boneWeights3;
			public byte[][] boneIndices;
			public class SVIEX_Bone
			{
				public string name;
				public int boneIdx;
				public Matrix matrix;
			};
			public SVIEX_Bone[] bones;
			public byte normalsPresent;
			public Vector3[] normals;
			public byte uvsPresent;
			public Vector2[] uvs;
			public byte futureSectionPresent;

			public SubmeshSection()
			{
				version = 100;
			}

			public SubmeshSection(Stream stream)
			{
				BinaryReader reader = new BinaryReader(stream);

				version = reader.ReadInt32();
				if (version != 100)
				{
					throw new Exception("SVIEX submesh section bad beginning: 0x" + version.ToString("X"));
				}
				Name = reader.ReadName();
				submeshIdx = reader.ReadInt32();
				int numIndices = reader.ReadInt32();
				indices = reader.ReadUInt16Array(numIndices);

				positionsPresent = reader.ReadByte();
				if (positionsPresent == 1)
				{
					positions = reader.ReadVector3Array(numIndices);
				}

				bonesPresent = reader.ReadByte();
				if (bonesPresent == 1)
				{
					boneWeights3 = new float[numIndices][];
					for (ushort i = 0; i < numIndices; i++)
					{
						boneWeights3[i] = reader.ReadSingleArray(3);
					}
					boneIndices = new byte[numIndices][];
					for (ushort i = 0; i < numIndices; i++)
					{
						boneIndices[i] = reader.ReadBytes(4);
					}

					int numBones = reader.ReadInt32();
					bones = new SVIEX_Bone[numBones];
					for (int i = 0; i < numBones; i++)
					{
						bones[i] = new SVIEX_Bone();
						bones[i].name = reader.ReadName();
						bones[i].boneIdx = reader.ReadInt32();
						bones[i].matrix = reader.ReadMatrix();
					}
				}

				normalsPresent = reader.ReadByte();
				if (normalsPresent == 1)
				{
					normals = reader.ReadVector3Array(numIndices);
				}

				uvsPresent = reader.ReadByte();
				if (uvsPresent == 1)
				{
					uvs = reader.ReadVector2Array(numIndices);
				}

				futureSectionPresent = reader.ReadByte();
				if (futureSectionPresent != 0)
				{
					throw new Exception("SVIEX future section present");
				}
			}

			public void WriteTo(Stream stream)
			{
				BinaryWriter writer = new BinaryWriter(stream);
				writer.Write(version);
				writer.WriteName(Name);
				writer.Write(submeshIdx);
				writer.Write(indices.Length);
				writer.Write(indices);

				writer.Write(positionsPresent);
				if (positionsPresent == 1)
				{
					writer.Write(positions);
				}

				writer.Write(bonesPresent);
				if (bonesPresent == 1)
				{
					for (ushort i = 0; i < indices.Length; i++)
					{
						writer.Write(boneWeights3[i]);
					}
					for (ushort i = 0; i < indices.Length; i++)
					{
						writer.Write(boneIndices[i]);
					}
					writer.Write(bones.Length);
					for (int i = 0; i < bones.Length; i++)
					{
						writer.WriteName(bones[i].name);
						writer.Write(bones[i].boneIdx);
						writer.Write(bones[i].matrix);
					}
				}

				writer.Write(normalsPresent);
				if (normalsPresent == 1)
				{
					writer.Write(normals);
				}

				writer.Write(uvsPresent);
				if (uvsPresent == 1)
				{
					writer.Write(uvs);
				}

				writer.Write(futureSectionPresent);
			}
		}
	}
}
