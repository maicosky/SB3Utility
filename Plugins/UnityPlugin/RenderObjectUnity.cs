using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using SlimDX;
using SlimDX.Direct3D9;

using SB3Utility;

namespace UnityPlugin
{
	public class RenderObjectUnity : IDisposable, IRenderObject
	{
		private AnimationFrame rootFrame;
		private Device device;
		private VertexDeclaration tweeningVertDec;
		private List<AnimationFrame> meshFrames;
		private SlimDX.Direct3D9.Material highlightMaterial;
		private SlimDX.Direct3D9.Material nullMaterial = new SlimDX.Direct3D9.Material();
		private int submeshNum = 0;
		private int numFrames = 0;

		private Texture[] Textures;
		private SlimDX.Direct3D9.Material[] Materials;
		private Dictionary<int, int> MatTexIndices = new Dictionary<int, int>();

		public BoundingBox Bounds { get; protected set; }
		public AnimationController AnimationController { get; protected set; }
		public bool IsDisposed { get; protected set; }
		public HashSet<int> HighlightSubmesh { get; protected set; }

		const int BoneObjSize = 16;

		public RenderObjectUnity(AnimatorEditor editor, HashSet<string> meshNames)
		{
			HighlightSubmesh = new HashSet<int>();
			highlightMaterial = new SlimDX.Direct3D9.Material();
			highlightMaterial.Ambient = new Color4(1, 1, 1, 1);
			highlightMaterial.Diffuse = new Color4(1, 0, 1, 0);

			this.device = Gui.Renderer.Device;
			this.tweeningVertDec = new VertexDeclaration(this.device, TweeningWithoutNormalsVertexBufferFormat.ThreeStreams);

			Textures = new Texture[editor.Textures.Count];
			Materials = new SlimDX.Direct3D9.Material[editor.Materials.Count];

			rootFrame = CreateHierarchy(editor, meshNames, device, out meshFrames);

			AnimationController = new AnimationController(numFrames, 30, 30, 1);
			Frame.RegisterNamedMatrices(rootFrame, AnimationController);

			if (meshFrames.Count > 0)
			{
				Bounds = meshFrames[0].Bounds;
				for (int i = 1; i < meshFrames.Count; i++)
				{
					Bounds = BoundingBox.Merge(Bounds, meshFrames[i].Bounds);
				}
			}
			else
			{
				Bounds = new BoundingBox();
			}
		}

		~RenderObjectUnity()
		{
			Dispose();
		}

		public void Dispose()
		{
			for (int i = 0; i < meshFrames.Count; i++)
			{
				MeshContainer mesh = meshFrames[i].MeshContainer;
				while (mesh != null)
				{
					if ((mesh.MeshData != null) && (mesh.MeshData.Mesh != null))
					{
						mesh.MeshData.Mesh.Dispose();
					}
					if (mesh is MorphMeshContainer)
					{
						MorphMeshContainer morphMesh = (MorphMeshContainer)mesh;
						if (morphMesh.StartBuffer != morphMesh.EndBuffer)
						{
							morphMesh.StartBuffer.Dispose();
						}
						if (morphMesh.EndBuffer != null)
						{
							morphMesh.EndBuffer.Dispose();
						}
						if (morphMesh.CommonBuffer != null)
						{
							morphMesh.CommonBuffer.Dispose();
						}
						if (morphMesh.IndexBuffer != null)
						{
							morphMesh.IndexBuffer.Dispose();
						}
					}

					mesh = mesh.NextMeshContainer;
				}
			}

			for (int i = 0; i < Textures.Length; i++)
			{
				Texture tex = Textures[i];
				if ((tex != null) && !tex.Disposed)
				{
					tex.Dispose();
				}
			}

			rootFrame.Dispose();
			AnimationController.Dispose();

			tweeningVertDec.Dispose();

			IsDisposed = true;
		}

		public void Render()
		{
			UpdateFrameMatrices(rootFrame, Matrix.Scaling(-1, 1, 1));

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
			if (frame.MeshContainer is AnimationMeshContainer)
			{
				AnimationMeshContainer animMeshContainer = (AnimationMeshContainer)frame.MeshContainer;
				if (animMeshContainer.BoneNames != null && animMeshContainer.BoneNames.Length > 0)
				{
					device.SetRenderState(RenderState.VertexBlend, VertexBlend.Weights3);
					device.SetRenderState(RenderState.IndexedVertexBlendEnable, true);
					device.SetRenderState(RenderState.DiffuseMaterialSource, ColorSource.Material);
					device.SetRenderState(RenderState.AmbientMaterialSource, ColorSource.Material);
					switch (Gui.Renderer.ShowBoneWeights)
					{
					case ShowBoneWeights.Weak:
						device.SetRenderState(RenderState.AmbientMaterialSource, ColorSource.Color1);
						break;
					case ShowBoneWeights.Strong:
						device.SetRenderState(RenderState.DiffuseMaterialSource, ColorSource.Color1);
						break;
					case ShowBoneWeights.Off:
						break;
					}

					for (int i = 0; i < animMeshContainer.BoneNames.Length; i++)
					{
						if (animMeshContainer.BoneFrames[i] != null)
						{
							device.SetTransform(i, animMeshContainer.BoneOffsets[i] * animMeshContainer.BoneFrames[i].CombinedTransform);
						}
					}
				}
				else
				{
					device.SetRenderState(RenderState.VertexBlend, VertexBlend.Disable);
					device.SetRenderState(RenderState.AmbientMaterialSource, ColorSource.Material);
					device.SetTransform(TransformState.World, frame.CombinedTransform * Matrix.Scaling(-1, 1, 1));
				}

				submeshNum = 0;
				while (animMeshContainer != null)
				{
					DrawAnimationMeshContainer(animMeshContainer);
					animMeshContainer = (AnimationMeshContainer)animMeshContainer.NextMeshContainer;
					submeshNum++;
				}
			}
			else if (frame.MeshContainer is MorphMeshContainer)
			{
				MorphMeshContainer morphMeshContainer = (MorphMeshContainer)frame.MeshContainer;
				device.SetRenderState(RenderState.AmbientMaterialSource, ColorSource.Material);
				device.SetTransform(TransformState.World, frame.CombinedTransform * Matrix.Scaling(-1, 1, 1));

				submeshNum = 0;
				while (morphMeshContainer != null)
				{
					DrawMorphMeshContainer(morphMeshContainer);
					morphMeshContainer = morphMeshContainer.NextMeshContainer as MorphMeshContainer;
					submeshNum++;
				}
			}
		}

