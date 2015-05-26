using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public class SubMesh : IObjInfo
	{
		public uint firstByte { get; set; }
		public uint indexCount { get; set; }
		public int topology { get; set; }
		public uint firstVertex { get; set; }
		public uint vertexCount { get; set; }
		public AABB localAABB { get; set; }

		public SubMesh(Stream stream)
		{
			LoadFrom(stream);
		}

		public SubMesh()
		{
			localAABB = new AABB();
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			firstByte = reader.ReadUInt32();
			indexCount = reader.ReadUInt32();
			topology = reader.ReadInt32();
			firstVertex = reader.ReadUInt32();
			vertexCount = reader.ReadUInt32();
			localAABB = new AABB(stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(firstByte);
			writer.Write(indexCount);
			writer.Write(topology);
			writer.Write(firstVertex);
			writer.Write(vertexCount);
			localAABB.WriteTo(stream);
		}
	}

	public class BlendShapeData : IObjInfo
	{
		public List<BlendShapeVertex> vertices { get; set; }
		public List<MeshBlendShape> shapes { get; set; }
		public List<MeshBlendShapeChannel> channels { get; set; }
		public List<float> fullWeights { get; set; }

		public BlendShapeData(Stream stream)
		{
			LoadFrom(stream);
		}

		public BlendShapeData()
		{
			vertices = new List<BlendShapeVertex>();
			shapes = new List<MeshBlendShape>();
			channels = new List<MeshBlendShapeChannel>();
			fullWeights = new List<float>();
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numVerts = reader.ReadInt32();
			vertices = new List<BlendShapeVertex>(numVerts);
			for (int i = 0; i < numVerts; i++)
			{
				vertices.Add(new BlendShapeVertex(stream));
			}

			int numShapes = reader.ReadInt32();
			shapes = new List<MeshBlendShape>(numShapes);
			for (int i = 0; i < numShapes; i++)
			{
				shapes.Add(new MeshBlendShape(stream));
			}

			int numChannels = reader.ReadInt32();
			channels = new List<MeshBlendShapeChannel>(numChannels);
			for (int i = 0; i < numChannels; i++)
			{
				channels.Add(new MeshBlendShapeChannel(stream));
			}

			int numWeights = reader.ReadInt32();
			fullWeights = new List<float>(numWeights);
			for (int i = 0; i < numWeights; i++)
			{
				fullWeights.Add(reader.ReadSingle());
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(vertices.Count);
			for (int i = 0; i < vertices.Count; i++)
			{
				vertices[i].WriteTo(stream);
			}

			writer.Write(shapes.Count);
			for (int i = 0; i < shapes.Count; i++)
			{
				shapes[i].WriteTo(stream);
			}

			writer.Write(channels.Count);
			for (int i = 0; i < channels.Count; i++)
			{
				channels[i].WriteTo(stream);
			}

			writer.Write(fullWeights.Count);
			for (int i = 0; i < fullWeights.Count; i++)
			{
				writer.Write(fullWeights[i]);
			}
		}
	}

	public class BlendShapeVertex : IObjInfo
	{
		public Vector3 vertex { get; set; }
		public Vector3 normal { get; set; }
		public Vector3 tangent { get; set; }
		public uint index { get; set; }

		public BlendShapeVertex() { }

		public BlendShapeVertex(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			vertex = reader.ReadVector3();
			normal = reader.ReadVector3();
			tangent = reader.ReadVector3();
			index = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(vertex);
			writer.Write(normal);
			writer.Write(tangent);
			writer.Write(index);
		}
	}

	public class MeshBlendShape : IObjInfo
	{
		public uint firstVertex { get; set; }
		public uint vertexCount { get; set; }
		public bool hasNormals { get; set; }
		public bool hasTangents { get; set; }

		public MeshBlendShape() { }

		public MeshBlendShape(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			firstVertex = reader.ReadUInt32();
			vertexCount = reader.ReadUInt32();
			hasNormals = reader.ReadBoolean();
			hasTangents = reader.ReadBoolean();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(firstVertex);
			writer.Write(vertexCount);
			writer.Write(hasNormals);
			writer.Write(hasTangents);
			writer.Write(new byte[2]);
		}
	}

	public class MeshBlendShapeChannel : IObjInfo
	{
		public string name { get; set; }
		public uint nameHash { get; set; }
		public int frameIndex { get; set; }
		public int frameCount { get; set; }

		public MeshBlendShapeChannel() { }

		public MeshBlendShapeChannel(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			name = reader.ReadNameA4();
			nameHash = reader.ReadUInt32();
			frameIndex = reader.ReadInt32();
			frameCount = reader.ReadInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(name);
			writer.Write(nameHash);
			writer.Write(frameIndex);
			writer.Write(frameCount);
		}
	}

	public class BoneInfluence : IObjInfo
	{
		public float[] weight { get; set; }
		public int[] boneIndex { get; set; }

		public BoneInfluence(Stream stream)
		{
			LoadFrom(stream);
		}

		public BoneInfluence()
		{
			weight = new float[4];
			boneIndex = new int[4];
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			weight = reader.ReadSingleArray(4);
			boneIndex = reader.ReadInt32Array(4);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(weight);
			writer.Write(boneIndex);
		}
	}

	public class VertexData : IObjInfo
	{
		public uint m_CurrentChannels { get; set; }
		public uint m_VertexCount { get; set; }
		public List<ChannelInfo> m_Channels { get; set; }
		public List<StreamInfo> m_Streams { get; set; }
		public byte[] m_DataSize { get; set; }

		public VertexData(Stream stream)
		{
			LoadFrom(stream);
		}

		public VertexData(uint vertexCount)
		{
			m_CurrentChannels = 0x2B;
			m_VertexCount = vertexCount;
			m_Channels = new List<ChannelInfo>
			(
				new ChannelInfo[]
				{
					new ChannelInfo(0, 0, 0, 3),
					new ChannelInfo(0, 12, 0, 3),
					new ChannelInfo(0, 0, 0, 0),
					new ChannelInfo(1, 0, 0, 2),
					new ChannelInfo(0, 0, 0, 0),
					new ChannelInfo(0, 24, 0, 4)
				}
			);
			m_Streams = new List<StreamInfo>
			(
				new StreamInfo[]
				{
					new StreamInfo(0x23, 0, 40, 0, 0),
					new StreamInfo(8, vertexCount * 40 + ((vertexCount & 1) != 0 ? (uint)8 : 0), 8, 0, 0),
					new StreamInfo(0, 0, 0, 0, 0),
					new StreamInfo(0, 0, 0, 0, 0)
				}
			);
			m_DataSize = new byte[vertexCount * 48 + ((vertexCount & 1) != 0 ? (uint)8 : 0)];
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_CurrentChannels = reader.ReadUInt32();
			m_VertexCount = reader.ReadUInt32();

			int numChannels = reader.ReadInt32();
			m_Channels = new List<ChannelInfo>(numChannels);
			for (int i = 0; i < numChannels; i++)
			{
				m_Channels.Add(new ChannelInfo(stream));
			}

			int numStreams = reader.ReadInt32();
			m_Streams = new List<StreamInfo>(numStreams);
			for (int i = 0; i < numStreams; i++)
			{
				m_Streams.Add(new StreamInfo(stream));
			}

			int numData = reader.ReadInt32();
			m_DataSize = reader.ReadBytes(numData);
			if ((numData & 3) > 0)
			{
				reader.ReadBytes(4 - (numData & 3));
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_CurrentChannels);
			writer.Write(m_VertexCount);

			writer.Write(m_Channels.Count);
			for (int i = 0; i < m_Channels.Count; i++)
			{
				m_Channels[i].WriteTo(stream);
			}

			writer.Write(m_Streams.Count);
			for (int i = 0; i < m_Streams.Count; i++)
			{
				m_Streams[i].WriteTo(stream);
			}

			writer.Write(m_DataSize.Length);
			writer.Write(m_DataSize);
			if ((m_DataSize.Length & 3) > 0)
			{
				writer.Write(new byte[4 - (m_DataSize.Length & 3)]);
			}
		}
	}

	public class ChannelInfo : IObjInfo
	{
		public byte stream { get; set; }
		public byte offset { get; set; }
		public byte format { get; set; }
		public byte dimension { get; set; }

		public ChannelInfo(Stream stream)
		{
			LoadFrom(stream);
		}

		public ChannelInfo(byte stream, byte offset, byte format, byte dimension)
		{
			this.stream = stream;
			this.offset = offset;
			this.format = format;
			this.dimension = dimension;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			this.stream = reader.ReadByte();
			offset = reader.ReadByte();
			format = reader.ReadByte();
			dimension = reader.ReadByte();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(this.stream);
			writer.Write(offset);
			writer.Write(format);
			writer.Write(dimension);
		}
	}

	public class StreamInfo : IObjInfo
	{
		public uint channelMask { get; set; }
		public uint offset { get; set; }
		public byte stride { get; set; }
		public byte dividerOp { get; set; }
		public UInt16 frequency { get; set; }

		public StreamInfo(Stream stream)
		{
			LoadFrom(stream);
		}

		public StreamInfo(uint channelMask, uint offset, byte stride, byte dividerOp, UInt16 frequency)
		{
			this.channelMask = channelMask;
			this.offset = offset;
			this.stride = stride;
			this.dividerOp = dividerOp;
			this.frequency = frequency;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			channelMask = reader.ReadUInt32();
			offset = reader.ReadUInt32();
			stride = reader.ReadByte();
			dividerOp = reader.ReadByte();
			frequency = reader.ReadUInt16();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(channelMask);
			writer.Write(offset);
			writer.Write(stride);
			writer.Write(dividerOp);
			writer.Write(frequency);
		}
	}

	public class CompressedMesh : IObjInfo
	{
		public PackedBitVector m_Vertices { get; set; }
		public PackedBitVector m_UV { get; set; }
		public PackedBitVector m_BindPoses { get; set; }
		public PackedBitVector m_Normals { get; set; }
		public PackedBitVector m_Tangents { get; set; }
		public PackedBitVector2 m_Weights { get; set; }
		public PackedBitVector2 m_NormalSigns { get; set; }
		public PackedBitVector2 m_TangentSigns { get; set; }
		public PackedBitVector2 m_BoneIndices { get; set; }
		public PackedBitVector2 m_Triangles { get; set; }
		public PackedBitVector2 m_Colors { get; set; }

		public CompressedMesh(Stream stream)
		{
			LoadFrom(stream);
		}

		public CompressedMesh()
		{
			m_Vertices = new PackedBitVector();
			m_UV = new PackedBitVector();
			m_BindPoses = new PackedBitVector();
			m_Normals = new PackedBitVector();
			m_Tangents = new PackedBitVector();
			m_Weights = new PackedBitVector2();
			m_NormalSigns = new PackedBitVector2();
			m_TangentSigns = new PackedBitVector2();
			m_BoneIndices = new PackedBitVector2();
			m_Triangles = new PackedBitVector2();
			m_Colors = new PackedBitVector2();
		}

		public void LoadFrom(Stream stream)
		{
			m_Vertices = new PackedBitVector(stream);
			m_UV = new PackedBitVector(stream);
			m_BindPoses = new PackedBitVector(stream);
			m_Normals = new PackedBitVector(stream);
			m_Tangents = new PackedBitVector(stream);
			m_Weights = new PackedBitVector2(stream);
			m_NormalSigns = new PackedBitVector2(stream);
			m_TangentSigns = new PackedBitVector2(stream);
			m_BoneIndices = new PackedBitVector2(stream);
			m_Triangles = new PackedBitVector2(stream);
			m_Colors = new PackedBitVector2(stream);
		}

		public void WriteTo(Stream stream)
		{
			m_Vertices.WriteTo(stream);
			m_UV.WriteTo(stream);
			m_BindPoses.WriteTo(stream);
			m_Normals.WriteTo(stream);
			m_Tangents.WriteTo(stream);
			m_Weights.WriteTo(stream);
			m_NormalSigns.WriteTo(stream);
			m_TangentSigns.WriteTo(stream);
			m_BoneIndices.WriteTo(stream);
			m_Triangles.WriteTo(stream);
			m_Colors.WriteTo(stream);
		}
	}

	public class PackedBitVector : IObjInfo
	{
		public uint m_NumItems { get; set; }
		public float m_Range { get; set; }
		public float m_Start { get; set; }
		public byte[] m_Data { get; set; }
		public byte m_BitSize { get; set; }

		public PackedBitVector(Stream stream)
		{
			LoadFrom(stream);
		}

		public PackedBitVector()
		{
			m_Data = new byte[0];
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_NumItems = reader.ReadUInt32();
			m_Range = reader.ReadSingle();
			m_Start = reader.ReadSingle();

			int numData = reader.ReadInt32();
			m_Data = reader.ReadBytes(numData);

			m_BitSize = reader.ReadByte();
			if ((numData + 1 & 3) > 0)
			{
				reader.ReadBytes(4 - (numData + 1 & 3));
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_NumItems);
			writer.Write(m_Range);
			writer.Write(m_Start);

			writer.Write(m_Data.Length);
			writer.Write(m_Data);

			writer.Write(m_BitSize);
			if ((m_Data.Length + 1 & 3) > 0)
			{
				writer.Write(new byte[4 - (m_Data.Length + 1 & 3)]);
			}
		}
	}

	public class PackedBitVector2 : IObjInfo
	{
		public uint m_NumItems { get; set; }
		public byte[] m_Data { get; set; }
		public byte m_BitSize { get; set; }

		public PackedBitVector2(Stream stream)
		{
			LoadFrom(stream);
		}

		public PackedBitVector2()
		{
			m_Data = new byte[0];
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_NumItems = reader.ReadUInt32();

			int numData = reader.ReadInt32();
			m_Data = reader.ReadBytes(numData);

			m_BitSize = reader.ReadByte();
			if ((numData + 1 & 3) > 0)
			{
				reader.ReadBytes(4 - (numData + 1 & 3));
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_NumItems);

			writer.Write(m_Data.Length);
			writer.Write(m_Data);

			writer.Write(m_BitSize);
			if ((m_Data.Length + 1 & 3) > 0)
			{
				writer.Write(new byte[4 - (m_Data.Length + 1 & 3)]);
			}
		}
	}

	public class Mesh : Component
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public List<SubMesh> m_SubMeshes { get; set; }
		public BlendShapeData m_Shapes { get; set; }
		public List<Matrix> m_BindPose { get; set; }
		public List<uint> m_BoneNameHashes { get; set; }
		public uint m_RootBoneNameHash { get; set; }
		public byte m_MeshCompression { get; set; }
		public byte m_StreamCompression { get; set; }
		public bool m_IsReadable { get; set; }
		public bool m_KeepVertices { get; set; }
		public bool m_KeepIndices { get; set; }
		public byte[] m_IndexBuffer { get; set; }
		public List<BoneInfluence> m_Skin { get; set; }
		public VertexData m_VertexData { get; set; }
		public CompressedMesh m_CompressedMesh { get; set; }
		public AABB m_LocalAABB { get; set; }
		public int m_MeshUsageFlags { get; set; }

		public Mesh(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public Mesh(AssetCabinet file) :
			this(file, 0, UnityClassID.Mesh, UnityClassID.Mesh)
		{
			file.ReplaceSubfile(-1, this, null);

			m_Shapes = new BlendShapeData();
			m_IsReadable = true;
			m_KeepVertices = true;
			m_KeepIndices = true;
			m_CompressedMesh = new CompressedMesh();
			m_LocalAABB = new AABB();
			m_MeshUsageFlags = 1;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();

			int numSubMeshes = reader.ReadInt32();
			m_SubMeshes = new List<SubMesh>(numSubMeshes);
			for (int i = 0; i < numSubMeshes; i++)
			{
				m_SubMeshes.Add(new SubMesh(stream));
			}

			m_Shapes = new BlendShapeData(stream);

			int numBones = reader.ReadInt32();
			m_BindPose = new List<Matrix>(numBones);
			for (int i = 0; i < numBones; i++)
			{
				m_BindPose.Add(reader.ReadMatrix());
			}

			int numHashes = reader.ReadInt32();
			m_BoneNameHashes = new List<uint>(numHashes);
			for (int i = 0; i < numHashes; i++)
			{
				m_BoneNameHashes.Add(reader.ReadUInt32());
			}

			m_RootBoneNameHash = reader.ReadUInt32();
			m_MeshCompression = reader.ReadByte();
			m_StreamCompression = reader.ReadByte();
			m_IsReadable = reader.ReadBoolean();
			m_KeepVertices = reader.ReadBoolean();
			m_KeepIndices = reader.ReadBoolean();
			reader.BaseStream.Position += 3;

			int numIndexBytes = reader.ReadInt32();
			m_IndexBuffer = reader.ReadBytes(numIndexBytes);
			if ((numIndexBytes & 3) > 0)
			{
				reader.BaseStream.Position += 4 - (numIndexBytes & 3);
			}

			int numInfluences = reader.ReadInt32();
			m_Skin = new List<BoneInfluence>(numInfluences);
			for (int i = 0; i < numInfluences; i++)
			{
				m_Skin.Add(new BoneInfluence(stream));
			}

			m_VertexData = new VertexData(stream);
			m_CompressedMesh = new CompressedMesh(stream);
			m_LocalAABB = new AABB(stream);
			m_MeshUsageFlags = reader.ReadInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);

			writer.Write(m_SubMeshes.Count);
			for (int i = 0; i < m_SubMeshes.Count; i++)
			{
				m_SubMeshes[i].WriteTo(stream);
			}

			m_Shapes.WriteTo(stream);

			writer.Write(m_BindPose.Count);
			for (int i = 0; i < m_BindPose.Count; i++)
			{
				writer.Write(m_BindPose[i]);
			}

			writer.Write(m_BoneNameHashes.Count);
			for (int i = 0; i < m_BoneNameHashes.Count; i++)
			{
				writer.Write(m_BoneNameHashes[i]);
			}

			writer.Write(m_RootBoneNameHash);
			writer.Write(m_MeshCompression);
			writer.Write(m_StreamCompression);
			writer.Write(m_IsReadable);
			writer.Write(m_KeepVertices);
			writer.Write(m_KeepIndices);
			writer.BaseStream.Position += 3;

			writer.Write(m_IndexBuffer.Length);
			writer.Write(m_IndexBuffer);
			if ((m_IndexBuffer.Length & 3) > 0)
			{
				writer.BaseStream.Position += 4 - (m_IndexBuffer.Length & 3);
			}

			writer.Write(m_Skin.Count);
			for (int i = 0; i < m_Skin.Count; i++)
			{
				m_Skin[i].WriteTo(stream);
			}

			m_VertexData.WriteTo(stream);
			m_CompressedMesh.WriteTo(stream);
			m_LocalAABB.WriteTo(stream);
			writer.Write(m_MeshUsageFlags);
		}

		public Mesh Clone(AssetCabinet file)
		{
			file.MergeTypeDefinition(this.file, UnityClassID.Mesh);

			Mesh mesh = new Mesh(file);
			using (MemoryStream mem = new MemoryStream())
			{
				WriteTo(mem);
				mem.Position = 0;
				mesh.LoadFrom(mem);
			}
			return mesh;
		}
	}
}
