using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public static partial class Plugins
	{
		[Plugin]
		public static void ExportFbx([DefaultVar]Animator animator, object[] meshes, object[] animationParsers, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, double filterPrecision, string path, string exportFormat, bool allFrames, bool allBones, bool skins, bool compatibility)
		{
			MeshRenderer[] meshArray = Utility.Convert<MeshRenderer>(meshes);
			List<MeshRenderer> meshList = new List<MeshRenderer>(meshArray);

			UnityConverter imp = new UnityConverter(animator, meshList, skins, false);

			FbxUtility.Export(path, imp, startKeyframe, endKeyframe, linear, EulerFilter, (float)filterPrecision, exportFormat, allFrames, allBones, skins, compatibility);
		}

		[Plugin]
		public static void ExportFbx([DefaultVar]UnityParser parser, object[] skinnedMeshRendererIDs, object[] animationParsers, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, double filterPrecision, string path, string exportFormat, bool allFrames, bool allBones, bool skins, bool compatibility)
		{
			List<double> sMeshIDList = new List<double>(Utility.Convert<double>(skinnedMeshRendererIDs));
			List<MeshRenderer> sMeshes = new List<MeshRenderer>(sMeshIDList.Count);
			for (int i = 0; i < sMeshIDList.Count; i++)
			{
				int sMeshID = (int)sMeshIDList[i];
				if (i > 0 && sMeshID < 0)
				{
					for (sMeshID = (int)sMeshIDList[i - 1] + 1; sMeshID < -(int)sMeshIDList[i]; sMeshID++)
					{
						SkinnedMeshRenderer sMesh = parser.Cabinet.LoadComponent(sMeshID);
						if (sMesh == null)
						{
							continue;
						}
						sMeshes.Add(sMesh);
					}
				}
				else
				{
					SkinnedMeshRenderer sMesh = parser.Cabinet.LoadComponent(sMeshID);
					if (sMesh == null)
					{
						continue;
					}
					sMeshes.Add(sMesh);
				}
			}

			UnityConverter imp = new UnityConverter(parser, sMeshes, skins);

			FbxUtility.Export(path, imp, startKeyframe, endKeyframe, linear, EulerFilter, (float)filterPrecision, exportFormat, allFrames, allBones, skins, compatibility);
		}

		[Plugin]
		public static void ExportMorphFbx([DefaultVar]Animator animator, object[] meshes, string path, string exportFormat, bool oneBlendShape, bool compatibility)
		{
			MeshRenderer[] meshArray = Utility.Convert<MeshRenderer>(meshes);
			List<MeshRenderer> meshList = new List<MeshRenderer>(meshArray);
			UnityConverter imp = new UnityConverter(animator, meshList, false, true);

			FbxUtility.ExportMorph(path, imp, exportFormat, oneBlendShape, compatibility);
		}

		public class UnityConverter : IImported
		{
			public List<ImportedFrame> FrameList { get; protected set; }
			public List<ImportedMesh> MeshList { get; protected set; }
			public List<ImportedMaterial> MaterialList { get; protected set; }
			public List<ImportedTexture> TextureList { get; protected set; }
			public List<ImportedAnimation> AnimationList { get; protected set; }
			public List<ImportedMorph> MorphList { get; protected set; }

			Avatar avatar = null;

			public UnityConverter(UnityParser parser, List<MeshRenderer> sMeshes, bool skins)
			{
				foreach (SkinnedMeshRenderer sMesh in sMeshes)
				{
					Transform rootBone = sMesh.m_RootBone.instance;
					if (rootBone != null)
					{
						while (rootBone.Parent != null)
						{
							rootBone = rootBone.Parent;
						}
						if (FrameList == null)
						{
							ConvertFrames(rootBone, null);
						}
						else if (FrameList[0].Name != rootBone.m_GameObject.instance.m_Name)
						{
							FrameList[0].Name = "Joined_Root";
						}
					}
				}

				if (skins)
				{
					foreach (Component asset in parser.Cabinet.Components)
					{
						if (asset.classID1 == UnityClassID.Avatar)
						{
							avatar = parser.Cabinet.LoadComponent(asset.pathID);
							break;
						}
					}
				}

				ConvertMeshRenderers(sMeshes, skins, false);
				AnimationList = new List<ImportedAnimation>();
			}

			public UnityConverter(Animator animator, List<MeshRenderer> meshList, bool skins, bool morphs)
			{
				ConvertFrames(animator.RootTransform, null);

				if (skins)
				{
					avatar = animator.m_Avatar.instance;
				}

				ConvertMeshRenderers(meshList, skins, morphs);

				AnimationList = new List<ImportedAnimation>();
			}

			private void ConvertFrames(Transform trans, ImportedFrame parent)
			{
				ImportedFrame frame = new ImportedFrame();
				frame.Name = trans.m_GameObject.instance.m_Name;
				frame.InitChildren(trans.Count);
				Vector3 euler = FbxUtility.QuaternionToEuler(trans.m_LocalRotation);
				euler.Y *= -1;
				euler.Z *= -1;
				Quaternion mirroredRotation = FbxUtility.EulerToQuaternion(euler);
				frame.Matrix = Matrix.Scaling(trans.m_LocalScale) * Matrix.RotationQuaternion(mirroredRotation) * Matrix.Translation(-trans.m_LocalPosition.X, trans.m_LocalPosition.Y, trans.m_LocalPosition.Z);
				if (parent == null)
				{
					FrameList = new List<ImportedFrame>();
					FrameList.Add(frame);
				}
				else
				{
					parent.AddChild(frame);
				}

				foreach (Transform child in trans)
				{
					ConvertFrames(child, frame);
				}
			}

			private void ConvertMeshRenderers(List<MeshRenderer> meshList, bool skins, bool morphs)
			{
				MeshList = new List<ImportedMesh>(meshList.Count);
				MaterialList = new List<ImportedMaterial>(meshList.Count);
				TextureList = new List<ImportedTexture>(meshList.Count);
				MorphList = new List<ImportedMorph>(meshList.Count);
				foreach (MeshRenderer meshR in meshList)
				{
					Mesh mesh = Operations.GetMesh(meshR);
					if (mesh == null)
					{
						Report.ReportLog("skipping " + meshR.m_GameObject.instance.m_Name + " - no mesh");
						continue;
					}

					ImportedMesh iMesh = new ImportedMesh();
					Transform meshTransform = meshR.m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
					iMesh.Name = meshTransform.GetTransformPath();
					iMesh.SubmeshList = new List<ImportedSubmesh>(mesh.m_SubMeshes.Count);
					using (BinaryReader vertReader = new BinaryReader(new MemoryStream(mesh.m_VertexData.m_DataSize)),
						indexReader = new BinaryReader(new MemoryStream(mesh.m_IndexBuffer)))
					{
						for (int i = 0; i < mesh.m_SubMeshes.Count; i++)
						{
							SubMesh submesh = mesh.m_SubMeshes[i];
							ImportedSubmesh iSubmesh = new ImportedSubmesh();
							iSubmesh.Index = i;
							iSubmesh.Visible = true;

							Material mat = meshR.m_Materials[i].instance;
							ConvertMaterial(mat);
							iSubmesh.Material = mat.m_Name;

							iSubmesh.VertexList = new List<ImportedVertex>((int)submesh.vertexCount);
							for (int str = 0; str < mesh.m_VertexData.m_Streams.Count; str++)
							{
								StreamInfo sInfo = mesh.m_VertexData.m_Streams[str];
								if (sInfo.channelMask == 0)
								{
									continue;
								}

								for (int j = 0; j < mesh.m_SubMeshes[i].vertexCount; j++)
								{
									ImportedVertex iVertex;
									if (iSubmesh.VertexList.Count < mesh.m_SubMeshes[i].vertexCount)
									{
										iVertex = new ImportedVertex();
										iSubmesh.VertexList.Add(iVertex);
									}
									else
									{
										iVertex = iSubmesh.VertexList[j];
									}

									for (int chn = 0; chn < mesh.m_VertexData.m_Channels.Count; chn++)
									{
										ChannelInfo cInfo = mesh.m_VertexData.m_Channels[chn];
										if ((sInfo.channelMask & (1 << chn)) == 0)
										{
											continue;
										}

										vertReader.BaseStream.Position = sInfo.offset + (submesh.firstVertex + j) * sInfo.stride + cInfo.offset;
										switch (chn)
										{
										case 0:
											iVertex.Position = new SlimDX.Vector3(-vertReader.ReadSingle(), vertReader.ReadSingle(), vertReader.ReadSingle());
											break;
										case 1:
											iVertex.Normal = new SlimDX.Vector3(-vertReader.ReadSingle(), vertReader.ReadSingle(), vertReader.ReadSingle());
											break;
										case 3:
											iVertex.UV = new float[2] { vertReader.ReadSingle(), vertReader.ReadSingle() };
											break;
										case 5:
											iVertex.Tangent = new SlimDX.Vector4(-vertReader.ReadSingle(), vertReader.ReadSingle(), vertReader.ReadSingle(), -vertReader.ReadSingle());
											break;
										}
									}

									if (skins && iVertex.BoneIndices == null && mesh.m_Skin.Count > 0)
									{
										BoneInfluence inf = mesh.m_Skin[(int)submesh.firstVertex + j];
										iVertex.BoneIndices = new byte[inf.boneIndex.Length];
										for (int k = 0; k < iVertex.BoneIndices.Length; k++)
										{
											iVertex.BoneIndices[k] = (byte)inf.boneIndex[k];
										}
										iVertex.Weights = (float[])inf.weight.Clone();
									}
								}
							}

							int numFaces = (int)(submesh.indexCount / 3);
							iSubmesh.FaceList = new List<ImportedFace>(numFaces);
							indexReader.BaseStream.Position = submesh.firstByte;
							for (int j = 0; j < numFaces; j++)
							{
								ImportedFace face = new ImportedFace();
								face.VertexIndices = new int[3];
								face.VertexIndices[0] = indexReader.ReadUInt16() - (int)submesh.firstVertex;
								face.VertexIndices[2] = indexReader.ReadUInt16() - (int)submesh.firstVertex;
								face.VertexIndices[1] = indexReader.ReadUInt16() - (int)submesh.firstVertex;
								iSubmesh.FaceList.Add(face);
							}

							iMesh.SubmeshList.Add(iSubmesh);
						}
					}

					if (skins && meshR is SkinnedMeshRenderer)
					{
						SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshR;
						if (sMesh.m_Bones.Count >= 256)
						{
							throw new Exception("Too many bones (" + mesh.m_BindPose.Count + ")");
						}
						if (sMesh.m_Bones.Count != mesh.m_BindPose.Count || sMesh.m_Bones.Count != mesh.m_BoneNameHashes.Count)
						{
							throw new Exception("Mismatching number of bones bind pose=" + mesh.m_BindPose.Count + " hashes=" + mesh.m_BoneNameHashes.Count + " numBones=" + sMesh.m_Bones.Count);
						}
						iMesh.BoneList = new List<ImportedBone>(sMesh.m_Bones.Count);
						for (int i = 0; i < sMesh.m_Bones.Count; i++)
						{
							ImportedBone bone = new ImportedBone();
							uint boneHash = mesh.m_BoneNameHashes[i];
							bone.Name = avatar.FindBoneName(boneHash);

							Matrix m = Matrix.Transpose(mesh.m_BindPose[i]);
							Vector3 s, t;
							Quaternion q;
							m.Decompose(out s, out q, out t);
							t.X *= -1;
							Vector3 euler = FbxUtility.QuaternionToEuler(q);
							euler.Y *= -1;
							euler.Z *= -1;
							q = FbxUtility.EulerToQuaternion(euler);
							bone.Matrix = Matrix.Scaling(s) * Matrix.RotationQuaternion(q) * Matrix.Translation(t);

							iMesh.BoneList.Add(bone);
						}
					}

					if (morphs && mesh.m_Shapes.shapes.Count > 0)
					{
						ImportedMorph morph = new ImportedMorph();
						morph.Name = iMesh.Name;
						morph.ClipName = Operations.BlendShapeName(mesh);
						morph.KeyframeList = new List<ImportedMorphKeyframe>(mesh.m_Shapes.shapes.Count);
						for (int i = 0; i < mesh.m_Shapes.shapes.Count; i++)
						{
							ImportedMorphKeyframe keyframe = new ImportedMorphKeyframe();
							keyframe.Name = Operations.BlendShapeKeyframeName(mesh, i);
							keyframe.VertexList = new List<ImportedVertex>((int)mesh.m_Shapes.shapes[i].vertexCount);
							keyframe.MorphedVertexIndices = new List<ushort>((int)mesh.m_Shapes.shapes[i].vertexCount);
							int lastVertIndex = (int)(mesh.m_Shapes.shapes[i].firstVertex + mesh.m_Shapes.shapes[i].vertexCount);
							for (int j = (int)mesh.m_Shapes.shapes[i].firstVertex; j < lastVertIndex; j++)
							{
								BlendShapeVertex morphVert = mesh.m_Shapes.vertices[j];
								ImportedVertex vert = GetSourceVertex(iMesh.SubmeshList, (int)morphVert.index);
								ImportedVertex destVert = new ImportedVertex();
								Vector3 morphPos = morphVert.vertex;
								morphPos.X *= -1;
								destVert.Position = vert.Position + morphPos;
								Vector3 morphNormal = morphVert.normal;
								morphNormal.X *= -1;
								destVert.Normal = morphNormal;
								Vector4 morphTangent = new Vector4(morphVert.tangent, 0);
								morphTangent.X *= -1;
								destVert.Tangent = morphTangent;
								keyframe.VertexList.Add(destVert);
								keyframe.MorphedVertexIndices.Add((ushort)morphVert.index);
							}
							morph.KeyframeList.Add(keyframe);
						}
						MorphList.Add(morph);
					}

					MeshList.Add(iMesh);
				}
			}

			private static ImportedVertex GetSourceVertex(List<ImportedSubmesh> submeshList, int morphVertIndex)
			{
				for (int i = 0; i < submeshList.Count; i++)
				{
					List<ImportedVertex> vertList = submeshList[i].VertexList;
					if (morphVertIndex < vertList.Count)
					{
						return vertList[morphVertIndex];
					}
					morphVertIndex -= vertList.Count;
				}
				return null;
			}

			public void WorldCoordinates(int meshIdx, Matrix worldTransform)
			{
				ImportedMesh mesh = MeshList[meshIdx];
				foreach (ImportedSubmesh submesh in mesh.SubmeshList)
				{
					List<ImportedVertex> vertList = submesh.VertexList;
					for (int i = 0; i < vertList.Count; i++)
					{
						vertList[i].Position = Vector3.TransformCoordinate(vertList[i].Position, worldTransform);
					}
				}
			}

			private void ConvertMaterial(Material mat)
			{
				ImportedMaterial iMat = ImportedHelpers.FindMaterial(mat.m_Name, MaterialList);
				if (iMat != null)
				{
					return;
				}

				iMat = new ImportedMaterial();
				iMat.Name = mat.m_Name;

				foreach (var col in mat.m_SavedProperties.m_Colors)
				{
					switch (col.Key.name)
					{
					case "_Color":
						iMat.Diffuse = col.Value;
						break;
					case "_SColor":
						iMat.Ambient = col.Value;
						break;
					case "_ReflectColor":
						iMat.Emissive = col.Value;
						break;
					case "_SpecColor":
						iMat.Specular = col.Value;
						break;
					case "_RimColor":
					case "_OutlineColor":
					case "_ShadowColor":
						break;
					}
				}

				foreach (var flt in mat.m_SavedProperties.m_Floats)
				{
					switch (flt.Key.name)
					{
					case "_Shininess":
						iMat.Power = flt.Value;
						break;
					case "_RimPower":
					case "_Outline":
						break;
					}
				}

				iMat.Textures = new string[4];
				int numTex = mat.m_SavedProperties.m_TexEnvs.Count > 4 ? 4 : mat.m_SavedProperties.m_TexEnvs.Count;
				for (int i = 0; i < numTex; i++)
				{
					var tex = mat.m_SavedProperties.m_TexEnvs[i];
					Texture2D tex2D = tex.Value.m_Texture.instance;
					if (tex2D != null)
					{
						iMat.Textures[i] = tex2D.m_Name + "-" + tex2D.m_TextureFormat + (tex2D.m_TextureFormat == TextureFormat.DXT1 || tex2D.m_TextureFormat == TextureFormat.DXT5 ? ".dds" : ".tga");
						ConvertTexture2D(tex2D, iMat.Textures[i]);
					}
				}

				MaterialList.Add(iMat);
			}

			private void ConvertTexture2D(Texture2D tex2D, string name)
			{
				ImportedTexture iTex = ImportedHelpers.FindTexture(name, TextureList);
				if (iTex != null)
				{
					return;
				}

				using (MemoryStream memStream = new MemoryStream())
				{
					tex2D.Export(memStream);

					memStream.Position = 0;
					iTex = new ImportedTexture(memStream, name);
				}
				TextureList.Add(iTex);
			}
		}
	}
}
