using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using SlimDX;
using System.Drawing;

using SB3Utility;

namespace AiDroidPlugin
{
	public class remId : IObjInfo, IComparable
	{
		public string buffer = null; // length 256

		public remId(string str)
		{
			buffer = str;
		}

		public override bool Equals(object obj)
		{
			return CompareTo(obj) == 0;
		}

		public static bool operator ==(remId id1, remId id2)
		{
			if (System.Object.ReferenceEquals(id1, id2))
			{
				return true;
			}

			if (((object)id1 == null) || ((object)id2 == null))
			{
				return false;
			}

			return id1.buffer == id2.buffer;
		}
		public static bool operator !=(remId id1, remId id2)
		{
			return !(id1 == id2);
		}

		public override int GetHashCode()
		{
			return buffer.GetHashCode();
		}

		public override string ToString()
		{
			return buffer;
		}

		public void WriteTo(Stream stream)
		{
			int rest;
			if (buffer != null)
			{
				byte[] ascii = Encoding.ASCII.GetBytes(buffer);
				stream.Write(ascii, 0, ascii.Length);
				rest = 256 - ascii.Length;
			}
			else
				rest = 256;
			if (rest > 0)
				stream.Write(new byte[rest], 0, rest);
		}

		public int CompareTo(object obj)
		{
			if (!(obj is remId))
				return -1;

			remId arg = (remId)obj;
			if (arg.buffer == null || buffer == null || buffer.Length != arg.buffer.Length)
				return -1;
			for (int i = 0; i < buffer.Length; i++)
			{
				int diff = buffer[i] - arg.buffer[i];
				if (diff != 0)
					return diff;
			}
			return 0;
		}
	}

	public abstract class remFileSection : IObjInfo
	{
		public byte[] type = null;
		public int length = 0;

		public remFileSection(byte[] t, int l)
		{
			this.type = t;
			this.length = l;
		}

		public abstract void WriteTo(Stream stream);
	}

	public class remMaterial : remFileSection, IObjInfo
	{
		public remId name = null;
		public float[] properties = null; // diffuse, ambient, specular, emissive with RGB only
		public int specularPower = 0;
		public int unk_or_flag = 0;
		public remId unknown = null;
		public remId texture = null;

		public float this[int i]
		{
			get { return properties[i]; }
			set { properties[i] = value; }
		}

		public float[] diffuse
		{
			get { return new float[] { properties[0], properties[1], properties[2] }; }
			set { properties[0] = value[0]; properties[1] = value[1]; properties[2] = value[2]; }
		}

		public float[] ambient
		{
			get { return new float[] { properties[3], properties[4], properties[5] }; }
			set { properties[3] = value[0]; properties[4] = value[1]; properties[5] = value[2]; }
		}

		public float[] emissive
		{
			get { return new float[] { properties[6], properties[7], properties[8] }; }
			set { properties[6] = value[0]; properties[7] = value[1]; properties[8] = value[2]; }
		}

		public float[] specular
		{
			get { return new float[] { properties[9], properties[10], properties[11] }; }
			set { properties[9] = value[0]; properties[10] = value[1]; properties[11] = value[2]; }
		}

		public remMaterial()
			: base(Encoding.ASCII.GetBytes("MATO"), 4+4+256+4*3*4+4+4+256)
		{
			properties = new float[12];
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(base.type);
			writer.Write(base.length + (texture != null ? 256 : 0));
			name.WriteTo(stream);
			writer.Write(properties);
			writer.Write(specularPower);
			writer.Write(unk_or_flag);
			unknown.WriteTo(stream);
			if (texture != null)
				texture.WriteTo(stream);
		}
	}

	public class remMATCsection : remFileSection
	{
		public int numMats { get { return materials.Count; } }
		public List<remMaterial> materials = null;

		public remMaterial this[int i]
		{
			get { return materials[i]; }
		}

		public void AddMaterial(remMaterial material)
		{
			materials.Add(material);
			length += material.length;
		}

		public remMATCsection(int numSubs)
			: base(Encoding.ASCII.GetBytes("MATC"), 0)
		{
			materials = new List<remMaterial>(numSubs);
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(base.type);
			writer.Write(base.length);
			writer.Write(numMats);
			if (numMats > 0)
			{
				for (int i = 0; i < numMats; i++)
					materials[i].WriteTo(stream);
			}
		}
	}

	public class remBone : remFileSection, IObjChild
	{
		public remId name = null;
		public Matrix trans;
		public int numChilds { get { return childNames.Count; } }
		public List<remId> childNames = null;

		public List<remBone> childs;

		public void AddChild(remId child)
		{
			childNames.Add(child);
			length += 256;
		}

		public remBone this[int i]
		{
			get { return childs[i]; }
		}

		public remBone(int numChilds)
			: base(Encoding.ASCII.GetBytes("BONO"), 4+4+256+16*4+4)
		{
			childNames = new List<remId>(numChilds);
			childs = new List<remBone>(numChilds);
		}

