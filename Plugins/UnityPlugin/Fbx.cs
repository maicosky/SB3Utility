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
		public static void ExportFbx([DefaultVar]Animator animator, object[] meshNames, object[] animationParsers, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, double filterPrecision, string path, string exportFormat, bool allFrames, bool allBones, bool skins, bool compatibility)
		{
			List<string> meshNamesList = new List<string>(Utility.Convert<string>(meshNames));
			List<SkinnedMeshRenderer> sMeshes = meshNames != null ? Operations.FindMeshes(animator.RootTransform, new List<string>(Utility.Convert<string>(meshNames))) : null;

			UnityConverter imp = new UnityConverter(animator, sMeshes, skins);

			FbxUtility.Export(path, imp, startKeyframe, endKeyframe, linear, EulerFilter, (float)filterPrecision, exportFormat, allFrames, allBones, skins, compatibility);
		}

		[Plugin]
		public static void ExportFbx([DefaultVar]UnityParser parser, object[] skinnedMeshRendererIDs, object[] animationParsers, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, double filterPrecision, string path, string exportFormat, bool allFrames, bool allBones, bool skins, bool compatibility)
		{
			List<double> sMeshIDList = new List<double>(Utility.Convert<double>(skinnedMeshRendererIDs));
			List<SkinnedMeshRenderer> sMeshes = new List<SkinnedMeshRenderer>(sMeshIDList.Count);
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
							Console.WriteLine("SkinnedMeshRenderer " + sMeshID + " not found");
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
						Console.WriteLine("SkinnedMeshRenderer " + sMeshID + " not found");
						continue;
					}
					sMeshes.Add(sMesh);
				}
			}

			UnityConverter imp = new UnityConverter(parser, sMeshes, skins);

			FbxUtility.Export(path, imp, startKeyframe, endKeyframe, linear, EulerFilter, (float)filterPrecision, exportFormat, allFrames, allBones, skins, compatibility);
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

			public UnityConverter(UnityParser parser, List<SkinnedMeshRenderer> sMeshes, bool skins)
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

				ConvertSkinnedMeshRenderers(sMeshes, skins);
				AnimationList = new List<ImportedAnimation>();
			}

			public UnityConverter(Animator animator, List<SkinnedMeshRenderer> sMeshes, bool skins)
			{
				ConvertFrames(animator.RootTransform, null);

				if (skins)
				{
					avatar = animator.m_Avatar.instance;
				}

				ConvertSkinnedMeshRenderers(sMeshes, skins);
				AnimationList = new List<ImportedAnimation>();
			}

			private void ConvertFrames(Transform trans, ImportedFrame parent)
			{
				ImportedFrame frame = new ImportedFrame();
				frame.Name = trans.m_GameObject.instance.m_Name;
				frame.InitChildren(trans./*m_Children.*/Count);
				frame.Matrix = Matrix.Scaling(trans.m_LocalScale) * Matrix.RotationQuaternion(trans.m_LocalRotation) * Matrix.Translation(trans.m_LocalPosition);
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

			private Matrix WorldTransform(ImportedFrame frame)
			{
				Matrix world = frame.Matrix;
				while (frame != FrameList[0])
				{
					frame = frame.Parent;
					world = world * frame.Matrix;
				}
				return world;
			}

			private void ConvertSkinnedMeshRenderers(List<SkinnedMeshRenderer> sMeshes, bool skins)
			{
				MeshList = new List<ImportedMesh>(sMeshes.Count);
				MaterialList = new List<ImportedMaterial>(sMeshes.Count);
				TextureList = new List<ImportedTexture>(sMeshes.Count);
				foreach (SkinnedMeshRenderer sMesh in sMeshes)
				{
					Mesh mesh = sMesh.m_Mesh.instance;
					if (mesh == null)
					{
						Report.ReportLog("skipping " + sMesh.m_GameObject.instance.m_Name + " - no mesh");
						continue;
					}

					ImportedMesh iMesh = new ImportedMesh();
					iMesh.Name = mesh.m_Name;
					iMesh.SubmeshList = new List<ImportedSubmesh>(mesh.m_SubMeshes.Count);
					for (int i = 0; i < mesh.m_SubMeshes.Count; i++)
					{
						SubMesh submesh = mesh.m_SubMeshes[i];
						ImportedSubmesh iSubmesh = new ImportedSubmesh();
						iSubmesh.Index = i;
						iSubmesh.Visible = true;

						Material mat = sMesh.m_Materials[i].instance;
						ConvertMaterial(mat);
						iSubmesh.Material = mat.m_Name;

						iSubmesh.VertexList = new List<ImportedVertex>((int)submesh.vertexCount);
						using (MemoryStream memStream = new MemoryStream(mesh.m_VertexData.m_DataSize, false))
						{
							BinaryReader reader = new BinaryReader(memStream);
							for (int str = 0; str < mesh.m_VertexData.m_Streams.Count; str++)
							{
								StreamInfo sInfo = mesh.m_VertexData.m_Streams[str];
								if (sInfo.channelMask == 0)
								{
									continue;
								}

								for (int j = 0; j < mesh.m_VertexData.m_VertexCount; j++)
								{
									ImportedVertex iVertex;
									if (iSubmesh.VertexList.Count < mesh.m_VertexData.m_VertexCount)
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

										memStream.Position = sInfo.offset + j * sInfo.stride + cInfo.offset;
										switch (chn)
										{
										case 0:
											iVertex.Position = new SlimDX.Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
											break;
										case 1:
											iVertex.Normal = new SlimDX.Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
											break;
										case 3:
											iVertex.UV = new float[2] { reader.ReadSingle(), reader.ReadSingle() };
											break;
										case 5:
											iVertex.Tangent = new SlimDX.Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
											break;
										}
									}

									if (skins && iVertex.BoneIndices == null && mesh.m_Skin.Count > 0)
									{
										iVertex.BoneIndices = new byte[mesh.m_Skin[j].boneIndex.Length];
										for (int k = 0; k < iVertex.BoneIndices.Length; k++)
										{
											iVertex.BoneIndices[k] = mesh.m_Skin[j].boneIndex[k] != 0 || mesh.m_Skin[j].weight[k] != 0 ? (byte)mesh.m_Skin[j].boneIndex[k] : (byte)0xFF;
										}
										iVertex.Weights = mesh.m_Skin[j].weight;
									}
								}
							}
						}

						int numFaces = (int)(submesh.indexCount / 3);
						iSubmesh.FaceList = new List<ImportedFace>(numFaces);
						using (BinaryReader reader = new BinaryReader(new MemoryStream(mesh.m_IndexBuffer)))
						{
							for (int j = 0; j < numFaces; j++)
							{
								ImportedFace face = new ImportedFace();
								face.VertexIndices = new int[3] { reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16() };
								iSubmesh.FaceList.Add(face);
							}
						}

						iMesh.SubmeshList.Add(iSubmesh);
					}

					if (skins)
					{
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
							bone.Matrix = Matrix.Transpose(mesh.m_BindPose[i]);
							/*Transform boneMatrix = parser.Cabinet.FindAsset(sMesh.m_Bones[i].m_PathID);
							bone.Matrix = Matrix.Invert(WorldTransform(boneMatrix, parser));*/
							iMesh.BoneList.Add(bone);
						}
					}

					MeshList.Add(iMesh);
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
					iMat.Textures[i] = tex2D.m_Name + "-" + tex.Key.name + "-" + "offset(X" + tex.Value.m_Offset.X.ToFloatString() + "Y" + tex.Value.m_Offset.Y.ToFloatString() + ")-scale(X" + tex.Value.m_Scale.X.ToFloatString() + "Y" + tex.Value.m_Scale.Y.ToFloatString() + ")" + (tex2D.m_TextureFormat == TextureFormat.DXT1 || tex2D.m_TextureFormat == TextureFormat.DXT5 ? ".dds" : ".tga");
					ConvertTexture2D(tex2D, iMat.Textures[i]);
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