		private void DrawAnimationMeshContainer(AnimationMeshContainer meshContainer)
		{
			if (meshContainer.MeshData != null)
			{
				device.SetRenderState(RenderState.ZEnable, ZBufferType.UseZBuffer);
				device.SetRenderState(RenderState.Lighting, true);

				Cull culling = (Gui.Renderer.Culling) ? Cull.Clockwise : Cull.None;
				device.SetRenderState(RenderState.CullMode, culling);

				FillMode fill = (Gui.Renderer.Wireframe) ? FillMode.Wireframe : FillMode.Solid;
				device.SetRenderState(RenderState.FillMode, fill);

				int matIdx = meshContainer.MaterialIndex;
				device.Material = ((matIdx >= 0) && (matIdx < Materials.Length)) ? Materials[matIdx] : nullMaterial;

				int texIdx = meshContainer.TextureIndex;
				Texture tex = ((texIdx >= 0) && (texIdx < Textures.Length)) ? Textures[texIdx] : null;
				device.SetTexture(0, tex);

				meshContainer.MeshData.Mesh.DrawSubset(0);
			}

			if (HighlightSubmesh.Contains(submeshNum) && meshContainer.MeshData != null)
			{
				device.SetRenderState(RenderState.ZEnable, ZBufferType.DontUseZBuffer);
				device.SetRenderState(RenderState.FillMode, FillMode.Wireframe);
				device.Material = highlightMaterial;
				device.SetTexture(0, null);
				meshContainer.MeshData.Mesh.DrawSubset(0);
			}

			if (Gui.Renderer.ShowNormals && meshContainer.NormalLines != null)
			{
				device.SetRenderState(RenderState.ZEnable, ZBufferType.UseZBuffer);
				device.SetRenderState(RenderState.Lighting, false);
				device.Material = nullMaterial;
				device.SetTexture(0, null);
				device.VertexFormat = PositionBlendWeightsIndexedColored.Format;
				device.DrawUserPrimitives(PrimitiveType.LineList, meshContainer.NormalLines.Length / 2, meshContainer.NormalLines);
			}

			if (Gui.Renderer.ShowBones && (meshContainer.BoneLines != null) && meshContainer.BoneLines.Length > 0)
			{
				device.SetRenderState(RenderState.ZEnable, ZBufferType.DontUseZBuffer);
				device.SetRenderState(RenderState.VertexBlend, VertexBlend.Weights1);
				device.SetRenderState(RenderState.Lighting, false);
				device.Material = nullMaterial;
				device.SetTexture(0, null);
				device.VertexFormat = PositionBlendWeightIndexedColored.Format;
				if (meshContainer.SelectedBone == -1)
				{
					device.DrawUserPrimitives(PrimitiveType.LineList, meshContainer.BoneLines.Length / 2, meshContainer.BoneLines);
				}
				else
				{
					if (meshContainer.SelectedBone > 0)
					{
						device.DrawUserPrimitives(PrimitiveType.LineList, 0, (meshContainer.SelectedBone * BoneObjSize) / 2, meshContainer.BoneLines);
					}
					int rest = meshContainer.BoneLines.Length / BoneObjSize - (meshContainer.SelectedBone + 1);
					if (rest > 0)
					{
						device.DrawUserPrimitives(PrimitiveType.LineList, (meshContainer.SelectedBone + 1) * BoneObjSize, (rest * BoneObjSize) / 2, meshContainer.BoneLines);
					}
					device.DrawUserPrimitives(PrimitiveType.LineList, meshContainer.SelectedBone * BoneObjSize, BoneObjSize / 2, meshContainer.BoneLines);
				}
			}
		}

