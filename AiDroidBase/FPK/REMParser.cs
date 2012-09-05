using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.IO;
using SlimDX;

using SB3Utility;

namespace AiDroidPlugin
{
	public class remParser
	{
		public remFile RemFile;
		public string RemPath;

		public remParser(string path)
		{
			RemPath = path;
			RemFile = Read(path);
			Report.ReportLog(path + " loaded successfully");
		}

		private static remId GetIdentifier(byte[] buffer, int startIdx, int lengthInBuffer)
		{
			char[] ca = Encoding.ASCII.GetChars(buffer, startIdx, lengthInBuffer);
			int length = 0;
			while (length < ca.Length && ca[length] != (char)0)
				length++;
			return new remId(new string(ca, 0, length));
		}

		private static remId GetIdentifier(byte[] buffer, int startIdx)
		{
			return GetIdentifier(buffer, startIdx, 256);
		}

		private static bool TypeCheck(byte[] t1, byte[] t2)
		{
			return t1[0] == t2[0] && t1[1] == t2[1] && t1[2] == t2[2] && t1[3] == t2[3];
		}

		private static remMATCsection ReadMaterials(string sectionName, int sectionLength, int numMaterials, byte[] sectionBuffer)
		{
			remMATCsection matSec = new remMATCsection(numMaterials);
			int secBufIdx = 0;
			for (int subSection = 0; subSection < numMaterials; subSection++)
			{
				byte[] type = new byte[4] { sectionBuffer[secBufIdx+0], sectionBuffer[secBufIdx+1], sectionBuffer[secBufIdx+2], sectionBuffer[secBufIdx+3] };
				int length = BitConverter.ToInt32(sectionBuffer, secBufIdx+4);

				remMaterial mat = new remMaterial();
				Trace.Assert(TypeCheck(mat.type, type));
				mat.name = GetIdentifier(sectionBuffer, secBufIdx+8);

				for (int i = 0; i < mat.properties.Length; i++)
					mat[i] = BitConverter.ToSingle(sectionBuffer, secBufIdx+8+256 + i*4);
				mat.specularPower = BitConverter.ToInt32(sectionBuffer, secBufIdx+8+256+12*4);
				mat.unk_or_flag = BitConverter.ToInt32(sectionBuffer, secBufIdx+8+256+12*4+4);
				mat.unknown = GetIdentifier(sectionBuffer, secBufIdx+8+256+12*4+4+4);

				if (length >= 0x240+256)
					mat.texture = GetIdentifier(sectionBuffer, secBufIdx+0x240);

				matSec.AddMaterial(mat);

				secBufIdx += length;
			}
			if (secBufIdx != sectionLength)
				Console.WriteLine("Warning! MATC section has wrong length.");
			return matSec;
		}

		private static remBONCsection ReadBones(string sectionName, int sectionLength, int numBones, byte[] sectionBuffer)
		{
			remBONCsection boneSec = new remBONCsection(numBones);
			int secBufIdx = 0;
			for (int subSection = 0; subSection < numBones; subSection++)
			{
				byte[] type = new byte[4] { sectionBuffer[secBufIdx+0], sectionBuffer[secBufIdx+1], sectionBuffer[secBufIdx+2], sectionBuffer[secBufIdx+3] };
				int length = BitConverter.ToInt32(sectionBuffer, secBufIdx+4);

				remBone bone = new remBone(length > 256+16*4+4 ? (length - 256+16*4+4) / 256 : 0);
				Trace.Assert(TypeCheck(bone.type, type));
				bone.name = GetIdentifier(sectionBuffer, secBufIdx+8);

				Matrix matrix = new Matrix();
				for (int i = 0; i < 4; i++)
				{
					Vector4 row = new Vector4();
					for (int j = 0; j < 4; j++)
						row[j] = BitConverter.ToSingle(sectionBuffer, secBufIdx+8+256 + (i*4 + j) * 4);
					matrix.set_Rows(i, row);
				}
				bone.trans = matrix;

				int numChilds = BitConverter.ToInt32(sectionBuffer, secBufIdx+8+256 + 16*4);
				for (int i = 0; i < numChilds; i++)
					bone.AddChild(GetIdentifier(sectionBuffer, secBufIdx+8+256 + 16*4 + 4 + i*256));

				boneSec.AddParentBone(bone);

				secBufIdx += length;
			}

			remBone root = new remBone(1);
			root.name = new remId("RootFrame");
			root.trans = Matrix.Identity;
			for (int i = 0; i < numBones; i++)
			{
				if (boneSec[i].Parent == null)
				{
					root.AddChild(boneSec[i].name);
					boneSec[i].Parent = root;
					root.childs.Add(boneSec[i]);
				}
			}
			boneSec.rootFrame = root;

			if (secBufIdx != sectionLength)
				Console.WriteLine("Warning! BONC section has wrong length.");
			return boneSec;
		}

