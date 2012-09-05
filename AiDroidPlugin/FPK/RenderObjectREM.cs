//#define DONT_MIRROR
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using SlimDX;
using SlimDX.Direct3D9;
using System.Runtime.InteropServices;

using SB3Utility;

namespace AiDroidPlugin
{
	class RenderObjectREM : IDisposable, IRenderObject
	{
		private AnimationFrame rootFrame;
		private Device device;
		private List<AnimationFrame> meshFrames;
		private Material highlightMaterial;
		private Material nullMaterial = new Material();
		private int submeshNum = 0;
		private int numFrames = 0;

		public BoundingBox Bounds { get; protected set; }
		public AnimationController AnimationController { get; protected set; }
		public bool IsDisposed { get; protected set; }
		public HashSet<int> HighlightSubmesh { get; protected set; }

		private Texture[] Textures;
		private Dictionary<string, int> TextureDic;
		private Material[] Materials;
		private Dictionary<string, Matrix> BoneMatrixDic;

		private static Dictionary<string, ImportedTexture> ImportedTextures = new Dictionary<string, ImportedTexture>();

		public RenderObjectREM(remParser parser, remMesh mesh)
		{
			HighlightSubmesh = new HashSet<int>();
			highlightMaterial = new Material();
			highlightMaterial.Ambient = new Color4(1, 1, 1, 1);
			highlightMaterial.Diffuse = new Color4(1, 0, 1, 0);

			this.device = Gui.Renderer.Device;

			Textures = new Texture[parser.RemFile.MATC.numMats];
			TextureDic = new Dictionary<string, int>(parser.RemFile.MATC.numMats);
			Materials = new Material[parser.RemFile.MATC.numMats];
			BoneMatrixDic = new Dictionary<string, Matrix>();

			rootFrame = CreateHierarchy(parser, mesh, device, out meshFrames);

			AnimationController = new AnimationController(numFrames, 30, 30, 1);
			Frame.RegisterNamedMatrices(rootFrame, AnimationController);

			for (int i = 0; i < meshFrames.Count; i++)
			{
				Bounds = BoundingBox.Merge(Bounds, meshFrames[i].Bounds);
			}
		}

		~RenderObjectREM()
		{
			Dispose();
		}

		public void Dispose()
		{
			for (int i = 0; i < meshFrames.Count; i++)
			{
				AnimationMeshContainer mesh = (AnimationMeshContainer)meshFrames[i].MeshContainer;
				while (mesh != null)
				{
					if ((mesh.MeshData != null) && (mesh.MeshData.Mesh != null))
					{
						mesh.MeshData.Mesh.Dispose();
					}

					for (int j = 0; j < Textures.Length; j++)
					{
						Texture tex = Textures[j];
						if ((tex != null) && !tex.Disposed)
						{
							tex.Dispose();
						}
					}

					mesh = (AnimationMeshContainer)mesh.NextMeshContainer;
				}
			}

			rootFrame.Dispose();
			AnimationController.Dispose();

			IsDisposed = true;
		}

		public void Render()
		{
//			UpdateFrameMatrices(rootFrame, Matrix.Identity);

			for (int i = 0; i < meshFrames.Count; i++)
			{
				DrawMeshFrame(meshFrames[i]);
			}
		}

		public void ResetPose()
		{
			ResetPose(rootFrame);
		}

		private void DrawMeshFrame(AnimationFrame frame)
		{
			submeshNum = 0;
			for (AnimationMeshContainer meshContainer = (AnimationMeshContainer)frame.MeshContainer;
				 meshContainer != null;
				 meshContainer = (AnimationMeshContainer)meshContainer.NextMeshContainer, submeshNum++)
			{
				DrawMeshContainer(meshContainer, frame);
			}
		}