		private void DrawMorphMeshContainer(MorphMeshContainer meshContainer)
		{
			device.SetRenderState(RenderState.ZEnable, ZBufferType.UseZBuffer);
			device.SetRenderState(RenderState.Lighting, true);

			Cull culling = (Gui.Renderer.Culling) ? Cull.Clockwise : Cull.None;
			device.SetRenderState(RenderState.CullMode, culling);

			FillMode fill = (Gui.Renderer.Wireframe) ? FillMode.Wireframe : FillMode.Solid;
			device.SetRenderState(RenderState.FillMode, fill);

			int matIdx = meshContainer.MaterialIndex;
			device.Material = ((matIdx >= 0) && (matIdx < Materials.Length)) ? Materials[matIdx] : nullMaterial;

			int texIdx = meshContainer.TextureIndex;
			Texture tex = ((texIdx >= 0) && (texIdx < Textures.Length)) ? Textures[texIdx] : null;
			device.SetTexture(0, tex);

			device.SetRenderState(RenderState.VertexBlend, VertexBlend.Tweening);
			device.SetRenderState(RenderState.TweenFactor, meshContainer.TweenFactor);

			device.VertexDeclaration = tweeningVertDec;
			device.Indices = meshContainer.IndexBuffer;
			device.SetStreamSource(0, meshContainer.StartBuffer, 0, Marshal.SizeOf(typeof(TweeningWithoutNormalsVertexBufferFormat.Stream0)));
			device.SetStreamSource(1, meshContainer.EndBuffer, 0, Marshal.SizeOf(typeof(TweeningWithoutNormalsVertexBufferFormat.Stream1)));
			device.SetStreamSource(2, meshContainer.CommonBuffer, 0, Marshal.SizeOf(typeof(TweeningWithoutNormalsVertexBufferFormat.Stream2)));
			device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, meshContainer.VertexCount, 0, meshContainer.FaceCount);

			if (HighlightSubmesh.Contains(submeshNum))
			{
				device.SetRenderState(RenderState.ZEnable, ZBufferType.DontUseZBuffer);
				device.SetRenderState(RenderState.FillMode, FillMode.Wireframe);
				device.Material = highlightMaterial;
				device.SetTexture(0, null);
				device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, meshContainer.VertexCount, 0, meshContainer.FaceCount);
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

		private AnimationFrame CreateHierarchy(AnimatorEditor editor, HashSet<string> meshNames, Device device, out List<AnimationFrame> meshFrames)
		{
			meshFrames = new List<AnimationFrame>(meshNames.Count);
			HashSet<string> extractFrames = Operations.SearchHierarchy(editor.Parser.RootTransform, meshNames);
			Dictionary<string, Tuple<Matrix, Matrix>> extractMatrices = new Dictionary<string, Tuple<Matrix, Matrix>>();
			CreateCombinedMatrices(editor.Parser.RootTransform, extractFrames, Matrix.Scaling(-1, 1, 1), extractMatrices);
			AnimationFrame rootFrame = CreateFrame(editor.Parser.RootTransform, editor, extractFrames, meshNames, device, meshFrames, extractMatrices);
			SetupBoneMatrices(rootFrame, rootFrame);
			return rootFrame;
		}

		private static HashSet<string> messageFilterCreateCombineMatrices = new HashSet<string>();

		private void CreateCombinedMatrices(Transform frame, HashSet<string> extractFrames, Matrix combinedParent, Dictionary<string, Tuple<Matrix, Matrix>> extractMatrices)
		{
			Matrix combinedTransform = combinedParent * Matrix.Scaling(frame.m_LocalScale) * Matrix.RotationQuaternion(frame.m_LocalRotation) * Matrix.Translation(frame.m_LocalPosition);
			try
			{
				extractMatrices.Add(frame.GetTransformPath(), new Tuple<Matrix, Matrix>(combinedTransform, Matrix.Invert(combinedTransform)));
			}
			catch (ArgumentException)
			{
				string msg = "A transform named " + frame.m_GameObject.instance.m_Name + " already exists.";
				if (!messageFilterCreateCombineMatrices.Contains(msg))
				{
					Report.ReportLog(msg);
					messageFilterCreateCombineMatrices.Add(msg);
				}
			}

			for (int i = 0; i < frame.Count; i++)
			{
				Transform child = frame[i];
				if (extractFrames.Contains(child.GetTransformPath()))
				{
					CreateCombinedMatrices(child, extractFrames, combinedTransform, extractMatrices);
				}
			}
		}