		private static remMESCsection ReadMeshes(string sectionName, int sectionLength, int numMeshes, byte[] sectionBuffer)
		{
			remMESCsection meshSec = new remMESCsection(numMeshes);
			int secBufIdx = 0;
			for (int subSection = 0; subSection < numMeshes; subSection++)
			{
				byte[] type = new byte[4] { sectionBuffer[secBufIdx+0], sectionBuffer[secBufIdx+1], sectionBuffer[secBufIdx+2], sectionBuffer[secBufIdx+3] };
				int length = BitConverter.ToInt32(sectionBuffer, secBufIdx+4);

				remMesh mesh = new remMesh(5);
				Trace.Assert(TypeCheck(mesh.type, type));
				mesh.name = GetIdentifier(sectionBuffer, secBufIdx+8);

				int numMats = BitConverter.ToInt32(sectionBuffer, secBufIdx+8+256);
				mesh.frame = GetIdentifier(sectionBuffer, secBufIdx+8+256+4);
				int numFaces = BitConverter.ToInt32(sectionBuffer, secBufIdx+8+256+4+256);
				int numVertices = BitConverter.ToInt32(sectionBuffer, secBufIdx+8+256+4+256 + 4);
				for (int i = 0; i < mesh.unknown.Length; i++)
					mesh.unknown[i] = BitConverter.ToInt32(sectionBuffer, secBufIdx+8+256+4+256+8 + i*4);
				for (int i = 0; i < numMats; i++)
				{
					remId mat = GetIdentifier(sectionBuffer, secBufIdx+8+256+4+256 + 4*4 + i*256);
					mesh.AddMaterial(mat);
				}

				mesh.vertices = new remVertex[numVertices];
				int vertBufIdx = secBufIdx+8+256+4+256 + 4*4 + mesh.numMats*256;
				for (int i = 0; i < numVertices; i++)
				{
					remVertex vertex = new remVertex();
					vertex.Position = new Vector3();
					vertex.Position[0] = BitConverter.ToSingle(sectionBuffer, vertBufIdx + 0);
					vertex.Position[1] = BitConverter.ToSingle(sectionBuffer, vertBufIdx + 4);
					vertex.Position[2] = BitConverter.ToSingle(sectionBuffer, vertBufIdx + 8);

					vertex.UV = new Vector2();
					vertex.UV[0]  = BitConverter.ToSingle(sectionBuffer, vertBufIdx + 12);
					vertex.UV[1]  = BitConverter.ToSingle(sectionBuffer, vertBufIdx + 16);

					vertex.Normal = new Vector3();
					vertex.Normal[0] = BitConverter.ToSingle(sectionBuffer, vertBufIdx + 20);
					vertex.Normal[1] = BitConverter.ToSingle(sectionBuffer, vertBufIdx + 24);
					vertex.Normal[2] = BitConverter.ToSingle(sectionBuffer, vertBufIdx + 28);

					vertex.RGBA = new Color4(BitConverter.ToInt32(sectionBuffer, vertBufIdx + 32));

					mesh.vertices[i] = vertex;
					vertBufIdx += 36;
				}

				mesh.faces = new int[numFaces * 3];
				int faceBufIdx = vertBufIdx;
				for (int i = 0; i < numFaces; i++)
				{
					mesh.faces[i*3+0] = BitConverter.ToInt32(sectionBuffer, faceBufIdx + 0);
					mesh.faces[i*3+1] = BitConverter.ToInt32(sectionBuffer, faceBufIdx + 4);
					mesh.faces[i*3+2] = BitConverter.ToInt32(sectionBuffer, faceBufIdx + 8);
					faceBufIdx += 12;
				}

				mesh.faceMarks = new int[numFaces];
				int faceExtraIdx = faceBufIdx;
				for (int i = 0; i < numFaces; i++)
				{
					mesh.faceMarks[i] = BitConverter.ToInt32(sectionBuffer, faceExtraIdx);
					faceExtraIdx += 4;
				}

				meshSec.AddMesh(mesh);

				secBufIdx += length;
			}
			if (secBufIdx != sectionLength)
				Console.WriteLine("Warning! MESC section has wrong length.");
			return meshSec;
		}

