using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public static partial class Operations
	{
		public static Transform CreateTransformTree(Animator parser, ImportedFrame frame, Transform parent)
		{
			Transform trans = new Transform(parser.file);
			GameObject gameObj = new GameObject(parser.file);
			gameObj.m_Name = (string)frame.Name.Clone();
			gameObj.AddLinkedComponent(trans);

			string framePath = parser.m_Avatar.instance.BonePath(gameObj.m_Name);
			if (framePath == null)
			{
				parser.m_Avatar.instance.AddBone(parent, trans);
			}

			Vector3 t, s;
			Quaternion r;
			frame.Matrix.Decompose(out s, out r, out t);
			trans.m_LocalRotation = r;
			trans.m_LocalPosition = t;
			trans.m_LocalScale = s;

			trans.InitChildren(frame.Count);
			for (int i = 0; i < frame.Count; i++)
			{
				trans.AddChild(CreateTransformTree(parser, frame[i], trans));
			}

			return trans;
		}

		public static void CopyOrCreateUnknowns(Transform dest, Transform root)
		{
			Transform src = FindFrame(dest.m_GameObject.instance.m_Name, root);
			if (src == null)
			{
				CreateUnknowns(dest);
			}
			else
			{
				CopyUnknowns(src, dest);
			}

			for (int i = 0; i < dest.Count; i++)
			{
				CopyOrCreateUnknowns(dest[i], root);
			}
		}

		public static List<PPtr<Transform>> CreateBoneList(Transform root, List<ImportedBone> boneList, List<Matrix> poseMatrices)
		{
			List<PPtr<Transform>> uBoneList = new List<PPtr<Transform>>(boneList.Count);
			for (int i = 0; i < boneList.Count; i++)
			{
				Transform boneFrame = FindFrame(boneList[i].Name, root);
				if (boneFrame == null)
				{
					throw new Exception("BoneFrame " + boneList[i].Name + " not found.");
				}
				poseMatrices.Add(Matrix.Transpose(boneList[i].Matrix));
				uBoneList.Add(new PPtr<Transform>(boneFrame));
			}
			return uBoneList;
		}

		public static SkinnedMeshRenderer CreateSkinnedMeshRenderer(Animator parser, AssetBundle bundle, List<Material> materials, WorkspaceMesh mesh, out int[] indices, out bool[] worldCoords, out bool[] replaceSubmeshesOption)
		{
			int numUncheckedSubmeshes = 0;
			foreach (ImportedSubmesh submesh in mesh.SubmeshList)
			{
				if (!mesh.isSubmeshEnabled(submesh))
					numUncheckedSubmeshes++;
			}
			int numSubmeshes = mesh.SubmeshList.Count - numUncheckedSubmeshes;
			indices = new int[numSubmeshes];
			worldCoords = new bool[numSubmeshes];
			replaceSubmeshesOption = new bool[numSubmeshes];

			SkinnedMeshRenderer sMesh = new SkinnedMeshRenderer(parser.file);

			sMesh.m_Materials.Capacity = mesh.SubmeshList.Count;
			foreach (ImportedSubmesh submesh in mesh.SubmeshList)
			{
				Material matFound = materials.Find
				(
					delegate(Material mat)
					{
						return mat.m_Name == submesh.Material;
					}
				);
				if (matFound != null)
				{
					sMesh.m_Materials.Add(new PPtr<Material>(matFound));
				}
			}
			Mesh uMesh = new Mesh(parser.file);
			sMesh.m_Mesh = new PPtr<Mesh>(uMesh);

			bool meshesFound = false;
			for (int i = 0; i < bundle.m_PreloadTable.Count; i++)
			{
				PPtr<Object> objPtr = bundle.m_PreloadTable[i];
				if (objPtr.asset != null)
				{
					if (objPtr.asset.classID1 == UnityClassID.Mesh)
					{
						meshesFound = true;
					}
					else if (meshesFound)
					{
						bundle.m_PreloadTable.Insert(i, new PPtr<Object>(uMesh));
						break;
					}
				}
			}
			bool animatorFound = false, skinnedFound = false;
			for (int i = 0; i < bundle.m_PreloadTable.Count; i++)
			{
				PPtr<Object> objPtr = bundle.m_PreloadTable[i];
				if (objPtr.asset != null)
				{
					if (bundle.m_PreloadTable[i].asset == parser)
					{
						animatorFound = true;
					}
					else if (animatorFound && objPtr.asset.classID1 == UnityClassID.SkinnedMeshRenderer)
					{
						skinnedFound = true;
					}
					else if (skinnedFound && objPtr.asset.classID1 != UnityClassID.SkinnedMeshRenderer)
					{
						bundle.m_PreloadTable.Insert(i, new PPtr<Object>(sMesh));
						break;
					}
				}
			}
			/*AssetInfo aInfo = new AssetInfo(parser.file);
			aInfo.preloadIndex = 1;
			aInfo.preloadSize = 1182;
			aInfo.asset = new PPtr<Object>(uMesh);
			bundle.m_Container.Add(new KeyValuePair<string, AssetInfo>(parser.m_GameObject.instance.m_Name, aInfo));*/

			uMesh.m_Name = mesh.Name;
			uMesh.m_SubMeshes.Capacity = mesh.SubmeshList.Count;

			List<Matrix> poseMatrices = new List<Matrix>(mesh.BoneList.Count);
			sMesh.m_Bones = CreateBoneList(parser.RootTransform, mesh.BoneList, poseMatrices);
			uMesh.m_BindPose.InsertRange(0, poseMatrices);
			uMesh.m_BoneNameHashes.Capacity = poseMatrices.Count;
			for (int i = 0; i < mesh.BoneList.Count; i++)
			{
				string bone = mesh.BoneList[i].Name;
				uint hash = parser.m_Avatar.instance.BoneHash(bone);
				uMesh.m_BoneNameHashes.Add(hash);
			}

			for (int i = 0, submeshIdx = 0; i < numSubmeshes; i++, submeshIdx++)
			{
				while (!mesh.isSubmeshEnabled(mesh.SubmeshList[submeshIdx]))
				{
					submeshIdx++;
				}

				if (uMesh.m_SubMeshes.Count == 1)
				{
					throw new Exception("Only one submesh is supported now.");
				}
				SubMesh submesh = new SubMesh();
				submesh.indexCount = (uint)mesh.SubmeshList[submeshIdx].FaceList.Count * 3;
				submesh.vertexCount = (uint)mesh.SubmeshList[submeshIdx].VertexList.Count;
				uMesh.m_SubMeshes.Add(submesh);
				uMesh.m_Skin.Capacity = (int)submesh.vertexCount;
				uMesh.m_VertexData.m_VertexCount = submesh.vertexCount;
				uMesh.m_VertexData.m_Streams[1].offset = submesh.vertexCount * 40;
				uMesh.m_VertexData.m_DataSize = new byte[submesh.vertexCount * 48];

				indices[i] = mesh.SubmeshList[submeshIdx].Index;
				worldCoords[i] = mesh.SubmeshList[submeshIdx].WorldCoords;
				replaceSubmeshesOption[i] = mesh.isSubmeshReplacingOriginal(mesh.SubmeshList[submeshIdx]);

				List<ImportedVertex> vertexList = mesh.SubmeshList[submeshIdx].VertexList;
				Vector3 min = new Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue);
				Vector3 max = new Vector3(Single.MinValue, Single.MinValue, Single.MinValue);
				using (BinaryWriter writer = new BinaryWriter(new MemoryStream(uMesh.m_VertexData.m_DataSize)))
				{
					for (int str = 0; str < uMesh.m_VertexData.m_Streams.Count; str++)
					{
						StreamInfo sInfo = uMesh.m_VertexData.m_Streams[str];
						if (sInfo.channelMask == 0)
						{
							continue;
						}

						for (int j = 0; j < uMesh.m_VertexData.m_VertexCount; j++)
						{
							ImportedVertex vert = vertexList[j];
							for (int chn = 0; chn < uMesh.m_VertexData.m_Channels.Count; chn++)
							{
								ChannelInfo cInfo = uMesh.m_VertexData.m_Channels[chn];
								if ((sInfo.channelMask & (1 << chn)) == 0)
								{
									continue;
								}

								writer.BaseStream.Position = sInfo.offset + j * sInfo.stride + cInfo.offset;
								switch (chn)
								{
								case 0:
									writer.Write(vert.Position);
									min = Vector3.Minimize(min, vert.Position);
									max = Vector3.Maximize(max, vert.Position);
									break;
								case 1:
									writer.Write(vert.Normal);
									break;
								case 3:
									writer.Write(vert.UV);
									break;
								case 5:
									writer.Write(vert.Tangent);
									break;
								}
							}

							if (sMesh.m_Bones.Count > 0 && uMesh.m_Skin.Count < uMesh.m_VertexData.m_VertexCount)
							{
								BoneInfluence item = new BoneInfluence();
								for (int k = 0; k < 4; k++)
								{
									item.boneIndex[k] = vert.BoneIndices[k] != 0xFF ? vert.BoneIndices[k] : -1;
								}
								vert.Weights.CopyTo(item.weight, 0);
								uMesh.m_Skin.Add(item);
							}
						}
					}
				}
				uMesh.m_SubMeshes[0].localAABB.m_Extend = (max - min) / 2;
				uMesh.m_SubMeshes[0].localAABB.m_Center = min + uMesh.m_SubMeshes[0].localAABB.m_Extend;
				uMesh.m_LocalAABB.m_Extend = uMesh.m_SubMeshes[0].localAABB.m_Extend;
				uMesh.m_LocalAABB.m_Center = uMesh.m_SubMeshes[0].localAABB.m_Center;

				List<ImportedFace> faceList = mesh.SubmeshList[submeshIdx].FaceList;
				uMesh.m_IndexBuffer = new byte[faceList.Count * sizeof(ushort) * 3];
				using (BinaryWriter writer = new BinaryWriter(new MemoryStream(uMesh.m_IndexBuffer)))
				{
					for (int j = 0; j < faceList.Count; j++)
					{
						int[] vertexIndices = faceList[j].VertexIndices;
						writer.Write((ushort)vertexIndices[0]);
						writer.Write((ushort)vertexIndices[1]);
						writer.Write((ushort)vertexIndices[2]);
					}
				}
			}

			return sMesh;
		}

		public static void ReplaceSkinnedMeshRenderer(Transform frame, Transform rootBone, Animator parser, AssetBundle bundle, List<Material> materials, WorkspaceMesh mesh, bool merge, CopyMeshMethod normalsMethod, CopyMeshMethod bonesMethod)
		{
			Matrix transform = Transform.WorldTransform(frame);
			transform.Invert();

			int[] indices;
			bool[] worldCoords;
			bool[] replaceSubmeshesOption;
			SkinnedMeshRenderer sMesh = CreateSkinnedMeshRenderer(parser, bundle, materials, mesh, out indices, out worldCoords, out replaceSubmeshesOption);
			vMesh destMesh = new Operations.vMesh(sMesh.m_Mesh.instance);

			SkinnedMeshRenderer frameMesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
			vMesh srcMesh = null;
			if (frameMesh == null)
			{
				sMesh.m_RootBone = new PPtr<Transform>(rootBone);
				if (rootBone != null)
				{
					sMesh.m_Mesh.instance.m_RootBoneNameHash = parser.m_Avatar.instance.BoneHash(rootBone.m_GameObject.instance.m_Name);
				}
				CreateUnknowns(sMesh);
			}
			else
			{
				sMesh.m_RootBone = new PPtr<Transform>(frameMesh.m_RootBone.instance);
				sMesh.m_Mesh.instance.m_RootBoneNameHash = frameMesh.m_Mesh.instance.m_RootBoneNameHash;

				srcMesh = new Operations.vMesh(frameMesh.m_Mesh.instance);
				CopyUnknowns(frameMesh, sMesh);

				if ((bonesMethod == CopyMeshMethod.CopyOrder) || (bonesMethod == CopyMeshMethod.CopyNear))
				{
					sMesh.m_Bones.Clear();
					sMesh.m_Bones.Capacity = frameMesh.m_Bones.Count;
					for (int i = 0; i < frameMesh.m_Bones.Count; i++)
					{
						sMesh.m_Bones.Add(new PPtr<Transform>(frameMesh.m_Bones[i].instance));
					}
					sMesh.m_Mesh.instance.m_BoneNameHashes.Clear();
					sMesh.m_Mesh.instance.m_BoneNameHashes.AddRange(frameMesh.m_Mesh.instance.m_BoneNameHashes);

					sMesh.m_Mesh.instance.m_BindPose.Clear();
					sMesh.m_Mesh.instance.m_BindPose.AddRange(frameMesh.m_Mesh.instance.m_BindPose);
				}
			}
			if (sMesh.m_RootBone.instance != null)
			{
				Matrix rootBoneMatrix = Transform.WorldTransform(sMesh.m_RootBone.instance);
				rootBoneMatrix.Invert();
				sMesh.m_AABB.m_Center = Vector3.TransformCoordinate(sMesh.m_Mesh.instance.m_LocalAABB.m_Center, rootBoneMatrix);
				sMesh.m_AABB.m_Extend = sMesh.m_Mesh.instance.m_LocalAABB.m_Extend;
			}

			vSubmesh[] replaceSubmeshes = (frameMesh == null) ? null : new vSubmesh[srcMesh.submeshes.Count];
			List<vSubmesh> addSubmeshes = new List<vSubmesh>(destMesh.submeshes.Count);
			for (int i = 0; i < destMesh.submeshes.Count; i++)
			{
				vSubmesh submesh = destMesh.submeshes[i];
				List<vVertex> vVertexList = submesh.vertexList;
				if (worldCoords[i])
				{
					for (int j = 0; j < vVertexList.Count; j++)
					{
						vVertexList[j].position = Vector3.TransformCoordinate(vVertexList[j].position, transform);
					}
				}

				vSubmesh baseSubmesh = null;
				int idx = indices[i];
				if ((frameMesh != null) && (idx >= 0) && (idx < frameMesh.m_Mesh.instance.m_SubMeshes.Count))
				{
					baseSubmesh = srcMesh.submeshes[idx];
					CopyUnknowns(frameMesh.m_Mesh.instance.m_SubMeshes[idx], sMesh.m_Mesh.instance.m_SubMeshes[i]);
				}

				if (baseSubmesh != null)
				{
					if (normalsMethod == CopyMeshMethod.CopyOrder)
					{
						Operations.CopyNormalsOrder(baseSubmesh.vertexList, submesh.vertexList);
					}
					else if (normalsMethod == CopyMeshMethod.CopyNear)
					{
						Operations.CopyNormalsNear(baseSubmesh.vertexList, submesh.vertexList);
					}

					if (bonesMethod == CopyMeshMethod.CopyOrder)
					{
						Operations.CopyBonesOrder(baseSubmesh.vertexList, submesh.vertexList);
					}
					else if (bonesMethod == CopyMeshMethod.CopyNear)
					{
						Operations.CopyBonesNear(baseSubmesh.vertexList, submesh.vertexList);
					}
				}

				if ((baseSubmesh != null) && merge && replaceSubmeshesOption[i])
				{
					replaceSubmeshes[idx] = submesh;
				}
				else
				{
					addSubmeshes.Add(submesh);
				}
			}

			if ((frameMesh != null) && merge)
			{
				destMesh.submeshes = new List<vSubmesh>(replaceSubmeshes.Length + addSubmeshes.Count);
				List<vSubmesh> copiedSubmeshes = new List<vSubmesh>(replaceSubmeshes.Length);
				for (int i = 0; i < replaceSubmeshes.Length; i++)
				{
					if (replaceSubmeshes[i] == null)
					{
						throw new Exception("Bad position of submesh - only one submesh suppported");
						/*vSubmesh vSubmesh = srcMesh.submeshes[i].Clone();
						copiedSubmeshes.Add(vSubmesh);
						destMesh.submeshes.Add(vSubmesh);*/
					}
					else
					{
						destMesh.submeshes.Add(replaceSubmeshes[i]);
					}
				}
				destMesh.submeshes.AddRange(addSubmeshes);

				if ((frameMesh.m_Bones.Count == 0) && (sMesh.m_Bones.Count > 0))
				{
					for (int i = 0; i < copiedSubmeshes.Count; i++)
					{
						List<vVertex> vertexList = copiedSubmeshes[i].vertexList;
						for (int j = 0; j < vertexList.Count; j++)
						{
							vertexList[j].boneIndices = new int[4] { -1, -1, -1, -1 };
						}
					}
				}
				else if ((frameMesh.m_Bones.Count > 0) && (sMesh.m_Bones.Count == 0))
				{
					for (int i = 0; i < replaceSubmeshes.Length; i++)
					{
						if (replaceSubmeshes[i] != null)
						{
							List<vVertex> vertexList = replaceSubmeshes[i].vertexList;
							for (int j = 0; j < vertexList.Count; j++)
							{
								vertexList[j].boneIndices = new int[4] { -1, -1, -1, -1 };
							}
						}
					}
					for (int i = 0; i < addSubmeshes.Count; i++)
					{
						List<vVertex> vertexList = addSubmeshes[i].vertexList;
						for (int j = 0; j < vertexList.Count; j++)
						{
							vertexList[j].boneIndices = new int[4] { -1, -1, -1, -1 };
						}
					}
				}
				else if ((frameMesh.m_Bones.Count > 0) && (sMesh.m_Bones.Count > 0))
				{
					int[] boneIdxMap;
					sMesh.m_Bones = MergeBoneList(frameMesh.m_Bones, sMesh.m_Bones, out boneIdxMap);
					for (int i = 0; i < replaceSubmeshes.Length; i++)
					{
						if (replaceSubmeshes[i] != null)
						{
							List<vVertex> vertexList = replaceSubmeshes[i].vertexList;
							for (int j = 0; j < vertexList.Count; j++)
							{
								int[] boneIndices = vertexList[j].boneIndices;
								vertexList[j].boneIndices = new int[4];
								for (int k = 0; k < 4; k++)
								{
									vertexList[j].boneIndices[k] = boneIndices[k] != -1 ? boneIdxMap[boneIndices[k]] : -1;
								}
							}
						}
					}
					for (int i = 0; i < addSubmeshes.Count; i++)
					{
						List<vVertex> vertexList = addSubmeshes[i].vertexList;
						for (int j = 0; j < vertexList.Count; j++)
						{
							int[] boneIndices = vertexList[j].boneIndices;
							vertexList[j].boneIndices = new int[4];
							for (int k = 0; k < 4; k++)
							{
								vertexList[j].boneIndices[k] = boneIndices[k] != -1 ? boneIdxMap[boneIndices[k]] : -1;
							}
						}
					}
				}
			}
			destMesh.Flush();

			if (frameMesh != null)
			{
				frame.m_GameObject.instance.RemoveLinkedComponent(frameMesh);
				parser.file.RemoveSubfile(frameMesh);
				parser.file.RemoveSubfile(frameMesh.m_Mesh.instance);

				bundle.DeleteComponent(frameMesh.m_Mesh.asset);
				bundle.DeleteComponent(frameMesh);
			}
			frame.m_GameObject.instance.AddLinkedComponent(sMesh);
		}

		public static void ReplaceMaterial(UnityParser parser, ImportedMaterial material)
		{
			for (int i = 0; i < parser.Cabinet.Components.Count; i++)
			{
				Component comp = parser.Cabinet.Components[i];
				if (comp.classID1 == UnityClassID.Material)
				{
					Material mat = parser.Cabinet.LoadComponent(comp.pathID);
					if (mat.m_Name == material.Name)
					{
						ReplaceMaterial(mat, material);
						return;
					}
				}
			}

			throw new Exception("Replacing a material currently requires an existing material with the same name");
		}

		public static void ReplaceTexture(UnityParser parser, ImportedTexture texture)
		{
			Texture2D tex = parser.GetTexture(texture.Name);
			if (tex == null)
			{
				parser.AddTexture(texture);
				return;
			}
			ReplaceTexture(tex, texture);
		}

		public static void ReplaceMaterial(Material mat, ImportedMaterial material)
		{
			if (mat == null)
			{
				throw new Exception("Replacing a material currently requires an existing material with the same name");
			}

			for (int i = 0; i < mat.m_SavedProperties.m_Colors.Count; i++)
			{
				var col = mat.m_SavedProperties.m_Colors[i];
				Color4 att;
				switch (col.Key.name)
				{
				case "_Color":
					att = material.Diffuse;
					break;
				case "_SColor":
					att = material.Ambient;
					break;
				case "_ReflectColor":
					att = material.Emissive;
					break;
				case "_SpecColor":
					att = material.Specular;
					break;
				case "_RimColor":
				case "_OutlineColor":
				case "_ShadowColor":
				default:
					continue;
				}
				mat.m_SavedProperties.m_Colors.RemoveAt(i);
				col = new KeyValuePair<FastPropertyName, Color4>(col.Key, att);
				mat.m_SavedProperties.m_Colors.Insert(i, col);
			}

			for (int i = 0; i < mat.m_SavedProperties.m_Floats.Count; i++)
			{
				var flt = mat.m_SavedProperties.m_Floats[i];
				float att;
				switch (flt.Key.name)
				{
				case "_Shininess":
					att = material.Power;
					break;
				case "_RimPower":
				case "_Outline":
				default:
					continue;
				}
				mat.m_SavedProperties.m_Floats.RemoveAt(i);
				flt = new KeyValuePair<FastPropertyName, float>(flt.Key, att);
				mat.m_SavedProperties.m_Floats.Insert(i, flt);
			}

			for (int i = 0; i < material.Textures.Length && i < mat.m_SavedProperties.m_TexEnvs.Count; i++)
			{
				try
				{
					Texture2D tex = null;
					if (material.Textures[i] != string.Empty)
					{
						tex = mat.file.Parser.GetTexture(material.Textures[i]);
					}
					if (mat.m_SavedProperties.m_TexEnvs[i].Value.m_Texture.asset != tex)
					{
						mat.m_SavedProperties.m_TexEnvs[i].Value.m_Texture = new PPtr<Texture2D>(tex);
					}
				}
				catch (Exception e)
				{
					Report.ReportLog(e.ToString());
				}
			}
		}

		public static void ReplaceTexture(Texture2D tex, ImportedTexture texture)
		{
			if (tex == null)
			{
				throw new Exception("This type of replacing a texture requires an existing texture with the same name");
			}

			Texture2D t2d = new Texture2D(null, tex.pathID, tex.classID1, tex.classID2);
			t2d.LoadFrom(texture);
			tex.image_data = t2d.image_data;
		}
	}
}