		private void DrawMeshContainer(AnimationMeshContainer meshContainer, AnimationFrame frame)
		{
			device.SetRenderState(RenderState.ZEnable, ZBufferType.UseZBuffer);
			device.SetRenderState(RenderState.Lighting, true);
//			device.SetRenderState(RenderState.Lighting, false);

#if !DONT_MIRROR
			Cull culling = (Gui.Renderer.Culling) ? Cull.Clockwise : Cull.None;
#else
			Cull culling = (Gui.Renderer.Culling) ? Cull.Counterclockwise : Cull.None;
#endif
			device.SetRenderState(RenderState.CullMode, culling);

			FillMode fill = (Gui.Renderer.Wireframe) ? FillMode.Wireframe : FillMode.Solid;
			device.SetRenderState(RenderState.FillMode, fill);

//			device.SetRenderState(RenderState.NormalizeNormals, true);

			if (meshContainer.BoneNames.Length > 0)
			{
				device.SetRenderState(RenderState.VertexBlend, VertexBlend.Weights3);
				device.SetRenderState(RenderState.IndexedVertexBlendEnable, true);
//				device.SetRenderState(RenderState.ColorVertex, true);
//				device.SetRenderState(RenderState.DiffuseMaterialSource, ColorSource.Color1);
				device.SetRenderState(RenderState.AmbientMaterialSource, ColorSource.Color1);

//				Matrix[] bitMatrices = new Matrix[meshContainer.BoneNames.Length];
				for (int i = 0; i < meshContainer.BoneNames.Length; i++)
				{
					if (meshContainer.BoneFrames[i] != null)
					{
						device.SetTransform(i, meshContainer.BoneOffsets[i] * meshContainer.BoneFrames[i].CombinedTransform);
/*						bitMatrices[i] = Matrix.Invert(meshContainer.BoneOffsets[i]);
						bitMatrices[i] = Matrix.Transpose(bitMatrices[i]);*/
					}
				}
/*				Mesh mesh = meshContainer.MeshData.Mesh;
				DataStream src = mesh.LockVertexBuffer(LockFlags.ReadOnly);
				DataStream dst = meshContainer.SkinInfo.LockVertexBuffer(LockFlags.None);
				Report.ReportLog("locked " + (dst == null ? "no dst" : "dst not null"));
				mesh.SkinInfo.UpdateSkinnedMesh(meshContainer.BoneOffsets, bitMatrices, src, dst);
				Report.ReportLog("update");
				mesh.UnlockVertexBuffer();
				Report.ReportLog("unlocked");*/
			}
			else
			{
				device.SetRenderState(RenderState.VertexBlend, VertexBlend.Disable);
#if !DONT_MIRROR
				device.SetTransform(TransformState.World, Matrix.Scaling(1.0f, 1.0f, -1.0f) * frame.CombinedTransform);
#else
				device.SetTransform(TransformState.World, frame.CombinedTransform);
#endif
			}

			int matIdx = meshContainer.MaterialIndex;
			device.Material = ((matIdx >= 0) && (matIdx < Materials.Length)) ? Materials[matIdx] : nullMaterial;

			int texIdx = meshContainer.TextureIndex;
			Texture tex = ((texIdx >= 0) && (texIdx < Textures.Length)) ? Textures[texIdx] : null;
			device.SetTexture(0, tex);

//			meshContainer.MeshData.Mesh.ComputeNormals();
			meshContainer.MeshData.Mesh.DrawSubset(0);

			if (HighlightSubmesh.Contains(submeshNum))
			{
				device.SetRenderState(RenderState.ZEnable, ZBufferType.DontUseZBuffer);
				device.SetRenderState(RenderState.FillMode, FillMode.Wireframe);
				device.Material = highlightMaterial;
				device.SetTexture(0, null);
				meshContainer.MeshData.Mesh.DrawSubset(0);
			}

			if (Gui.Renderer.ShowNormals)
			{
				device.SetRenderState(RenderState.ZEnable, ZBufferType.UseZBuffer);
				device.SetRenderState(RenderState.Lighting, false);
				device.Material = nullMaterial;
				device.SetTexture(0, null);
				device.VertexFormat = PositionBlendWeightsIndexedColored.Format;
				device.DrawUserPrimitives(PrimitiveType.LineList, meshContainer.NormalLines.Length / 2, meshContainer.NormalLines);
			}

			if (Gui.Renderer.ShowBones && (meshContainer.BoneLines != null))
			{
				device.SetRenderState(RenderState.ZEnable, ZBufferType.DontUseZBuffer);
				device.SetRenderState(RenderState.VertexBlend, VertexBlend.Weights1);
				device.SetRenderState(RenderState.IndexedVertexBlendEnable, true);
				device.SetRenderState(RenderState.Lighting, false);
				device.Material = nullMaterial;
				device.SetTexture(0, null);
				device.VertexFormat = PositionBlendWeightIndexedColored.Format;
				device.DrawUserPrimitives(PrimitiveType.LineList, meshContainer.BoneLines.Length / 2, meshContainer.BoneLines);
			}
		}