		private AnimationFrame CreateFrame(Transform frame, AnimatorEditor editor, HashSet<string> extractFrames, HashSet<string> meshNames, Device device, List<AnimationFrame> meshFrames, Dictionary<string, Tuple<Matrix, Matrix>> extractMatrices)
		{
			AnimationFrame animationFrame = new AnimationFrame();
			animationFrame.Name = frame.GetTransformPath();
			animationFrame.TransformationMatrix = Matrix.Scaling(frame.m_LocalScale) * Matrix.RotationQuaternion(frame.m_LocalRotation) * Matrix.Translation(frame.m_LocalPosition);
			animationFrame.OriginalTransform = animationFrame.TransformationMatrix;
			animationFrame.CombinedTransform = extractMatrices[animationFrame.Name].Item1;

			if (meshNames.Contains(animationFrame.Name))
			{
				MeshRenderer meshR = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
				if (meshR == null)
				{
					meshR = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshRenderer);
				}
				if (meshR != null)
				{
					Mesh mesh = Operations.GetMesh(meshR);
					if (mesh != null)
					{
						SkinnedMeshRenderer smr = meshR as SkinnedMeshRenderer;
						List<PPtr<Transform>> boneList = null;
						string[] boneNames = null;
						Matrix[] boneOffsets = null;
						if (smr != null && smr.m_Bones.Count > 0)
						{
							boneList = smr.m_Bones;

							int numBones = boneList.Count > 0 ? extractFrames.Count : 0;
							boneNames = new string[numBones];
							boneOffsets = new Matrix[numBones];
							if (numBones > 0)
							{
								string[] extractArray = new string[numBones];
								extractFrames.CopyTo(extractArray);
								HashSet<string> extractCopy = new HashSet<string>(extractArray);
								int invalidBones = 0;
								for (int i = 0; i < boneList.Count; i++)
								{
									Transform bone = boneList[i].instance;
									if (bone == null || bone.m_GameObject.instance == null || !extractCopy.Remove(bone.GetTransformPath()))
									{
										invalidBones++;
									}
									else if (i < numBones)
									{
										boneNames[i] = bone.GetTransformPath();
										boneOffsets[i] = Operations.Mirror(Matrix.Transpose(mesh.m_BindPose[i]));
									}
								}
								extractCopy.CopyTo(boneNames, boneList.Count - invalidBones);
								for (int i = boneList.Count; i < extractFrames.Count; i++)
								{
									boneOffsets[i] = extractMatrices[boneNames[i]].Item2;
								}
							}
						}

						AnimationMeshContainer[] meshContainers = new AnimationMeshContainer[mesh.m_SubMeshes.Count];
						Vector3 min = new Vector3(Single.MaxValue);
						Vector3 max = new Vector3(Single.MinValue);
						Operations.vMesh vMesh = new Operations.vMesh(meshR, true, true);
						for (int i = 0; i < mesh.m_SubMeshes.Count; i++)
						{
							Operations.vSubmesh submesh = vMesh.submeshes[i];
							List<Operations.vFace> faceList = submesh.faceList;
							List<Operations.vVertex> vertexList = submesh.vertexList;

							SlimDX.Direct3D9.Mesh animationMesh = null;
							PositionBlendWeightsIndexedColored[] normalLines = null;
							try
							{
								animationMesh = new SlimDX.Direct3D9.Mesh(device, faceList.Count, vertexList.Count, MeshFlags.Managed, PositionBlendWeightsIndexedNormalTexturedColoured.Format);

								using (DataStream indexStream = animationMesh.LockIndexBuffer(LockFlags.None))
								{
									for (int j = 0; j < faceList.Count; j++)
									{
										ushort[] indices = faceList[j].index;
										indexStream.Write(indices[0]);
										indexStream.Write(indices[2]);
										indexStream.Write(indices[1]);
									}
									animationMesh.UnlockIndexBuffer();
								}

								FillVertexBuffer(animationMesh, vertexList, -1);

								normalLines = new PositionBlendWeightsIndexedColored[vertexList.Count * 2];
								for (int j = 0; j < vertexList.Count; j++)
								{
									Operations.vVertex vertex = vertexList[j];

									byte[] bIdx;
									float[] bWeights;
									if (vertex.boneIndices != null)
									{
										bIdx = new byte[4] { (byte)vertex.boneIndices[0], (byte)vertex.boneIndices[1], (byte)vertex.boneIndices[2], (byte)vertex.boneIndices[3] };
										bWeights = vertex.weights;
									}
									else
									{
										bIdx = new byte[4];
										bWeights = new float[4];
									}
									normalLines[j * 2] = new PositionBlendWeightsIndexedColored(vertex.position, bWeights, bIdx, Color.Yellow.ToArgb());
									normalLines[(j * 2) + 1] = new PositionBlendWeightsIndexedColored(vertex.position + (vertex.normal / 64), bWeights, bIdx, Color.Yellow.ToArgb());

									min = Vector3.Minimize(min, vertex.position);
									max = Vector3.Maximize(max, vertex.position);
								}
							}
							catch
							{
								Report.ReportLog("No display of submeshes with more than 64k vertices!");
							}

							AnimationMeshContainer meshContainer = new AnimationMeshContainer();
							if (animationMesh != null)
							{
								meshContainer.Name = animationFrame.Name;
								meshContainer.MeshData = new MeshData(animationMesh);
								meshContainer.NormalLines = normalLines;
							}
							meshContainers[i] = meshContainer;

							if (submesh.matList.Count > 0 && submesh.matList[0].instance != null)
							{
								Material mat = submesh.matList[0].instance;
								int matIdx = editor.Materials.IndexOf(mat);
								int texIdx;
								if (!MatTexIndices.TryGetValue(matIdx, out texIdx))
								{
									texIdx = -1;

									SlimDX.Direct3D9.Material materialD3D = new SlimDX.Direct3D9.Material();
									materialD3D.Ambient = GetColour(mat, "_SColor");
									materialD3D.Diffuse = GetColour(mat, "_Color");
									materialD3D.Emissive = GetColour(mat, "_ReflectColor");
									materialD3D.Specular = GetColour(mat, "_SpecColor");
									materialD3D.Power = GetFloat(mat, "_Shininess");
									Materials[matIdx] = materialD3D;

									Texture2D matTex = GetTexture(mat, "_MainTex");
									if (matTex != null)
									{
										texIdx = editor.Textures.IndexOf(matTex);
										if (Textures[texIdx] == null)
										{
											using (MemoryStream mem = new MemoryStream())
											{
												matTex.Export(mem);
												mem.Position = 0;
												ImportedTexture image = new ImportedTexture(mem, matTex.m_Name);
												Textures[texIdx] = Texture.FromMemory(device, image.Data);
											}
										}
									}

									MatTexIndices.Add(matIdx, texIdx);
								}

								meshContainer.MaterialIndex = matIdx;
								meshContainer.TextureIndex = texIdx;
							}
						}

						for (int i = 0; i < (meshContainers.Length - 1); i++)
						{
							meshContainers[i].NextMeshContainer = meshContainers[i + 1];
						}
						if (boneList != null)
						{
							for (int i = 0; i < meshContainers.Length; i++)
							{
								meshContainers[i].BoneNames = boneNames;
								meshContainers[i].BoneOffsets = boneOffsets;
								meshContainers[i].RealBones = boneList.Count;
							}
						}

						Matrix mirrorCombined = Operations.Mirror(animationFrame.CombinedTransform);
						min = Vector3.TransformCoordinate(min, mirrorCombined);
						max = Vector3.TransformCoordinate(max, mirrorCombined);
						animationFrame.Bounds = new BoundingBox(min, max);
						animationFrame.MeshContainer = meshContainers[0];
						meshFrames.Add(animationFrame);
					}
				}
			}