		private static remSKICsection ReadSkin(string sectionName, int sectionLength, int numSkins, byte[] sectionBuffer)
		{
			remSKICsection skinSec = new remSKICsection(numSkins);
			int secBufIdx = 0;
			for (int subSection = 0; subSection < numSkins; subSection++)
			{
				byte[] type = new byte[4] { sectionBuffer[secBufIdx+0], sectionBuffer[secBufIdx+1], sectionBuffer[secBufIdx+2], sectionBuffer[secBufIdx+3] };
				int length = BitConverter.ToInt32(sectionBuffer, secBufIdx+4);

				remId mesh = GetIdentifier(sectionBuffer, secBufIdx+8);
				int numWeights = BitConverter.ToInt32(sectionBuffer, secBufIdx+8+256);
				remSkin skin = new remSkin(numWeights);
				Trace.Assert(TypeCheck(skin.type, type));
				skin.mesh = mesh;
				int weightBufIdx = secBufIdx+8+256+4;
				for (int weightIdx = 0; weightIdx < numWeights; weightIdx++)
				{
					remBoneWeights weights = new remBoneWeights();
					weights.bone = GetIdentifier(sectionBuffer, weightBufIdx);
					weightBufIdx += 256;
					int numVertIdxWts = BitConverter.ToInt32(sectionBuffer, weightBufIdx);
					weightBufIdx += 4;

					Matrix matrix = new Matrix();
					for (int i = 0; i < 4; i++)
					{
						Vector4 row = new Vector4();
						for (int j = 0; j < 4; j++)
						{
							row[j] = BitConverter.ToSingle(sectionBuffer, weightBufIdx);
							weightBufIdx += 4;
						}
						matrix.set_Rows(i, row);
					}
					weights.trans = matrix;

					weights.vertexIndices = new int[numVertIdxWts];
					for (int i = 0; i < numVertIdxWts; i++)
					{
						weights.vertexIndices[i] = BitConverter.ToInt32(sectionBuffer, weightBufIdx);
						weightBufIdx += 4;
					}
					weights.vertexWeights = new float[weights.numVertIdxWts];
					for (int i = 0; i < numVertIdxWts; i++)
					{
						weights.vertexWeights[i] = BitConverter.ToSingle(sectionBuffer, weightBufIdx);
						weightBufIdx += 4;
					}

					skin.AddWeights(weights);
				}

				skinSec.AddSkin(skin);

				secBufIdx += length;
			}
			if (secBufIdx != sectionLength)
				Console.WriteLine("Warning! SKIC section has wrong length.");
			return skinSec;
		}

		public static remFile Read(string filename)
		{
			try
			{
				using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(filename)))
				{
					remFile file = new remFile();
					string[] sectionNames = { "MATC", "BONC", "MESC", "SKIC" };
					for (int sectionIdx = 0; sectionIdx < sectionNames.Length; sectionIdx++)
					{
						string sectionName = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
						if (sectionName != sectionNames[sectionIdx])
						{
							Console.WriteLine("Strange section or order");
							return null;
						}
						int sectionLength = binaryReader.ReadInt32();
						int numSubSections = binaryReader.ReadInt32();
						byte[] sectionBuffer = binaryReader.ReadBytes((int)sectionLength);
						switch (sectionIdx)
						{
						case 0: file.MATC = ReadMaterials(sectionName, sectionLength, numSubSections, sectionBuffer); break;
						case 1: file.BONC = ReadBones(sectionName, sectionLength, numSubSections, sectionBuffer); break;
						case 2: file.MESC = ReadMeshes(sectionName, sectionLength, numSubSections, sectionBuffer); break;
						case 3: file.SKIC = ReadSkin(sectionName, sectionLength, numSubSections, sectionBuffer); break;
						}
					}
					if (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
						Console.WriteLine("EOF not reached at " + binaryReader.BaseStream.Position + " (" + binaryReader.BaseStream.Length + ")");
					return file;
				}
			}
			catch (FileNotFoundException)
			{
				Console.WriteLine("file not found");
			}
			return null;
		}
	}
}