		private void ResetPose(AnimationFrame frame)
		{
			frame.TransformationMatrix = frame.OriginalTransform;

			if (frame.Sibling != null)
			{
				ResetPose((AnimationFrame)frame.Sibling);
			}

			if (frame.FirstChild != null)
			{
				ResetPose((AnimationFrame)frame.FirstChild);
			}
		}

		private void UpdateFrameMatrices(AnimationFrame frame, Matrix parentMatrix)
		{
			frame.CombinedTransform = frame.TransformationMatrix * parentMatrix;

			if (frame.Sibling != null)
			{
				UpdateFrameMatrices((AnimationFrame)frame.Sibling, parentMatrix);
			}

			if (frame.FirstChild != null)
			{
				UpdateFrameMatrices((AnimationFrame)frame.FirstChild, frame.CombinedTransform);
			}
		}

		private AnimationFrame CreateHierarchy(remParser parser, remMesh mesh, Device device, out List<AnimationFrame> meshFrames)
		{
			meshFrames = new List<AnimationFrame>(1);
			HashSet<string> extractFrames = rem.SearchHierarchy(parser, mesh);
#if !DONT_MIRROR
			Vector3 translate, scale;
			Quaternion rotate;
			parser.RemFile.BONC.rootFrame.trans.Decompose(out scale, out rotate, out translate);
			parser.RemFile.BONC.rootFrame.trans = Matrix.Scaling(scale.X, scale.Y, -scale.Z) * Matrix.RotationQuaternion(rotate) * Matrix.Translation(translate);
#endif
			AnimationFrame rootFrame = CreateFrame(parser.RemFile.BONC.rootFrame, parser, extractFrames, mesh, device, Matrix.Identity, meshFrames);
			SetupBoneMatrices(rootFrame, rootFrame);
#if !DONT_MIRROR
			parser.RemFile.BONC.rootFrame.trans = Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotate) * Matrix.Translation(translate);
#endif
UpdateFrameMatrices(rootFrame, Matrix.Identity);
			return rootFrame;
		}

