using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public static partial class Operations
	{
		public static Transform FindFrame(string name, Transform root)
		{
			Transform frame = root;
			if ((frame != null) && (frame.m_GameObject.instance.m_Name == name))
			{
				return frame;
			}

			for (int i = 0; i < root.Count; i++)
			{
				if ((frame = FindFrame(name, root[i])) != null)
				{
					return frame;
				}
			}

			return null;
		}

		public static int FindBoneIndex(List<PPtr<Transform>> boneList, Transform boneFrame)
		{
			for (int i = 0; i < boneList.Count; i++)
			{
				if (boneList[i].instance == boneFrame)
				{
					return i;
				}
			}
			return -1;
		}

		public static List<MeshRenderer> FindMeshes(Transform rootFrame, List<string> nameList)
		{
			List<MeshRenderer> meshList = new List<MeshRenderer>(nameList.Count);
			FindMeshFrames(rootFrame, meshList, nameList);
			return meshList;
		}

		static void FindMeshFrames(Transform frame, List<MeshRenderer> meshList, List<string> nameList)
		{
			MeshRenderer mesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
			if (mesh == null)
			{
				mesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshRenderer);
			}
			if ((mesh != null) && nameList.Contains(frame.m_GameObject.instance.m_Name))
			{
				meshList.Add(mesh);
			}

			for (int i = 0; i < frame.Count; i++)
			{
				FindMeshFrames(frame[i], meshList, nameList);
			}
		}

		public static Material FindMaterial(List<Material> matList, string name)
		{
			foreach (Material mat in matList)
			{
				if (mat.m_Name == name)
				{
					return mat;
				}
			}
			return null;
		}

		public static Texture2D FindTexture(List<Texture2D> texList, string name)
		{
			foreach (Texture2D tex in texList)
			{
				if (name.Contains(tex.m_Name))
				{
					return tex;
				}
			}
			return null;
		}

		public static void CopyUnknowns(Transform src, Transform dest)
		{
			dest.m_GameObject.instance.m_Layer = src.m_GameObject.instance.m_Layer;
			dest.m_GameObject.instance.m_Tag = src.m_GameObject.instance.m_Tag;
			dest.m_GameObject.instance.m_isActive = src.m_GameObject.instance.m_isActive;
		}

		public static void CreateUnknowns(Transform frame)
		{
			frame.m_GameObject.instance.m_isActive = true;
		}

		public static void CopyUnknowns(SkinnedMeshRenderer src, SkinnedMeshRenderer dest)
		{
			dest.m_Enabled = src.m_Enabled;
			dest.m_CastShadows = src.m_CastShadows;
			dest.m_ReceiveShadows = src.m_CastShadows;
			dest.m_LightmapIndex = src.m_LightmapIndex;
			dest.m_LightmapTilingOffset = src.m_LightmapTilingOffset;
			//m_SubsetIndices
			dest.m_StaticBatchRoot = src.m_StaticBatchRoot;
			dest.m_UseLightProbes = src.m_UseLightProbes;
			dest.m_LightProbeAnchor = src.m_LightProbeAnchor;
			dest.m_SortingLayerID = src.m_SortingLayerID;
			dest.m_SortingOrder = src.m_SortingOrder;
			dest.m_Quality = src.m_Quality;
			dest.m_UpdateWhenOffScreen = src.m_UpdateWhenOffScreen;
			//m_BlendShapeWeights
			dest.m_RootBone = src.m_RootBone;
			dest.m_AABB = src.m_AABB;
			dest.m_DirtyAABB = src.m_DirtyAABB;

			if (dest.m_Mesh.instance != null && src.m_Mesh.instance != null)
			{
				Mesh destMesh = dest.m_Mesh.instance;
				Mesh srcMesh = src.m_Mesh.instance;

				destMesh.m_Name = (string)srcMesh.m_Name.Clone();
				for (int i = 0; i < srcMesh.m_SubMeshes.Count; i++)
				{
					destMesh.m_SubMeshes[i].topology = srcMesh.m_SubMeshes[i].topology;
				}
				//m_Shapes
				destMesh.m_IsReadable = srcMesh.m_IsReadable;
				destMesh.m_KeepVertices = srcMesh.m_KeepVertices;
				destMesh.m_KeepIndices = srcMesh.m_KeepIndices;
				destMesh.m_MeshUsageFlags = srcMesh.m_MeshUsageFlags;
			}
		}

		public static void CopyUnknowns(SubMesh src, SubMesh dst)
		{
			dst.topology = src.topology;
		}

		public static List<PPtr<Transform>> MergeBoneList(List<PPtr<Transform>> boneList1, List<PPtr<Transform>> boneList2, out int[] boneList2IdxMap)
		{
			boneList2IdxMap = new int[boneList2.Count];
			Dictionary<string, int> boneDic = new Dictionary<string, int>();
			List<PPtr<Transform>> mergedList = new List<PPtr<Transform>>(boneList1.Count + boneList2.Count);
			for (int i = 0; i < boneList1.Count; i++)
			{
				PPtr<Transform> transPtr = new PPtr<Transform>(boneList1[i].instance);
				boneDic.Add(transPtr.instance.m_GameObject.instance.m_Name, i);
				mergedList.Add(transPtr);
			}
			for (int i = 0; i < boneList2.Count; i++)
			{
				PPtr<Transform> transPtr = new PPtr<Transform>(boneList2[i].instance);
				int boneIdx;
				if (boneDic.TryGetValue(transPtr.instance.m_GameObject.instance.m_Name, out boneIdx))
				{
					mergedList[boneIdx] = transPtr;
				}
				else
				{
					boneIdx = mergedList.Count;
					mergedList.Add(transPtr);
					boneDic.Add(transPtr.instance.m_GameObject.instance.m_Name, boneIdx);
				}
				boneList2IdxMap[i] = boneIdx;
			}
			return mergedList;
		}

		public class vVertex
		{
			public Vector3 position;
			public Vector3 normal;
			public Vector4 tangent;
			public Vector2 uv;
			public int[] boneIndices;
			public float[] weights;
		}

		public class vSubmesh
		{
			public List<vVertex> vertexList;

			public vSubmesh(Mesh mesh, int submeshIdx)
			{
				SubMesh s = mesh.m_SubMeshes[submeshIdx];
				int numVertices = (int)s.vertexCount;
				List<BoneInfluence> weightList = mesh.m_Skin;
				vertexList = new List<vVertex>(numVertices);
				using (BinaryReader reader = new BinaryReader(new MemoryStream(mesh.m_VertexData.m_DataSize)))
				{
					for (int str = 0; str < mesh.m_VertexData.m_Streams.Count; str++)
					{
						StreamInfo sInfo = mesh.m_VertexData.m_Streams[str];
						if (sInfo.channelMask == 0)
						{
							continue;
						}

						for (int j = 0; j < numVertices; j++)
						{
							vVertex vVertex;
							if (vertexList.Count < numVertices)
							{
								vVertex = new vVertex();
								vertexList.Add(vVertex);
							}
							else
							{
								vVertex = vertexList[j];
							}

							for (int chn = 0; chn < mesh.m_VertexData.m_Channels.Count; chn++)
							{
								ChannelInfo cInfo = mesh.m_VertexData.m_Channels[chn];
								if ((sInfo.channelMask & (1 << chn)) == 0)
								{
									continue;
								}

								reader.BaseStream.Position = /*s.firstByte +*/ sInfo.offset + /*(*/j/*+s.firstVertex)*/ * sInfo.stride + cInfo.offset;
								switch (chn)
								{
								case 0:
									vVertex.position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
									break;
								case 1:
									vVertex.normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
									break;
								case 3:
									vVertex.uv = new Vector2(reader.ReadSingle(), reader.ReadSingle());
									break;
								case 5:
									vVertex.tangent = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
									break;
								}
							}

							if (vVertex.boneIndices == null && weightList.Count > 0)
							{
								//s.firstVertex
								vVertex.boneIndices = weightList[j].boneIndex;
								vVertex.weights = weightList[j].weight;
							}
						}
					}
				}
			}
		}

		public class vMesh
		{
			public List<vSubmesh> submeshes;
			protected Mesh mesh;

			public vMesh(Mesh mesh)
			{
				this.mesh = mesh;

				submeshes = new List<vSubmesh>(mesh.m_SubMeshes.Count);
				for (int i = 0; i < mesh.m_SubMeshes.Count; i++)
				{
					submeshes.Add(new vSubmesh(mesh, i));
				}
			}

			public void Flush()
			{
				List<vVertex> vertexList = submeshes[0].vertexList;
				if (vertexList[0].weights != null)
				{
					mesh.m_Skin.Capacity = vertexList.Count;
				}
				using (BinaryWriter writer = new BinaryWriter(new MemoryStream(mesh.m_VertexData.m_DataSize)))
				{
					for (int str = 0; str < mesh.m_VertexData.m_Streams.Count; str++)
					{
						StreamInfo sInfo = mesh.m_VertexData.m_Streams[str];
						if (sInfo.channelMask == 0)
						{
							continue;
						}

						for (int j = 0; j < mesh.m_VertexData.m_VertexCount; j++)
						{
							vVertex vert = vertexList[j];
							for (int chn = 0; chn < mesh.m_VertexData.m_Channels.Count; chn++)
							{
								ChannelInfo cInfo = mesh.m_VertexData.m_Channels[chn];
								if ((sInfo.channelMask & (1 << chn)) == 0)
								{
									continue;
								}

								writer.BaseStream.Position = sInfo.offset + j * sInfo.stride + cInfo.offset;
								switch (chn)
								{
								case 0:
									writer.Write(vert.position);
									break;
								case 1:
									writer.Write(vert.normal);
									break;
								case 3:
									writer.Write(vert.uv);
									break;
								case 5:
									writer.Write(vert.tangent);
									break;
								}
							}

							if (str == 0 && vert.weights != null)
							{
								BoneInfluence item;
								if (mesh.m_Skin.Count <= j)
								{
									item = new BoneInfluence();
									mesh.m_Skin.Add(item);
								}
								else
								{
									item = mesh.m_Skin[j];
								}
								vert.boneIndices.CopyTo(item.boneIndex, 0);
								vert.weights.CopyTo(item.weight, 0);
							}
						}
					}
				}
			}
		}

		public static void CopyNormalsOrder(List<vVertex> src, List<vVertex> dest)
		{
			int len = (src.Count < dest.Count) ? src.Count : dest.Count;
			for (int i = 0; i < len; i++)
			{
				dest[i].normal = src[i].normal;
				dest[i].tangent = src[i].tangent;
			}
		}

		public static void CopyNormalsNear(List<vVertex> src, List<vVertex> dest)
		{
			for (int i = 0; i < dest.Count; i++)
			{
				var destVert = dest[i];
				var destPos = destVert.position;
				float minDistSq = Single.MaxValue;
				vVertex nearestVert = null;
				foreach (vVertex srcVert in src)
				{
					var srcPos = srcVert.position;
					float[] diff = new float[] { destPos[0] - srcPos[0], destPos[1] - srcPos[1], destPos[2] - srcPos[2] };
					float distSq = (diff[0] * diff[0]) + (diff[1] * diff[1]) + (diff[2] * diff[2]);
					if (distSq < minDistSq)
					{
						minDistSq = distSq;
						nearestVert = srcVert;
					}
				}

				destVert.normal = nearestVert.normal;
				destVert.tangent = nearestVert.tangent;
			}
		}

		public static void CopyBonesOrder(List<vVertex> src, List<vVertex> dest)
		{
			int len = (src.Count < dest.Count) ? src.Count : dest.Count;
			for (int i = 0; i < len; i++)
			{
				dest[i].boneIndices = (int[])src[i].boneIndices.Clone();
				dest[i].weights = (float[])src[i].weights.Clone();
			}
		}

		public static void CopyBonesNear(List<vVertex> src, List<vVertex> dest)
		{
			for (int i = 0; i < dest.Count; i++)
			{
				var destVert = dest[i];
				var destPos = destVert.position;
				float minDistSq = Single.MaxValue;
				vVertex nearestVert = null;
				foreach (vVertex srcVert in src)
				{
					var srcPos = srcVert.position;
					float[] diff = new float[] { destPos[0] - srcPos[0], destPos[1] - srcPos[1], destPos[2] - srcPos[2] };
					float distSq = (diff[0] * diff[0]) + (diff[1] * diff[1]) + (diff[2] * diff[2]);
					if (distSq < minDistSq)
					{
						minDistSq = distSq;
						nearestVert = srcVert;
					}
				}

				destVert.boneIndices = (int[])nearestVert.boneIndices.Clone();
				destVert.weights = (float[])nearestVert.weights.Clone();
			}
		}
	}
}