		public dynamic Parent { get; set; }

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(base.type);
			writer.Write(base.length);
			name.WriteTo(stream);
			writer.Write(trans);
			writer.Write(numChilds);
			if (numChilds > 0)
			{
				for (int i = 0; i < numChilds; i++)
					childNames[i].WriteTo(stream);
			}
		}
	}

	public class remBONCsection : remFileSection
	{
		public int numBones { get { return frames.Count; } }
		public List<remBone> frames = null;

		public remBone rootFrame = null;

		public remBone this[int i]
		{
			get { return frames[i]; }
		}

		public void AddParentBone(remBone parent)
		{
			for (int i = 0; i < parent.numChilds; i++)
			{
				remId child = parent.childNames[i];
				for (int j = 0; j < frames.Count; j++)
				{
					if (frames[j].name == child)
					{
						frames[j].Parent = parent;
						parent.childs.Add(frames[j]);
						break;
					}
				}
			}

			frames.Add(parent);
			length += parent.length;
		}

		public remBONCsection(int numSubs)
			: base(Encoding.ASCII.GetBytes("BONC"), 0)
		{
			this.frames = new List<remBone>(numSubs);
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(base.type);
			writer.Write(base.length);
			writer.Write(numBones);
			if (numBones > 0)
			{
				for (int i = 0; i < numBones; i++)
					frames[i].WriteTo(stream);
			}
		}
	}

	public class remVertex : IObjInfo
	{
		public Vector3 Position;
		public Vector2 UV;
		public Vector3 Normal;
		public Color4 RGBA;

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(Position);
			writer.Write(UV);
			writer.Write(Normal);
			writer.Write(RGBA);
		}
	}

	public class remMesh : remFileSection
	{
		public remId name = null;
		public int numMats { get { return materials.Count; } }
		public remId frame = null;
		public int numFaces { get { return faces.Length / 3; } }
		public int numVertices { get { return vertices.Length; } }
		public int[] unknown = new int[2];
		public List<remId> materials = null;
		public remVertex[] vertices = null;
		public int[] faces = null;
		public int[] faceMarks = null;

		public void AddMaterial(remId material)
		{
			materials.Add(material);
			length += 256;
		}

		public remMesh(int numMats)
			: base(Encoding.ASCII.GetBytes("MESO"), 4+4+256+4+256+4+4+2*4)
		{
			materials = new List<remId>(numMats);
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(base.type);
			writer.Write(base.length);
			name.WriteTo(stream);
			writer.Write(numMats);
			frame.WriteTo(stream);
			writer.Write(numFaces);
			writer.Write(numVertices);
			writer.Write(unknown);
			if (numMats > 0)
			{
				for (int i = 0; i < numMats; i++)
					materials[i].WriteTo(stream);
			}
			if (numVertices > 0)
			{
				for (int i = 0; i < numVertices; i++)
				{
					vertices[i].WriteTo(stream);
				}
			}
			if (numFaces > 0)
			{
				writer.Write(faces);
				writer.Write(faceMarks);
			}
		}
	}

	public class remMESCsection : remFileSection
	{
		public int numMeshes { get { return meshes.Count; } }
		public List<remMesh> meshes = null;

		public remMesh this[int i]
		{
			get { return meshes[i]; }
		}

		public void AddMesh(remMesh mesh)
		{
			meshes.Add(mesh);
			length += mesh.length;
		}

		public remMESCsection(int numSubs)
			: base(Encoding.ASCII.GetBytes("MESC"), 0)
		{
			meshes = new List<remMesh>(numSubs);
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(base.type);
			writer.Write(base.length);
			writer.Write(numMeshes);
			if (numMeshes > 0)
			{
				for (int i = 0; i < numMeshes; i++)
					meshes[i].WriteTo(stream);
			}
		}
	}

	public class remBoneWeights : IObjInfo
	{
		public remId bone = null;
		public int numVertIdxWts { get { return vertexIndices.Length; } }
		public Matrix trans;
		public int[] vertexIndices = null;
		public float[] vertexWeights = null;

		public void WriteTo(Stream stream)
		{
			bone.WriteTo(stream);
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(numVertIdxWts);
			writer.Write(trans);
			writer.Write(vertexIndices);
			writer.Write(vertexWeights);
		}
	}

	public class remSkin : remFileSection
	{
		public remId mesh = null;
		public int numWeights { get { return weights.Count; } }
		public List<remBoneWeights> weights = null;

		public remBoneWeights this[int i]
		{
			get { return weights[i]; }
		}

		public void AddWeights(remBoneWeights weights)
		{
			this.weights.Add(weights);
			length += 256+4+16*4 + (4+4)*weights.numVertIdxWts;
		}

		public remSkin(int numWeights)
			: base(Encoding.ASCII.GetBytes("SKIO"), 4+4+256+4)
		{
			weights = new List<remBoneWeights>(numWeights);
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(base.type);
			writer.Write(base.length);
			mesh.WriteTo(stream);
			writer.Write(numWeights);
			if (numWeights > 0)
			{
				for (int i = 0; i < numWeights; i++)
				{
					weights[i].WriteTo(stream);
				}
			}
		}
	}

	public class remSKICsection : remFileSection
	{
		public int numSkins { get { return skins.Count; } }
		public List<remSkin> skins = null;

		public remSkin this[int i]
		{
			get { return skins[i]; }
		}

		public void AddSkin(remSkin skin)
		{
			skins.Add(skin);
			length += skin.length;
		}

		public remSKICsection(int numSubs)
			: base(Encoding.ASCII.GetBytes("SKIC"), 0)
		{
			skins = new List<remSkin>(numSubs);
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(base.type);
			writer.Write(base.length);
			writer.Write(numSkins);
			if (numSkins > 0)
			{
				for (int i = 0; i < numSkins; i++)
					skins[i].WriteTo(stream);
			}
		}
	}

	public class remFile : IObjInfo
	{
		public remMATCsection MATC = null;
		public remBONCsection BONC = null;
		public remMESCsection MESC = null;
		public remSKICsection SKIC = null;

		public void WriteTo(Stream stream)
		{
			MATC.WriteTo(stream);
			BONC.WriteTo(stream);
			MESC.WriteTo(stream);
			SKIC.WriteTo(stream);
		}
	}
}