		private AnimationFrame CreateFrame(remBone frame, remParser parser, HashSet<string> extractFrames, remMesh mesh, Device device, Matrix combinedParent, List<AnimationFrame> meshFrames)
		{
			AnimationFrame animationFrame = new AnimationFrame();
			animationFrame.Name = frame.name.ToString();
			animationFrame.TransformationMatrix = frame.trans;
			animationFrame.OriginalTransform = animationFrame.TransformationMatrix;
			animationFrame.CombinedTransform = combinedParent * animationFrame.TransformationMatrix;

			if (frame.name == mesh.frame)
			{
				ExtendedMaterial[] materials = new ExtendedMaterial[mesh.numMats];

				List<List<remVertex>> submeshVertLists = new List<List<remVertex>>(mesh.numMats);
				List<List<ushort>> submeshFaceLists = new List<List<ushort>>(mesh.numMats);
				List<int[]> submeshVertIndices = new List<int[]>(mesh.numMats);
				for (int i = 0; i < mesh.numFaces; i++)
				{
					while (mesh.faceMarks[i] >= submeshFaceLists.Count)
					{
						List<ushort> newFaceList = new List<ushort>(mesh.numFaces);
						submeshFaceLists.Add(newFaceList);
						List<remVertex> newVertList = new List<remVertex>(mesh.numVertices);
						submeshVertLists.Add(newVertList);
						int[] newVertIndices = new int[mesh.numVertices];
						for (int j = 0; j < mesh.numVertices; j++)
							newVertIndices[j] = -1;
						submeshVertIndices.Add(newVertIndices);
					}

					int submesh = mesh.faceMarks[i];
					for (int j = 0; j < 3; j++)
					{
						int vertIdx = mesh.faces[i * 3 + j];
						if (submeshVertIndices[submesh][vertIdx] < 0)
						{
							submeshVertIndices[submesh][vertIdx] = submeshVertLists[submesh].Count;
							submeshVertLists[submesh].Add(mesh.vertices[vertIdx]);
						}
						submeshFaceLists[submesh].Add((ushort)submeshVertIndices[submesh][vertIdx]);
					}
				}

				remSkin boneList = rem.FindSkin(mesh.name, parser.RemFile.SKIC);
				bool skinned = boneList != null;
				int numBones = skinned ? boneList.numWeights : 0;
				string[] boneNames = new string[numBones];
				Matrix[] boneOffsets = new Matrix[numBones];
				for (int boneIdx = 0; boneIdx < numBones; boneIdx++)
				{
					boneNames[boneIdx] = boneList[boneIdx].bone.ToString();
					Matrix mirrored;
					if (!BoneMatrixDic.TryGetValue(boneNames[boneIdx], out mirrored))
					{
#if !DONT_MIRROR
						Vector3 translate, scale;
						Quaternion rotate;
						boneList[boneIdx].trans.Decompose(out scale, out rotate, out translate);
						mirrored = Matrix.Scaling(scale.X, scale.Y, -scale.Z) * Matrix.RotationQuaternion(rotate) * Matrix.Translation(translate);
#else
						mirrored = bone.Matrix;
#endif
						BoneMatrixDic.Add(boneNames[boneIdx], mirrored);
					}
					boneOffsets[boneIdx] = mirrored;
				}

				AnimationMeshContainer[] meshContainers = new AnimationMeshContainer[submeshFaceLists.Count];
				Vector3 min = new Vector3(Single.MaxValue);
				Vector3 max = new Vector3(Single.MinValue);
				for (int i = 0; i < submeshFaceLists.Count; i++)
				{
					List<ushort> faceList = submeshFaceLists[i];
					List<remVertex> vertexList = submeshVertLists[i];

					Mesh animationMesh = new Mesh(device, faceList.Count, vertexList.Count, MeshFlags.Managed, PositionBlendWeightsIndexedNormalTexturedColoured.Format);

					using (DataStream indexStream = animationMesh.LockIndexBuffer(LockFlags.None))
					{
						for (int j = 0; j < faceList.Count; j++)
						{
							indexStream.Write(faceList[j]);
						}
						animationMesh.UnlockIndexBuffer();
					}

					byte[][] vertexBoneIndices = null;
					float[][] vertexWeights = ConvertVertexWeights(vertexList, submeshVertIndices[i], boneList, out vertexBoneIndices);
					FillVertexBuffer(animationMesh, vertexList, vertexWeights, vertexBoneIndices, -1);

					var normalLines = new PositionBlendWeightsIndexedColored[vertexList.Count * 2];
					for (int j = 0; j < vertexList.Count; j++)
					{
						remVertex vertex = vertexList[j];
#if !DONT_MIRROR
						Vector3 position = new Vector3(vertex.Position.X, vertex.Position.Y, -vertex.Position.Z);
						Vector3 normal = new Vector3(vertex.Normal.X, vertex.Normal.Y, -vertex.Normal.Z);
#else
						Vector3 position = vertex.Position;
						Vector3 normal = vertex.Normal;
#endif
						float[] boneWeights = vertexWeights[j];

						normalLines[j * 2] = new PositionBlendWeightsIndexedColored(position, boneWeights, vertexBoneIndices[j], Color.Coral.ToArgb());
						normalLines[(j * 2) + 1] = new PositionBlendWeightsIndexedColored(position + normal, boneWeights, vertexBoneIndices[j], Color.Blue.ToArgb());

						min = Vector3.Minimize(min, position);
						max = Vector3.Maximize(max, position);
					}

					AnimationMeshContainer meshContainer = new AnimationMeshContainer();
					meshContainer.Name = animationFrame.Name;
					meshContainer.MeshData = new MeshData(animationMesh);
					meshContainer.NormalLines = normalLines;
					meshContainer.BoneNames = boneNames;
					meshContainer.BoneOffsets = boneOffsets;
					meshContainers[i] = meshContainer;

					remMaterial mat = rem.FindMaterial(mesh.materials[i], parser.RemFile.MATC);
					if (mat != null)
					{
						Material material3D = new Material();
						material3D.Ambient = new Color4(mat.ambient[0], mat.ambient[1], mat.ambient[2]);
						material3D.Diffuse = new Color4(mat.diffuse[0], mat.diffuse[1], mat.diffuse[2]);
						material3D.Emissive = new Color4(mat.emissive[0], mat.emissive[1], mat.emissive[2]);
						material3D.Specular = new Color4(mat.specular[0], mat.specular[1], mat.specular[2]);
						material3D.Power = mat.specularPower;
						int matIdx = parser.RemFile.MATC.materials.IndexOf(mat);
						Materials[matIdx] = material3D;
						meshContainer.MaterialIndex = matIdx;

						int texIdx = 0;
						if (mat.texture != null && !TextureDic.TryGetValue(mat.texture.ToString(), out texIdx))
						{
							ImportedTexture importedTex = null;
							if (!ImportedTextures.TryGetValue(mat.texture.ToString(), out importedTex))
							{
								importedTex = rem.ImportedTexture(mat.texture, Path.GetDirectoryName(parser.RemPath), true);
								ImportedTextures.Add(mat.texture.ToString(), importedTex);
							}
							Texture memTex = Texture.FromMemory(device, importedTex.Data);
							texIdx = TextureDic.Count;
							TextureDic.Add(mat.texture.ToString(), texIdx);
							Textures[texIdx] = memTex;
						}
						meshContainer.TextureIndex = texIdx;
					}
				}

				for (int i = 0; i < (meshContainers.Length - 1); i++)
				{
					meshContainers[i].NextMeshContainer = meshContainers[i + 1];
				}

				min = Vector3.TransformCoordinate(min, animationFrame.CombinedTransform);
				max = Vector3.TransformCoordinate(max, animationFrame.CombinedTransform);
				animationFrame.Bounds = new BoundingBox(min, max);
				animationFrame.MeshContainer = meshContainers[0];
				meshFrames.Add(animationFrame);
			}

			for (int i = 0; i < frame.numChilds; i++)
			{
				remBone child = frame[i];
				if (extractFrames.Contains(child.name.ToString()))
				{
					AnimationFrame childAnimationFrame = CreateFrame(child, parser, extractFrames, mesh, device, animationFrame.CombinedTransform, meshFrames);
					childAnimationFrame.Parent = animationFrame;
					animationFrame.AppendChild(childAnimationFrame);
				}
			}

			numFrames++;
			return animationFrame;
		}