			for (int i = 0; i < frame.Count; i++)
			{
				Transform child = frame[i];
				if (extractFrames.Contains(child.GetTransformPath()))
				{
					AnimationFrame childAnimationFrame = CreateFrame(child, editor, extractFrames, meshNames, device, meshFrames, extractMatrices);
					childAnimationFrame.Parent = animationFrame;
					animationFrame.AppendChild(childAnimationFrame);
				}
			}

			numFrames++;
			return animationFrame;
		}

		private Texture2D GetTexture(Material mat, string name)
		{
			try
			{
				UnityTexEnv texEnv = mat.m_SavedProperties.GetTex(name);
				return texEnv.m_Texture.instance;
			}
			catch
			{
				return null;
			}
		}

		private float GetFloat(Material mat, string name)
		{
			try
			{
				return mat.m_SavedProperties.GetFloat(name);
			}
			catch
			{
				return 1f;
			}
		}

		private Color4 GetColour(Material mat, string name)
		{
			try
			{
				return mat.m_SavedProperties.GetColour(name);
			}
			catch
			{
				return new Color4(Color.White);
			}
		}

		private void FillVertexBuffer(SlimDX.Direct3D9.Mesh animationMesh, List<Operations.vVertex> vertexList, int selectedBoneIdx)
		{
			using (DataStream vertexStream = animationMesh.LockVertexBuffer(LockFlags.None))
			{
				Color4 col = new Color4(1f, 1f, 1f);
				for (int i = 0; i < vertexList.Count; i++)
				{
					Operations.vVertex vertex = vertexList[i];
					vertexStream.Write(vertex.position.X);
					vertexStream.Write(vertex.position.Y);
					vertexStream.Write(vertex.position.Z);
					if (vertex.boneIndices != null)
					{
						vertexStream.Write(vertex.weights[0]);
						vertexStream.Write(vertex.weights[1]);
						vertexStream.Write(vertex.weights[2]);
						vertexStream.Write((byte)vertex.boneIndices[0]);
						vertexStream.Write((byte)vertex.boneIndices[1]);
						vertexStream.Write((byte)vertex.boneIndices[2]);
						vertexStream.Write((byte)vertex.boneIndices[3]);
					}
					else
					{
						vertexStream.Write((float)0);
						vertexStream.Write((float)0);
						vertexStream.Write((float)0);
						vertexStream.Write((byte)0);
						vertexStream.Write((byte)0);
						vertexStream.Write((byte)0);
						vertexStream.Write((byte)0);
					}
					vertexStream.Write(vertex.normal.X);
					vertexStream.Write(vertex.normal.Y);
					vertexStream.Write(vertex.normal.Z);
					if (selectedBoneIdx >= 0)
					{
						col.Red = 0f; col.Green = 0f; col.Blue = 0f;
						int[] boneIndices = vertex.boneIndices;
						float[] boneWeights = vertex.weights;
						for (int j = 0; j < boneIndices.Length; j++)
						{
							if (boneIndices[j] == 0 && boneWeights[j] == 0)
							{
								continue;
							}

							int boneIdx = boneIndices[j];
							if (boneIdx == selectedBoneIdx)
							{
/*								switch (cols)
								{
								case WeightsColourPreset.Greyscale:
									col.r = col.g = col.b = boneWeights[j];
									break;
								case WeightsColourPreset.Metal:
									col.r = boneWeights[j] > 0.666f ? 1f : boneWeights[j] * 1.5f;
									col.g = boneWeights[j] * boneWeights[j] * boneWeights[j];
									break;
								WeightsColourPreset.Rainbow:*/
									if (boneWeights[j] > 0.75f)
									{
										col.Red = 1f;
										col.Green = (1f - boneWeights[j]) * 2f;
										col.Blue = 0f;
									}
									else if (boneWeights[j] > 0.5f)
									{
										col.Red = 1f;
										col.Green = (1f - boneWeights[j]) * 2f;
										col.Blue = 0f;
									}
									else if (boneWeights[j] > 0.25f)
									{
										col.Red = (boneWeights[j] - 0.25f) * 4f;
										col.Green = 1f;
										col.Blue = 0f;
									}
									else
									{
										col.Green = boneWeights[j] * 4f;
										col.Blue = 1f - boneWeights[j] * 4f;
									}
/*									break;
								}*/
								break;
							}
						}
					}
					vertexStream.Write(col.ToArgb());
					vertexStream.Write(vertex.uv[0]);
					vertexStream.Write(vertex.uv[1]);
				}
				animationMesh.UnlockVertexBuffer();
			}
		}