		private float[][] ConvertVertexWeights(List<remVertex> vertexList, int[] vertexIndices, remSkin boneList, out byte[][] vertexBoneIndices)
		{
			int numBones = boneList != null ? boneList.numWeights : 0;
			float[][] vertexWeights = new float[vertexList.Count][];
			vertexBoneIndices = new byte[vertexList.Count][];
			for (int j = 0; j < vertexList.Count; j++)
			{
				int meshVertIdx = -1;
				for (int k = 0; k < vertexIndices.Length; k++)
				{
					if (vertexIndices[k] == j)
					{
						meshVertIdx = k;
						break;
					}
				}
				if (meshVertIdx < 0)
				{
					throw new Exception("ConvertVertexWeights : index not found");
				}
				vertexWeights[j] = new float[4];
				vertexBoneIndices[j] = new byte[4];
				int weightIdx = 0;
				for (int k = 0; k < numBones; k++)
				{
					remBoneWeights bone = boneList.weights[k];
					for (int l = 0; l < bone.numVertIdxWts; l++)
					{
						if (bone.vertexIndices[l] == meshVertIdx)
						{
							vertexBoneIndices[j][weightIdx] = (byte)k;
							if (weightIdx < 3)
							{
								vertexWeights[j][weightIdx++] = bone.vertexWeights[l];
							}
							else
							{
								vertexWeights[j][3] = 1f - vertexWeights[j][0] - vertexWeights[j][1] - vertexWeights[j][2];
							}
							break;
						}
					}
				}
			}

			return vertexWeights;
		}

		private void FillVertexBuffer(Mesh animationMesh, List<remVertex> vertexList, float[][] vertexWeights, byte[][] vertexBoneIndices, int selectedBoneIdx)
		{
			using (DataStream vertexStream = animationMesh.LockVertexBuffer(LockFlags.None))
			{
				Color4 col = new Color4(1f, 1f, 1f);
				for (int j = 0; j < vertexList.Count; j++)
				{
					remVertex vertex = vertexList[j];
#if !DONT_MIRROR
					Vector3 position = new Vector3(vertex.Position.X, vertex.Position.Y, -vertex.Position.Z);
					Vector3 normal = new Vector3(vertex.Normal.X, vertex.Normal.Y, -vertex.Normal.Z);
#else
					Vector3 position = vertex.Position;
					Vector3 normal = vertex.Normal;
#endif
					float[] boneWeights = vertexWeights[j];
					vertexStream.Write(position.X);
					vertexStream.Write(position.Y);
					vertexStream.Write(position.Z);
					vertexStream.Write(boneWeights[0]);
					vertexStream.Write(boneWeights[1]);
					vertexStream.Write(boneWeights[2]);
					vertexStream.Write(vertexBoneIndices[j][0]);
					vertexStream.Write(vertexBoneIndices[j][1]);
					vertexStream.Write(vertexBoneIndices[j][2]);
					vertexStream.Write(vertexBoneIndices[j][3]);
					vertexStream.Write(normal.X);
					vertexStream.Write(normal.Y);
					vertexStream.Write(normal.Z);
					if (selectedBoneIdx >= 0)
					{
						col.Red = 0f; col.Green = 0f; col.Blue = 0f;
						byte[] boneIndices = vertexBoneIndices[j];
						for (int k = 0; k < boneIndices.Length; k++)
						{
							if (boneIndices[k] == 0xFF)
							{
								continue;
							}

							byte boneIdx = boneIndices[k];
							if (boneIdx == selectedBoneIdx)
							{
/*								switch (cols)
								{
								case WeightsColourPreset.Greyscale:
									col.r = col.g = col.b = boneWeights[k];
									break;
								case WeightsColourPreset.Metal:
									col.r = boneWeights[k] > 0.666f ? 1f : boneWeights[k] * 1.5f;
									col.g = boneWeights[k] * boneWeights[k] * boneWeights[k];
									break;
								case WeightsColourPreset.Rainbow:*/
									if (boneWeights[k] > 0.75f)
									{
										col.Red = 1f;
										col.Green = (1f - boneWeights[k]) * 2f;
										col.Blue = 0f;
									}
									else if (boneWeights[k] > 0.5f)
									{
										col.Red = 1f;
										col.Green = (1f - boneWeights[k]) * 2f;
										col.Blue = 0f;
									}
									else if (boneWeights[k] > 0.25f)
									{
										col.Red = (boneWeights[k] - 0.25f) * 4f;
										col.Green = 1f;
										col.Blue = 0f;
									}
									else
									{
										col.Green = boneWeights[k] * 4f;
										col.Blue = 1f - boneWeights[k] * 4f;
									}
/*									break;
								}*/
								break;
							}
						}
					}
					vertexStream.Write(col.ToArgb());
					vertexStream.Write(vertex.UV[0]);
					vertexStream.Write(vertex.UV[1]);
				}
				animationMesh.UnlockVertexBuffer();
			}
		}