		private void SetupBoneMatrices(AnimationFrame frame, AnimationFrame root)
		{
			AnimationMeshContainer mesh = (AnimationMeshContainer)frame.MeshContainer;
			if (mesh != null)
			{
				AnimationFrame[] boneFrames = null;
				PositionBlendWeightIndexedColored[] boneLines = null;
				if (mesh.RealBones > 0)
				{
					int numBones = mesh.BoneNames.Length;
					boneFrames = new AnimationFrame[numBones];
					var boneDic = new Dictionary<string, int>();
					int topBone = -1;
					int topLevel = 100;
					for (int i = 0; i < numBones; i++)
					{
						string boneName = mesh.BoneNames[i];
						if (boneName != null)
						{
							AnimationFrame bone = (AnimationFrame)root.FindChild(boneName);
							boneFrames[i] = bone;

							boneDic.Add(boneName, i);

							if (i < mesh.RealBones)
							{
								int level = 0;
								while (bone != root)
								{
									bone = bone.Parent;
									level++;
								}
								if (level < topLevel)
								{
									topLevel = level;
									topBone = i;
								}
							}
						}
					}

					List<PositionBlendWeightIndexedColored> boneLineList = new List<PositionBlendWeightIndexedColored>(numBones * BoneObjSize);
					for (int i = 0; i < numBones; i++)
					{
						float boneWidth = 0.009f;
						AnimationFrame bone = boneFrames[i];
						if (bone == null)
						{
							continue;
						}
						AnimationFrame parent = bone.Parent;
						if (parent == null)
						{
							continue;
						}

						Matrix boneMatrix = Matrix.Invert(mesh.BoneOffsets[i]);
						Vector3 bonePos = Vector3.TransformCoordinate(new Vector3(), boneMatrix);
						if (i >= mesh.RealBones && bonePos.X == 0 && bonePos.Y == 0 && bonePos.Z == 0)
						{
							continue;
						}
						int realParentId;
						Vector3 boneParentPos;
						if (i != topBone)
						{
							boneDic.TryGetValue(parent.Name, out realParentId);
							Matrix boneParentMatrix = Matrix.Invert(mesh.BoneOffsets[realParentId]);
							boneParentPos = Vector3.TransformCoordinate(new Vector3(), boneParentMatrix);
							if (i >= mesh.RealBones && boneParentPos.X == 0 && boneParentPos.Y == 0 && boneParentPos.Z == 0)
							{
								continue;
							}
						}
						else
						{
							realParentId = i;
							boneParentPos = bonePos;
						}

						float lenlen = (bonePos - boneParentPos).LengthSquared();
						if (lenlen < 1E-12)
						{
							boneWidth /= 3f;
							int level = 0;
							while (parent != null)
							{
								level++;
								parent = parent.Parent;
							}
							if ((level % 2) == 0)
							{
								bonePos.Y -= boneWidth;
								boneParentPos.Y += boneWidth;
							}
							else
							{
								bonePos.Y += boneWidth;
								boneParentPos.Y -= boneWidth;
							}
						}
						else if (lenlen < 0.0001)
						{
							boneWidth /= 2f;
						}

						Vector3 direction = bonePos - boneParentPos;
						float scale = boneWidth * (1 + direction.Length() / 2);
						Vector3 perpendicular = direction.Perpendicular();
						Vector3 cross = Vector3.Cross(direction, perpendicular);
						perpendicular = Vector3.Normalize(perpendicular) * scale;
						cross = Vector3.Normalize(cross) * scale;

						Vector3 bottomLeft = -perpendicular + -cross + boneParentPos;
						Vector3 bottomRight = -perpendicular + cross + boneParentPos;
						Vector3 topLeft = perpendicular + -cross + boneParentPos;
						Vector3 topRight = perpendicular + cross + boneParentPos;

						int boneColor = i < mesh.RealBones ? Color.CornflowerBlue.ToArgb() : Color.BlueViolet.ToArgb();
						if (realParentId < 256 && i < 256)
						{
							byte boneParentId = (byte)realParentId;
							boneLineList.Add(new PositionBlendWeightIndexedColored(bottomLeft, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(bottomRight, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(bottomRight, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(topRight, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(topRight, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(topLeft, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(topLeft, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(bottomLeft, boneParentId, boneColor));

							byte boneId = (byte)i;
							boneLineList.Add(new PositionBlendWeightIndexedColored(bottomLeft, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(bonePos, boneId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(bottomRight, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(bonePos, boneId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(topLeft, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(bonePos, boneId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(topRight, boneParentId, boneColor));
							boneLineList.Add(new PositionBlendWeightIndexedColored(bonePos, boneId, boneColor));
						}
						else
						{
							for (int j = 0; j < BoneObjSize; j++)
							{
								boneLineList.Add(new PositionBlendWeightIndexedColored());
							}
						}
					}
					boneLines = boneLineList.ToArray();
				}

				while (mesh != null)
				{
					if (mesh.NextMeshContainer == null)
					{
						mesh.BoneLines = boneLines;
					}

					mesh.BoneFrames = boneFrames;
					mesh = (AnimationMeshContainer)mesh.NextMeshContainer;
				}
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

		public void HighlightBone(MeshRenderer meshR, int boneIdx, bool show)
		{
			Operations.vMesh vMesh = new Operations.vMesh(meshR, false, true);
			int submeshIdx = 0;
			for (AnimationMeshContainer mesh = meshFrames[0].MeshContainer as AnimationMeshContainer;
				 mesh != null;
				 mesh = (AnimationMeshContainer)mesh.NextMeshContainer, submeshIdx++)
			{
				if (mesh.MeshData != null && mesh.MeshData.Mesh != null)
				{
					List<Operations.vVertex> vertexList = vMesh.submeshes[submeshIdx].vertexList;
					FillVertexBuffer(mesh.MeshData.Mesh, vertexList, show ? boneIdx : -1);
					if (show)
					{
						Materials[mesh.MaterialIndex].Ambient = new Color4(unchecked((int)0xFF060660));
						Materials[mesh.MaterialIndex].Emissive = Color.Black;
					}
					else
					{
						Material mat = vMesh.submeshes[submeshIdx].matList[0].instance;
						Materials[mesh.MaterialIndex].Ambient = GetColour(mat, "_SColor");
						Materials[mesh.MaterialIndex].Emissive = GetColour(mat, "_ReflectColor");
					}
				}
				if (mesh.BoneLines != null)
				{
					if (boneIdx < mesh.BoneLines.Length / BoneObjSize)
					{
						for (int j = 0; j < BoneObjSize; j++)
						{
							mesh.BoneLines[boneIdx * BoneObjSize + j].Color = show ? Color.Crimson.ToArgb() : Color.CornflowerBlue.ToArgb();
						}
						mesh.SelectedBone = boneIdx;
					}
				}
			}
		}

		public float SetMorphKeyframe(SkinnedMeshRenderer sMesh, int keyframeIdx, bool asStart)
		{
			foreach (AnimationFrame frame in meshFrames)
			{
				Transform meshTransform = sMesh.m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
				if (frame.Name == meshTransform.GetTransformPath())
				{
					Mesh mesh = Operations.GetMesh(sMesh);
					AnimationMeshContainer animMesh = frame.MeshContainer as AnimationMeshContainer;
					if (animMesh != null)
					{
						MorphMeshContainer[] morphMeshes = new MorphMeshContainer[mesh.m_SubMeshes.Count];
						int startVertexIdx = 0;
						for (int meshObjIdx = 0; meshObjIdx < mesh.m_SubMeshes.Count; meshObjIdx++)
						{
							MorphMeshContainer morphMesh = new MorphMeshContainer();
							morphMeshes[meshObjIdx] = morphMesh;
							morphMesh.FaceCount = (int)mesh.m_SubMeshes[meshObjIdx].indexCount / 3;
							morphMesh.IndexBuffer = animMesh.MeshData.Mesh.IndexBuffer;

							morphMesh.VertexCount = (int)mesh.m_SubMeshes[meshObjIdx].vertexCount;
							Operations.vMesh vMesh = new Operations.vMesh(sMesh, false, false);
							List<Operations.vVertex> vertexList = vMesh.submeshes[meshObjIdx].vertexList;
							VertexBuffer vertBuffer = CreateMorphVertexBuffer(mesh.m_Shapes, keyframeIdx, vertexList, startVertexIdx);
							morphMesh.StartBuffer = morphMesh.EndBuffer = vertBuffer;

							int vertBufferSize = morphMesh.VertexCount * Marshal.SizeOf(typeof(TweeningWithoutNormalsVertexBufferFormat.Stream2));
							vertBuffer = new VertexBuffer(device, vertBufferSize, Usage.WriteOnly, VertexFormat.Texture1, Pool.Managed);
							using (DataStream vertexStream = vertBuffer.Lock(0, vertBufferSize, LockFlags.None))
							{
								for (int i = 0; i < vertexList.Count; i++)
								{
									Operations.vVertex vertex = vertexList[i];
									vertexStream.Write(vertex.uv[0]);
									vertexStream.Write(vertex.uv[1]);
								}
								vertBuffer.Unlock();
							}
							morphMesh.CommonBuffer = vertBuffer;

							morphMesh.MaterialIndex = animMesh.MaterialIndex;
							morphMesh.TextureIndex = animMesh.TextureIndex;

							morphMesh.TweenFactor = 0.0f;

							startVertexIdx += morphMesh.VertexCount;
							animMesh = (AnimationMeshContainer)animMesh.NextMeshContainer;
						}

						for (int meshObjIdx = 0; meshObjIdx < mesh.m_SubMeshes.Count; meshObjIdx++)
						{
							morphMeshes[meshObjIdx].NextMeshContainer = meshObjIdx < mesh.m_SubMeshes.Count - 1
								? (MeshContainer)morphMeshes[meshObjIdx + 1] : frame.MeshContainer;
						}
						frame.MeshContainer = morphMeshes[0];
						return 0;
					}
					else
					{
						MorphMeshContainer morphMesh = frame.MeshContainer as MorphMeshContainer;
						Operations.vMesh vMesh = new Operations.vMesh(sMesh, false, false);
						int startVertexIdx = 0;
						for (int meshObjIdx = 0; meshObjIdx < mesh.m_SubMeshes.Count; meshObjIdx++)
						{
							List<Operations.vVertex> vertexList = vMesh.submeshes[meshObjIdx].vertexList;
							VertexBuffer vertBuffer = CreateMorphVertexBuffer(mesh.m_Shapes, keyframeIdx, vertexList, startVertexIdx);
							if (asStart)
							{
								if (morphMesh.StartBuffer != morphMesh.EndBuffer)
								{
									morphMesh.StartBuffer.Dispose();
								}
								morphMesh.StartBuffer = vertBuffer;
								morphMesh.TweenFactor = 0.0f;
							}
							else
							{
								if (morphMesh.StartBuffer != morphMesh.EndBuffer)
								{
									morphMesh.EndBuffer.Dispose();
								}
								morphMesh.EndBuffer = vertBuffer;
								morphMesh.TweenFactor = 1.0f;
							}

							startVertexIdx += morphMesh.VertexCount;
							morphMesh = morphMesh.NextMeshContainer as MorphMeshContainer;
						}
						return asStart ? 0 : 1;
					}
				}
			}
			Report.ReportLog("Mesh frame " + sMesh.m_GameObject.instance.m_Name + " not displayed.");
			return -1f;
		}

		private VertexBuffer CreateMorphVertexBuffer(BlendShapeData shapes, int keyframeIdx, List<Operations.vVertex> vertexList, int firstVertexIndex)
		{
			int vertBufferSize = vertexList.Count * Marshal.SizeOf(typeof(TweeningWithoutNormalsVertexBufferFormat.Stream0));
			VertexBuffer vertBuffer = new VertexBuffer(device, vertBufferSize, Usage.WriteOnly, VertexFormat.Position, Pool.Managed);
			Vector3[] positions = new Vector3[vertexList.Count];
			for (int i = 0; i < positions.Length; i++)
			{
				positions[i] = vertexList[i].position;
			}
			List<BlendShapeVertex> blendVerts = shapes.vertices;
			int nextShapeVertIdx = (int)(shapes.shapes[keyframeIdx].firstVertex + shapes.shapes[keyframeIdx].vertexCount);
			for (int i = (int)shapes.shapes[keyframeIdx].firstVertex; i < nextShapeVertIdx; i++)
			{
				int morphVertIdx = (int)blendVerts[i].index - firstVertexIndex;
				if (morphVertIdx >= 0 && morphVertIdx < vertexList.Count)
				{
					positions[morphVertIdx] += blendVerts[i].vertex;
				}
			}

			using (DataStream vertexStream = vertBuffer.Lock(0, vertBufferSize, LockFlags.None))
			{
				for (int i = 0; i < positions.Length; i++)
				{
					Vector3 pos = positions[i];
					vertexStream.Write(-pos.X);
					vertexStream.Write(pos.Y);
					vertexStream.Write(pos.Z);
				}
				vertBuffer.Unlock();
			}

			return vertBuffer;
		}

		public float UnsetMorphKeyframe(SkinnedMeshRenderer sMesh, bool asStart)
		{
			foreach (AnimationFrame frame in meshFrames)
			{
				Transform meshTransform = sMesh.m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
				if (frame.Name == meshTransform.GetTransformPath())
				{
					MorphMeshContainer morphMesh = frame.MeshContainer as MorphMeshContainer;
					float tweenFactor = 0;
					for (int meshObjIdx = 0; morphMesh != null; meshObjIdx++)
					{
						if (asStart)
						{
							if (morphMesh.StartBuffer != morphMesh.EndBuffer)
							{
								morphMesh.StartBuffer.Dispose();
								morphMesh.StartBuffer = morphMesh.EndBuffer;
							}
							else
							{
								frame.MeshContainer = morphMesh.NextMeshContainer;
							}
							morphMesh.TweenFactor = 1.0f;
						}
						else
						{
							if (morphMesh.StartBuffer != morphMesh.EndBuffer)
							{
								morphMesh.EndBuffer.Dispose();
								morphMesh.EndBuffer = morphMesh.StartBuffer;
							}
							else
							{
								frame.MeshContainer = morphMesh.NextMeshContainer;
							}
							morphMesh.TweenFactor = 0.0f;
						}
						tweenFactor = morphMesh.TweenFactor;
						morphMesh = morphMesh.NextMeshContainer as MorphMeshContainer;
					}
					return tweenFactor;
				}
			}
			Report.ReportLog("Mesh frame " + sMesh.m_GameObject.instance.m_Name + " not displayed.");
			return -1f;
		}

		public void SetTweenFactor(SkinnedMeshRenderer sMesh, float tweenFactor)
		{
			foreach (AnimationFrame frame in meshFrames)
			{
				Transform meshTransform = sMesh.m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
				if (frame.Name == meshTransform.GetTransformPath())
				{
					MorphMeshContainer morphMesh = frame.MeshContainer as MorphMeshContainer;
					for (int meshObjIdx = 0; morphMesh != null; meshObjIdx++)
					{
						morphMesh.TweenFactor = tweenFactor;
						morphMesh = morphMesh.NextMeshContainer as MorphMeshContainer;
					}
					return;
				}
			}
			Report.ReportLog("Mesh frame " + sMesh.m_GameObject.instance.m_Name + " not displayed.");
		}
	}

	public static class TweeningWithoutNormalsVertexBufferFormat
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct Stream0
		{
			public Vector3 Position;
		}
		[StructLayout(LayoutKind.Sequential)]
		public struct Stream1
		{
			public Vector3 Position;
		}
		[StructLayout(LayoutKind.Sequential)]
		public struct Stream2
		{
			public float U, V;
		}

		public static readonly VertexElement[] ThreeStreams = new[] {
			new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
			new VertexElement(1, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 1),
			new VertexElement(2, 0, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
			VertexElement.VertexDeclarationEnd
		};
	}
}