		private void SetupBoneMatrices(AnimationFrame frame, AnimationFrame root)
		{
			for (AnimationMeshContainer mesh = (AnimationMeshContainer)frame.MeshContainer;
				 mesh != null;
				 mesh = (AnimationMeshContainer)mesh.NextMeshContainer)
			{
				int numBones = mesh.BoneNames.Length;
				if (numBones <= 0)
					continue;

				AnimationFrame[] boneFrames = new AnimationFrame[numBones];
				for (int i = 0; i < numBones; i++)
				{
					AnimationFrame boneFrame = (AnimationFrame)root.FindChild(mesh.BoneNames[i]);
					boneFrames[i] = boneFrame;
				}
				mesh.BoneFrames = boneFrames;

				int boneObjSize = 16;
				float boneWidth = 0.1f;
				int boneColor = Color.CornflowerBlue.ToArgb();
				PositionBlendWeightIndexedColored[] boneLines = new PositionBlendWeightIndexedColored[numBones * boneObjSize];
				for (byte i = 0; i < numBones; i++)
				{
					AnimationFrame bone = boneFrames[i];

					if (bone != null && bone.Parent != null /*&& BoneMatrixDic.TryGetValue(bone.Parent.Name, out boneParentMatrix)*/)
					{
						byte parentBoneIdx = 0xFF;
						for (byte j = 0; j < mesh.BoneNames.Length; j++)
						{
							if (mesh.BoneNames[j] == bone.Parent.Name)
							{
								parentBoneIdx = j;
								break;
							}
						}
						if (parentBoneIdx == 0xFF)
						{
							continue;
						}
						Matrix boneMatrix = Matrix.Invert(mesh.BoneOffsets[i]/*boneMatrix*/);
						Matrix boneParentMatrix = Matrix.Invert(mesh.BoneOffsets[parentBoneIdx]/*boneParentMatrix*/);

						Vector3 bonePos = Vector3.TransformCoordinate(new Vector3(), boneMatrix);
//						bonePos.Z *= -1f;
						Vector3 boneParentPos = Vector3.TransformCoordinate(new Vector3(), boneParentMatrix);
//						boneParentPos.Z *= -1f;

						Vector3 direction = bonePos - boneParentPos;
						Vector3 perpendicular = direction.Perpendicular();
						Vector3 cross = Vector3.Cross(direction, perpendicular);
						perpendicular = Vector3.Normalize(perpendicular) * boneWidth;
						cross = Vector3.Normalize(cross) * boneWidth;

						Vector3 bottomLeft = -perpendicular + -cross + boneParentPos;
						Vector3 bottomRight = -perpendicular + cross + boneParentPos;
						Vector3 topLeft = perpendicular + -cross + boneParentPos;
						Vector3 topRight = perpendicular + cross + boneParentPos;

						boneLines[i * boneObjSize] = new PositionBlendWeightIndexedColored(bottomLeft, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 1] = new PositionBlendWeightIndexedColored(bottomRight, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 2] = new PositionBlendWeightIndexedColored(bottomRight, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 3] = new PositionBlendWeightIndexedColored(topRight, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 4] = new PositionBlendWeightIndexedColored(topRight, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 5] = new PositionBlendWeightIndexedColored(topLeft, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 6] = new PositionBlendWeightIndexedColored(topLeft, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 7] = new PositionBlendWeightIndexedColored(bottomLeft, parentBoneIdx, boneColor);

						boneLines[(i * boneObjSize) + 8] = new PositionBlendWeightIndexedColored(bottomLeft, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 9] = new PositionBlendWeightIndexedColored(bonePos, i, boneColor);
						boneLines[(i * boneObjSize) + 10] = new PositionBlendWeightIndexedColored(bottomRight, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 11] = new PositionBlendWeightIndexedColored(bonePos, i, boneColor);
						boneLines[(i * boneObjSize) + 12] = new PositionBlendWeightIndexedColored(topLeft, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 13] = new PositionBlendWeightIndexedColored(bonePos, i, boneColor);
						boneLines[(i * boneObjSize) + 14] = new PositionBlendWeightIndexedColored(topRight, parentBoneIdx, boneColor);
						boneLines[(i * boneObjSize) + 15] = new PositionBlendWeightIndexedColored(bonePos, i, boneColor);
					}
				}

				mesh.BoneLines = boneLines;
			}

			if (frame.Sibling != null)
			{
				SetupBoneMatrices(frame.Sibling as AnimationFrame, root);
			}

			if (frame.FirstChild != null)
			{
				SetupBoneMatrices(frame.FirstChild as AnimationFrame, root);
			}
		}

/*		public void HighlightBone(odfParser parser, int meshIdx, int submeshIdx, int boneIdx)
		{
			const int boneObjSize = 16;

			odfSubmesh submesh = parser.MeshSection[meshIdx][submeshIdx];
			odfBoneList boneList = odf.FindBoneList(submesh.Id, parser.EnvelopeSection);
			string boneFrameName = null;
			if (boneIdx >= 0)
			{
				odfBone bone = boneList[boneIdx];
				boneFrameName = odf.FindFrame(bone.FrameId, parser.FrameSection.RootFrame).Name;
			}

			AnimationMeshContainer mesh = (AnimationMeshContainer)meshFrames[0].MeshContainer;
			for (int i = 0; mesh != null; i++)
			{
				if (i == submeshIdx && (mesh.MeshData != null) && (mesh.MeshData.Mesh != null))
				{
					List<odfVertex> vertexList = submesh.VertexList;
					float[][] vertexWeights = ConvertVertexWeights(vertexList, boneList);
					FillVertexBuffer(mesh.MeshData.Mesh, vertexList, vertexWeights, boneIdx);
//					break;
				}
				if (boneIdx >= 0)
				{
					for (int idx = 0; idx < mesh.BoneLines.Length / boneObjSize; idx++)
					{
						if (mesh.BoneNames[idx] == boneFrameName)
						{
							for (int j = 0; j < boneObjSize; j++)
							{
								mesh.BoneLines[idx * boneObjSize + j].Color = Color.Crimson.ToArgb();
							}
							break;
						}
					}
				}
				else
				{
					for (int idx = 0; idx < mesh.BoneLines.Length / boneObjSize; idx++)
					{
						for (int j = 0; j < boneObjSize; j++)
						{
							mesh.BoneLines[idx * boneObjSize + j].Color = Color.CornflowerBlue.ToArgb();
						}
					}
				}
				mesh = (AnimationMeshContainer)mesh.NextMeshContainer;
			}
		}*/
	}
}
