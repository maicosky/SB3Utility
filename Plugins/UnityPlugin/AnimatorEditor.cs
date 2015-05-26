using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	[Plugin]
	public class AnimatorEditor : IDisposable, EditedContent
	{
		public List<Transform> Frames { get; protected set; }
		public List<MeshRenderer> Meshes { get; set; }
		public List<Material> Materials { get; set; }
		public List<Texture2D> Textures { get; set; }

		public Animator Parser { get; protected set; }

		protected bool contentChanged = false;

		public AnimatorEditor(Animator parser)
		{
			Parser = parser;

			Frames = new List<Transform>();
			Meshes = new List<MeshRenderer>();
			Materials = new List<Material>();
			Textures = new List<Texture2D>();
			try
			{
				parser.file.BeginLoadingSkippedComponents();
				if (Parser.m_Avatar.asset == null)
				{
					string animatorName = Parser.m_GameObject.instance.m_Name;
					if (animatorName.Length > 2 && animatorName[1] == '_')
					{
						animatorName = animatorName.Substring(2);
					}
					foreach (Component asset in Parser.file.Components)
					{
						if (asset.classID1 == UnityClassID.Avatar)
						{
							string assetName;
							if (asset is NotLoaded)
							{
								if (((NotLoaded)asset).Name != null)
								{
									assetName = ((NotLoaded)asset).Name;
								}
								else
								{
									Parser.file.SourceStream.Position = ((NotLoaded)asset).offset;
									assetName = Avatar.LoadName(Parser.file.SourceStream);
									((NotLoaded)asset).Name = assetName;
								}
							}
							else
							{
								assetName = ((Avatar)asset).m_Name;
							}
							if (assetName.Contains(animatorName))
							{
								Parser.m_Avatar = new PPtr<Avatar>(asset);
								Report.ReportLog("Warning! Using Avatar " + assetName + " for Animator " + Parser.m_GameObject.instance.m_Name);
								break;
							}
						}
					}
				}
				if (Parser.m_Avatar.asset is NotLoaded)
				{
					Avatar loadedAvatar = Parser.file.LoadComponent(Parser.file.SourceStream, (NotLoaded)Parser.m_Avatar.asset);
					Parser.m_Avatar = new PPtr<Avatar>(loadedAvatar);
				}
				if (Parser.m_Avatar.instance == null)
				{
					Report.ReportLog("Warning! Animator " + Parser.m_GameObject.instance.m_Name + " has no Avatar!");
				}
				InitLists(parser.RootTransform);
			}
			finally
			{
				parser.file.EndLoadingSkippedComponents();
			}
		}

		private void InitLists()
		{
			HashSet<Transform> framesBefore = new HashSet<Transform>(Frames);
			Frames.Clear();
			Meshes.Clear();
			Materials.Clear();
			Textures.Clear();
			try
			{
				Parser.file.BeginLoadingSkippedComponents();
				InitLists(Parser.RootTransform);
			}
			finally
			{
				Parser.file.EndLoadingSkippedComponents();
			}

			HashSet<Mesh> meshesForRemoval = new HashSet<Mesh>();
			foreach (Transform trans in framesBefore)
			{
				if (!Frames.Contains(trans))
				{
					foreach (MeshRenderer meshR in Meshes)
					{
						if (!(meshR is SkinnedMeshRenderer))
						{
							continue;
						}
						SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshR;
						for (int i = 0; i < sMesh.m_Bones.Count; i++)
						{
							PPtr<Transform> bonePtr = sMesh.m_Bones[i];
							if (bonePtr.instance == trans)
							{
								string boneName = bonePtr.instance.m_GameObject.instance.m_Name;
								/*Transform newBone = Frames.Find
								(
									delegate(Transform t)
									{
										return t.m_GameObject.instance.m_Name == boneName;
									}
								);
								if (newBone == null)
								{*/
									Report.ReportLog("Bone " + boneName + " in SMR " + sMesh.m_GameObject.instance.m_Name + " lost");
									sMesh.m_Bones[i] = new PPtr<Transform>((Component)null);
								/*}
								else
								{
									sMesh.m_Bones[i] = new PPtr<Transform>(newBone);
								}*/
								break;
							}
						}
					}

					GameObject gameObj = trans.m_GameObject.instance;
					MeshRenderer frameMeshR = gameObj.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
					if (frameMeshR == null)
					{
						frameMeshR = gameObj.FindLinkedComponent(UnityClassID.MeshRenderer);
					}
					if (frameMeshR == null)
					{
						frameMeshR = gameObj.FindLinkedComponent(UnityClassID.ParticleRenderer);
					}
					if (frameMeshR != null)
					{
						Mesh mesh = Operations.GetMesh(frameMeshR);
						meshesForRemoval.Add(mesh);
						if (Parser.file.Bundle != null)
						{
							Parser.file.Bundle.DeleteComponent(frameMeshR);
						}
						Parser.file.RemoveSubfile(frameMeshR);
						frameMeshR.m_GameObject.instance.RemoveLinkedComponent(frameMeshR);
					}
					if (Parser.file.Bundle != null)
					{
						Parser.file.Bundle.DeleteComponent(trans);
					}
					Parser.file.RemoveSubfile(trans);
					gameObj.RemoveLinkedComponent(trans);
					if (gameObj.m_Component.Count == 0 && Parser.file.Bundle != null)
					{
						Parser.file.Bundle.DeleteComponent(gameObj);
					}
				}
			}
			RemoveUnlinkedMeshes(meshesForRemoval);
		}

		private void RemoveUnlinkedMeshes(HashSet<Mesh> meshesForRemoval)
		{
			foreach (MeshRenderer meshR in Meshes)
			{
				Mesh mesh = Operations.GetMesh(meshR);
				if (meshesForRemoval.Contains(mesh))
				{
					meshesForRemoval.Remove(mesh);
				}
			}
			foreach (Mesh mesh in meshesForRemoval)
			{
				Parser.file.RemoveSubfile(mesh);
				if (Parser.file.Bundle != null)
				{
					Parser.file.Bundle.DeleteComponent(mesh);
				}
			}
		}

		private void InitLists(Transform frame)
		{
			Frames.Add(frame);
			MeshRenderer meshR = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
			if (meshR == null)
			{
				meshR = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshRenderer);
				if (meshR == null)
				{
					meshR = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.ParticleRenderer);
				}
			}
			if (meshR != null)
			{
				Meshes.Add(meshR);
				if (meshR is SkinnedMeshRenderer)
				{
					SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshR;
					if (sMesh.m_Mesh.asset is NotLoaded)
					{
						Mesh loadedMesh = Parser.file.LoadComponent(Parser.file.SourceStream, (NotLoaded)sMesh.m_Mesh.asset);
						sMesh.m_Mesh = new PPtr<Mesh>(loadedMesh);
					}
				}
				else
				{
					MeshFilter filter = meshR.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshFilter);
					if (filter != null && filter.m_Mesh.asset is NotLoaded)
					{
						Mesh loadedMesh = Parser.file.LoadComponent(Parser.file.SourceStream, (NotLoaded)filter.m_Mesh.asset);
						filter.m_Mesh = new PPtr<Mesh>(loadedMesh);
					}
				}
				for (int i = 0; i < meshR.m_Materials.Count; i++)
				{
					Material mat;
					if (meshR.m_Materials[i].asset is NotLoaded)
					{
						if (((NotLoaded)meshR.m_Materials[i].asset).replacement != null)
						{
							mat = (Material)((NotLoaded)meshR.m_Materials[i].asset).replacement;
						}
						else
						{
							mat = Parser.file.LoadComponent(Parser.file.SourceStream, (NotLoaded)meshR.m_Materials[i].asset);
							meshR.m_Materials[i] = new PPtr<Material>(mat);
						}
					}
					else
					{
						mat = meshR.m_Materials[i].instance;
					}
					if (mat != null && !Materials.Contains(mat))
					{
						AddMaterialToEditor(mat);
					}
				}
			}

			foreach (Transform child in frame)
			{
				InitLists(child);
			}
		}

		private void AddMaterialToEditor(Material mat)
		{
			Materials.Add(mat);
			if (mat.m_Shader.asset is NotLoaded)
			{
				Shader shader = (Shader)((NotLoaded)mat.m_Shader.asset).replacement;
				if (shader == null)
				{
					shader = Parser.file.LoadComponent(Parser.file.SourceStream, (NotLoaded)mat.m_Shader.asset);
				}
				mat.m_Shader = new PPtr<Shader>(shader);
			}
			foreach (var pair in mat.m_SavedProperties.m_TexEnvs)
			{
				if (pair.Value.m_Texture.asset is NotLoaded)
				{
					Texture2D loadedTex = (Texture2D)((NotLoaded)pair.Value.m_Texture.asset).replacement;
					if (loadedTex == null)
					{
						loadedTex = Parser.file.LoadComponent(Parser.file.SourceStream, (NotLoaded)pair.Value.m_Texture.asset);
					}
					pair.Value.m_Texture = new PPtr<Texture2D>(loadedTex);
				}
				Texture2D tex = pair.Value.m_Texture.instance;
				if (tex != null && !Textures.Contains(tex))
				{
					Textures.Add(tex);
				}
			}
		}

		public void Dispose()
		{
			Parser = null;
		}

		public bool Changed
		{
			get { return contentChanged; }
			set { contentChanged = value; }
		}

		[Plugin]
		public int GetTransformId(string name)
		{
			for (int i = 0; i < Frames.Count; i++)
			{
				if (Frames[i].m_GameObject.instance.m_Name == name)
				{
					return i;
				}
			}

			return -1;
		}

		[Plugin]
		public int GetMeshRendererId(string name)
		{
			for (int i = 0; i < Meshes.Count; i++)
			{
				if (Meshes[i].m_GameObject.instance.m_Name == name)
				{
					return i;
				}
			}

			return -1;
		}

		[Plugin]
		public int GetMaterialId(string name)
		{
			for (int i = 0; i < Materials.Count; i++)
			{
				if (Materials[i].m_Name == name)
				{
					return i;
				}
			}

			return -1;
		}

		[Plugin]
		public int GetTextureId(string name)
		{
			for (int i = 0; i < Textures.Count; i++)
			{
				if (Textures[i].m_Name == name)
				{
					return i;
				}
			}

			return -1;
		}

		[Plugin]
		public void SetFrameName(int id, string name)
		{
			string oldName = Frames[id].m_GameObject.instance.m_Name;
			Frames[id].m_GameObject.instance.m_Name = name;

			if (id == 0)
			{
				if (Parser.file.Bundle != null)
				{
					Parser.file.Bundle.RegisterForUpdate(Parser.m_GameObject.asset);
				}
			}
			else
			{
				SortedDictionary<uint, uint> boneHashTranslation = Parser.m_Avatar.instance.RenameBone(oldName, name);
				foreach (MeshRenderer meshR in Meshes)
				{
					if (!(meshR is SkinnedMeshRenderer))
					{
						continue;
					}

					SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshR;
					Mesh mesh = sMesh.m_Mesh.instance;
					if (mesh == null)
					{
						continue;
					}

					for (int i = 0; i < mesh.m_BoneNameHashes.Count; i++)
					{
						uint newHash;
						if (boneHashTranslation.TryGetValue(mesh.m_BoneNameHashes[i], out newHash))
						{
							mesh.m_BoneNameHashes[i] = newHash;
						}
						if (sMesh.m_Bones[i].instance == null)
						{
							sMesh.m_Bones[i] = new PPtr<Transform>(FindTransform(mesh.m_BoneNameHashes[i]));
						}
					}

					uint newRootHash;
					if (boneHashTranslation.TryGetValue(mesh.m_RootBoneNameHash, out newRootHash))
					{
						mesh.m_RootBoneNameHash = newRootHash;
					}
					if (sMesh.m_RootBone == null && mesh.m_RootBoneNameHash != 0)
					{
						sMesh.m_RootBone = new PPtr<Transform>(FindTransform(mesh.m_RootBoneNameHash));
					}
				}
			}

			Changed = true;
		}

		[Plugin]
		public void LoadAndSetAvatar(int componentIndex)
		{
			Component asset = componentIndex >= 0 ? Parser.file.Components[componentIndex] : null;
			if (asset is NotLoaded)
			{
				asset = Parser.file.LoadComponent(asset.pathID);
			}
			Parser.m_Avatar = new PPtr<Avatar>(asset);

			if (Parser.m_Controller != null)
			{
				Changed = true;
			}
		}

		[Plugin]
		public void MoveFrame(int id, int parent, int index)
		{
			var srcFrame = Frames[id];
			var srcParent = (Transform)srcFrame.Parent;
			var destParent = Frames[parent];
			srcParent.RemoveChild(srcFrame);
			destParent.InsertChild(index, srcFrame);

			Changed = true;
		}

		[Plugin]
		public void RemoveFrame(int id)
		{
			var frame = Frames[id];
			var parent = (Transform)frame.Parent;
			if (parent == null)
			{
				throw new Exception("The root transform can't be removed");
			}

			parent.RemoveChild(frame);
			if (Parser.m_Avatar.instance != null)
			{
				Parser.m_Avatar.instance.RemoveBone(frame.m_GameObject.instance.m_Name);
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Parser.m_GameObject.asset);
			}
			InitLists();
			Changed = true;
		}

		[Plugin]
		public void SetFrameSRT(int id, double sX, double sY, double sZ, double rX, double rY, double rZ, double tX, double tY, double tZ)
		{
			Frames[id].m_LocalRotation = FbxUtility.EulerToQuaternion(new Vector3((float)rX, (float)rY, (float)rZ));
			Frames[id].m_LocalPosition = new Vector3((float)tX, (float)tY, (float)tZ);
			Frames[id].m_LocalScale = new Vector3((float)sX, (float)sY, (float)sZ);
			Changed = true;
		}

		[Plugin]
		public void SetFrameMatrix(int id,
			double m11, double m12, double m13, double m14,
			double m21, double m22, double m23, double m24,
			double m31, double m32, double m33, double m34,
			double m41, double m42, double m43, double m44)
		{
			Transform frame = Frames[id];
			Matrix m = new Matrix();

			m.M11 = (float)m11;
			m.M12 = (float)m12;
			m.M13 = (float)m13;
			m.M14 = (float)m14;

			m.M21 = (float)m21;
			m.M22 = (float)m22;
			m.M23 = (float)m23;
			m.M24 = (float)m24;

			m.M31 = (float)m31;
			m.M32 = (float)m32;
			m.M33 = (float)m33;
			m.M34 = (float)m34;

			m.M41 = (float)m41;
			m.M42 = (float)m42;
			m.M43 = (float)m43;
			m.M44 = (float)m44;

			Vector3 s, t;
			Quaternion q;
			m.Decompose(out s, out q, out t);
			frame.m_LocalPosition = t;
			frame.m_LocalRotation = q;
			frame.m_LocalScale = s;
			Changed = true;
		}

		[Plugin]
		public void AddFrame(ImportedFrame srcFrame, int destParentId)
		{
			Transform destParent = destParentId >= 0 ? Frames[destParentId] : Parser.RootTransform;
			Transform newFrame = Operations.CreateTransformTree(Parser, srcFrame, destParent);
			Operations.CopyOrCreateUnknowns(newFrame, Parser.RootTransform);

			AddFrame(newFrame, destParentId);
		}

		public void AddFrame(Transform newFrame, int destParentId)
		{
			if (destParentId < 0)
			{
				//Parser.RootTransform = newFrame;
			}
			else
			{
				Frames[destParentId].AddChild(newFrame);
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Parser.m_GameObject.asset);
			}
			InitLists();
			Changed = true;
		}

		[Plugin]
		public void AddFrame(Transform srcFrame, List<Material> srcMaterials, List<Texture2D> srcTextures, bool appendIfMissing, int destParentId)
		{
			Transform destParent = destParentId >= 0 ? Frames[destParentId] : Parser.RootTransform;
			Transform newFrame = Operations.CloneTransformTree(Parser, srcFrame, destParent);
			AddFrame(newFrame, destParentId);
		}

		[Plugin]
		public void MergeFrame(ImportedFrame srcFrame, int destParentId)
		{
			Transform destParent = destParentId >= 0 ? Frames[destParentId] : Parser.RootTransform;
			Transform newFrame = Operations.CreateTransformTree(Parser, srcFrame, destParent);
			Operations.CopyOrCreateUnknowns(newFrame, Parser.RootTransform);

			MergeFrame(newFrame, destParentId);
		}

		[Plugin]
		public void MergeFrame(Transform srcFrame, int destParentId)
		{
			Transform srcParent = new Transform(srcFrame.file);
			GameObject srcGameObj = new GameObject(srcFrame.file);
			srcGameObj.AddLinkedComponent(srcParent);
			srcParent.InitChildren(1);
			srcParent.AddChild(srcFrame);

			Transform destParent;
			if (destParentId < 0)
			{
				destParent = new Transform(srcFrame.file);
				GameObject destGameObj = new GameObject(srcFrame.file);
				destGameObj.AddLinkedComponent(destParent);
				destParent.InitChildren(1);
				destParent.AddChild(Parser.RootTransform);
				Parser.m_GameObject.instance.RemoveLinkedComponent(Parser);
			}
			else
			{
				destParent = Frames[destParentId];
			}

			MergeFrame(srcParent, destParent);

			if (destParentId < 0)
			{
				//Parser.RootTransform = srcParent[0];
				srcParent[0].m_GameObject.instance.AddLinkedComponent(Parser);
				srcParent.RemoveChild(0);
				destParent.m_GameObject.instance.RemoveLinkedComponent(destParent);
				srcFrame.file.RemoveSubfile(destParent.m_GameObject.instance);
				srcFrame.file.RemoveSubfile(destParent);
			}

			srcGameObj.RemoveLinkedComponent(srcParent);
			srcFrame.file.RemoveSubfile(srcGameObj);
			srcFrame.file.RemoveSubfile(srcParent);

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Parser.m_GameObject.asset);
			}
			InitLists();
			Changed = true;
		}

		[Plugin]
		public void MergeFrame(Transform srcFrame, List<Material> srcMaterials, List<Texture2D> srcTextures, bool appendIfMissing, int destParentId)
		{
			Transform destParent = destParentId >= 0 ? Frames[destParentId] : Parser.RootTransform;
			Transform newFrame = Operations.CloneTransformTree(Parser, srcFrame, destParent);
			MergeFrame(newFrame, destParentId);
		}

		void MergeFrame(Transform srcParent, Transform destParent)
		{
			for (int i = 0; i < destParent.Count; i++)
			{
				var dest = destParent[i];
				for (int j = 0; j < srcParent.Count; j++)
				{
					var src = srcParent[j];
					if (src.m_GameObject.instance.m_Name == dest.m_GameObject.instance.m_Name)
					{
						MergeFrame(src, dest);

						srcParent.RemoveChild(j);
						destParent.RemoveChild(i);
						destParent.InsertChild(i, src);
						break;
					}
				}
			}

			if (srcParent.m_GameObject.instance.m_Name == destParent.m_GameObject.instance.m_Name)
			{
				while (destParent.Count > 0)
				{
					var dest = destParent[0];
					destParent.RemoveChild(0);
					srcParent.AddChild(dest);
				}
			}
			else
			{
				while (srcParent.Count > 0)
				{
					var src = srcParent[0];
					srcParent.RemoveChild(0);
					destParent.AddChild(src);
				}
			}
		}

		[Plugin]
		public void ReplaceFrame(ImportedFrame srcFrame, int destParentId)
		{
			throw new NotImplementedException();
		}

		[Plugin]
		public void ReplaceFrame(Transform srcFrame, int destParentId)
		{
			throw new NotImplementedException();
		}

		[Plugin]
		public void ReplaceFrame(Transform srcFrame, List<Material> srcMaterials, List<Texture2D> srcTextures, bool appendIfMissing, int destParentId)
		{
			throw new NotImplementedException();
		}

		[Plugin]
		public void UniqueFrame(int id)
		{
			UniqueFrame(Frames[id]);
		}

		private void UniqueFrame(Transform frame)
		{
			for (int i = 0; i < frame.Count; i++)
			{
				UniqueFrame(frame[i]);
			}

			int attempt = 1;
			for (int i = 0; i < Frames.Count; i++)
			{
				Transform t = Frames[i];
				if (t != frame && t.m_GameObject.instance.m_Name == frame.m_GameObject.instance.m_Name)
				{
					++attempt;
				}
			}
			if (attempt > 1)
			{
				string framePath = GetTransformPath(frame);

				frame.m_GameObject.instance.m_Name += attempt;

				int index = Parser.m_Avatar.instance.m_TOS.FindLastIndex
				(
					delegate(KeyValuePair<uint, string> data)
					{
						return data.Value == framePath;
					}
				);
				if (index >= 0)
				{
					string newName = framePath + attempt;
					var pair = Parser.m_Avatar.instance.m_TOS[index];
					Parser.m_Avatar.instance.m_TOS.RemoveAt(index);
					Report.ReportLog("renaming " + framePath + " to " + newName);
					if (frame.Count > 0)
					{
						for (int i = 0; i < Parser.m_Avatar.instance.m_TOS.Count; i++)
						{
							KeyValuePair<uint, string> data = Parser.m_Avatar.instance.m_TOS[i];
							if (data.Value.StartsWith(framePath + "/"))
							{
								Parser.m_Avatar.instance.m_TOS.RemoveAt(i);
								Parser.m_Avatar.instance.m_TOS.Insert(i, new KeyValuePair<uint, string>(data.Key, newName + data.Value.Substring(framePath.Length)));
								Report.ReportLog("   child " + data.Value + " to " + Parser.m_Avatar.instance.m_TOS[i].Value);
							}
						}
					}
					Parser.m_Avatar.instance.m_TOS.Insert(index, new KeyValuePair<uint, string>(pair.Key, newName));
				}
				else
				{
					Report.ReportLog("adding " + framePath);
					Parser.m_Avatar.instance.AddBone(frame.Parent, frame);
				}
			}
		}

		public string GetTransformPath(Transform trans)
		{
			return (trans.Parent != null && trans.Parent.Parent != null ? GetTransformPath(trans.Parent) + "/" : String.Empty) + trans.m_GameObject.instance.m_Name;
		}

		[Plugin]
		public void AddBone(int id, object[] meshes)
		{
			Matrix boneMatrix = Matrix.Transpose(Matrix.Invert(Transform.WorldTransform(Frames[id])));
			uint boneHash = Parser.m_Avatar.instance.BoneHash(Frames[id].m_GameObject.instance.m_Name);
			string[] meshFrameNames = Utility.Convert<string>(meshes);
			List<MeshRenderer> meshList = Operations.FindMeshes(Parser.RootTransform, new HashSet<string>(meshFrameNames));
			foreach (MeshRenderer meshR in meshList)
			{
				if (meshR is SkinnedMeshRenderer)
				{
					SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshR;
					if (Operations.FindBoneIndex(sMesh.m_Bones, Frames[id]) < 0)
					{
						sMesh.m_Bones.Add(new PPtr<Transform>(Frames[id]));
						Mesh mesh = sMesh.m_Mesh.instance;
						if (mesh != null)
						{
							mesh.m_BoneNameHashes.Add(boneHash);
							mesh.m_BindPose.Add(boneMatrix);
						}
					}
				}
			}
			Changed = true;
		}

		[Plugin]
		public void RemoveBone(int meshId, int boneId)
		{
			SkinnedMeshRenderer smr = (SkinnedMeshRenderer)Meshes[meshId];
			Mesh mesh = smr.m_Mesh.instance;
			if (mesh != null)
			{
				Operations.vMesh vMesh = new Operations.vMesh(smr, false);
				Transform boneFrame = smr.m_Bones[boneId].instance;
				int parentBoneIdx = -1;
				if (boneFrame != null)
				{
					Transform parentFrame = boneFrame.Parent;
					parentBoneIdx = Operations.FindBoneIndex(smr.m_Bones, parentFrame);
				}
				smr.m_Bones.RemoveAt(boneId);
				mesh.m_BindPose.RemoveAt(boneId);
				mesh.m_BoneNameHashes.RemoveAt(boneId);

				foreach (Operations.vSubmesh submesh in vMesh.submeshes)
				{
					foreach (Operations.vVertex vertex in submesh.vertexList)
					{
						for (int i = 0; i < vertex.boneIndices.Length; i++)
						{
							int boneIdx = vertex.boneIndices[i];
							if (boneIdx == boneId)
							{
								float[] w4 = vertex.weights;
								for (int j = i + 1; j < vertex.boneIndices.Length; j++)
								{
									vertex.boneIndices[j - 1] = vertex.boneIndices[j];
									vertex.weights[j - 1] = w4[j];
								}
								vertex.boneIndices[vertex.boneIndices.Length - 1] = -1;

								w4 = vertex.weights;
								float normalize = 1f / (w4[0] + w4[1] + w4[2] + w4[3]);
								if (w4[3] != 1f)
								{
									for (int j = 0; vertex.boneIndices[j] != -1; j++)
									{
										vertex.weights[j] *= normalize;
									}
								}
								else if (parentBoneIdx >= 0)
								{
									vertex.boneIndices[0] = parentBoneIdx;
									vertex.weights[0] = 1f;
								}

								i--;
							}
							else if (boneIdx != -1 && boneIdx > boneId)
							{
								vertex.boneIndices[i]--;
							}
						}
					}
				}
				vMesh.Flush();
			}
		}

		[Plugin]
		public void SetBoneSRT(int meshId, int boneId, double sX, double sY, double sZ, double rX, double rY, double rZ, double tX, double tY, double tZ)
		{
			SkinnedMeshRenderer smr = (SkinnedMeshRenderer)Meshes[meshId];
			Matrix boneMatrix = FbxUtility.SRTToMatrix(new Vector3((float)sX, (float)sY, (float)sZ), new Vector3((float)rX, (float)rY, (float)rZ), new Vector3((float)tX, (float)tY, (float)tZ));
			smr.m_Mesh.instance.m_BindPose[boneId] = Matrix.Transpose(boneMatrix);
			Changed = true;
		}

		[Plugin]
		public void SetBoneMatrix(int meshId, int boneId,
			double m11, double m12, double m13, double m14,
			double m21, double m22, double m23, double m24,
			double m31, double m32, double m33, double m34,
			double m41, double m42, double m43, double m44)
		{
			SkinnedMeshRenderer smr = (SkinnedMeshRenderer)Meshes[meshId];
			Matrix m = new Matrix();

			m.M11 = (float)m11;
			m.M12 = (float)m12;
			m.M13 = (float)m13;
			m.M14 = (float)m14;

			m.M21 = (float)m21;
			m.M22 = (float)m22;
			m.M23 = (float)m23;
			m.M24 = (float)m24;

			m.M31 = (float)m31;
			m.M32 = (float)m32;
			m.M33 = (float)m33;
			m.M34 = (float)m34;

			m.M41 = (float)m41;
			m.M42 = (float)m42;
			m.M43 = (float)m43;
			m.M44 = (float)m44;

			smr.m_Mesh.instance.m_BindPose[boneId] = Matrix.Transpose(m);
			Changed = true;
		}

		[Plugin]
		public void ReplaceMeshRenderer(WorkspaceMesh mesh, int frameId, int rootBoneId, bool merge, string normals, string bones, bool targetFullMesh)
		{
			var normalsMethod = (CopyMeshMethod)Enum.Parse(typeof(CopyMeshMethod), normals);
			var bonesMethod = (CopyMeshMethod)Enum.Parse(typeof(CopyMeshMethod), bones);
			Transform rootBone = rootBoneId >= 0 && rootBoneId < Frames.Count ? Frames[rootBoneId] : null;
			Operations.ReplaceMeshRenderer(Frames[frameId], rootBone, Parser, Materials, mesh, merge, normalsMethod, bonesMethod, targetFullMesh);

			InitLists();
			Changed = true;
		}

		[Plugin]
		public void AddRendererMaterial(int meshId, int materialId)
		{
			MeshRenderer meshR = Meshes[meshId];
			meshR.m_Materials.Add(new PPtr<Material>(materialId >= 0 ? Materials[materialId] : (Component)null));
		}

		[Plugin]
		public void RemoveRendererMaterial(int meshId)
		{
			MeshRenderer meshR = Meshes[meshId];
			meshR.m_Materials.RemoveAt(meshR.m_Materials.Count - 1);

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Parser.m_GameObject.asset);
			}
			Changed = true;
		}

		[Plugin]
		public void RemoveMeshRenderer(int id)
		{
			MeshRenderer meshR = Meshes[id];
			Mesh mesh = Operations.GetMesh(meshR);
			if (mesh != null)
			{
				Parser.file.RemoveSubfile(mesh);
				if (Parser.file.Bundle != null)
				{
					Parser.file.Bundle.DeleteComponent(mesh);
				}
			}
			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.DeleteComponent(meshR);
			}
			meshR.m_GameObject.instance.RemoveLinkedComponent(meshR);
			Parser.file.RemoveSubfile(meshR);

			InitLists();
			Changed = true;
		}

		[Plugin]
		public void CalculateNormals(int id, double threshold)
		{
			MeshRenderer meshR = Meshes[id];
			Operations.vMesh vMesh = new Operations.vMesh(meshR, true);
			Operations.CalculateNormals(vMesh.submeshes, (float)threshold);
			Changed = true;
		}

		[Plugin]
		public void CalculateNormals(object[] editors, object[] numMeshes, object[] meshes, double threshold)
		{
			if (editors == null || numMeshes == null || meshes == null)
			{
				return;
			}

			List<Operations.vMesh> meshList = new List<Operations.vMesh>(meshes.Length);
			List<Operations.vSubmesh> submeshList = new List<Operations.vSubmesh>(meshes.Length);
			ConvertMeshArgs(editors, numMeshes, meshes, meshList, submeshList);
			Operations.CalculateNormals(submeshList, (float)threshold);
			foreach (Operations.vMesh mesh in meshList)
			{
				mesh.Flush();
			}
			Changed = true;
		}

		[Plugin]
		public void CalculateTangents(int id)
		{
			MeshRenderer meshR = Meshes[id];
			Operations.vMesh vMesh = new Operations.vMesh(meshR, true);
			Operations.CalculateTangents(vMesh.submeshes);
			Changed = true;
		}

		[Plugin]
		public void CalculateTangents(object[] editors, object[] numMeshes, object[] meshes)
		{
			if (editors == null || numMeshes == null || meshes == null)
			{
				return;
			}

			List<Operations.vMesh> meshList = new List<Operations.vMesh>(meshes.Length);
			List<Operations.vSubmesh> submeshList = new List<Operations.vSubmesh>(meshes.Length);
			ConvertMeshArgs(editors, numMeshes, meshes, meshList, submeshList);
			Operations.CalculateTangents(submeshList);
			foreach (Operations.vMesh mesh in meshList)
			{
				mesh.Flush();
			}
			Changed = true;
		}

		private static void ConvertMeshArgs(object[] editors, object[] numMeshes, object[] meshes, List<Operations.vMesh> meshList, List<Operations.vSubmesh> submeshList)
		{
			AnimatorEditor editor = null;
			int editorIdx = -1;
			int i = 1;
			foreach (object id in meshes)
			{
				if (--i == 0)
				{
					editorIdx++;
					i = (int)(double)numMeshes[editorIdx];
					editor = (AnimatorEditor)editors[editorIdx];
				}
				MeshRenderer meshR = editor.Meshes[(int)(double)id];
				Operations.vMesh vMesh = new Operations.vMesh(meshR, true);
				meshList.Add(vMesh);
				submeshList.AddRange(vMesh.submeshes);
			}
		}

		[Plugin]
		public void LoadAndSetMesh(int meshId, int componentIndex)
		{
			MeshRenderer meshRenderer = Meshes[meshId];
			Component asset = componentIndex >= 0 ? Parser.file.Components[componentIndex] : null;
			Mesh mesh = asset is NotLoaded ? Parser.file.LoadComponent(asset.pathID) : asset;
			if (meshRenderer is SkinnedMeshRenderer)
			{
				SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshRenderer;
				sMesh.m_Mesh = new PPtr<Mesh>(mesh);
				Transform frame = mesh != null ? FindTransform(mesh.m_RootBoneNameHash) : null;
				sMesh.m_RootBone = new PPtr<Transform>(frame);
				if (frame == null && mesh != null && mesh.m_RootBoneNameHash != 0)
				{
					Report.ReportLog("Warning: Transform for RootBone not found. hash=" + mesh.m_RootBoneNameHash + " name(Avatar)=" + Parser.m_Avatar.instance.FindBoneName(mesh.m_RootBoneNameHash));
				}
				sMesh.m_Bones.Clear();
				if (mesh != null)
				{
					for (int i = 0; i < mesh.m_BoneNameHashes.Count; i++)
					{
						frame = FindTransform(mesh.m_BoneNameHashes[i]);
						sMesh.m_Bones.Add(new PPtr<Transform>(frame));
						if (frame == null)
						{
							Report.ReportLog("Warning: Transform for bone[" + i + "] not found. hash=" + mesh.m_BoneNameHashes[i] + " name(Avatar)=" + Parser.m_Avatar.instance.FindBoneName(mesh.m_BoneNameHashes[i]));
						}
					}
					if (sMesh.m_RootBone.instance != null)
					{
						Matrix rootBoneMatrix = Transform.WorldTransform(sMesh.m_RootBone.instance);
						rootBoneMatrix.Invert();
						sMesh.m_AABB.m_Center = Vector3.TransformCoordinate(mesh.m_LocalAABB.m_Center, rootBoneMatrix);
					}
					else
					{
						sMesh.m_AABB.m_Center = mesh.m_LocalAABB.m_Center;
					}
					sMesh.m_AABB.m_Extend = mesh.m_LocalAABB.m_Extend;

					if (sMesh.m_BlendShapeWeights.Count != mesh.m_Shapes.shapes.Count)
					{
						int diff = sMesh.m_BlendShapeWeights.Count - mesh.m_Shapes.shapes.Count;
						if (diff < 0)
						{
							sMesh.m_BlendShapeWeights.RemoveRange(sMesh.m_BlendShapeWeights.Count + diff, -diff);
						}
						else
						{
							sMesh.m_BlendShapeWeights.AddRange(new float[diff]);
						}
					}
				}
			}
			else
			{
				MeshFilter filter = meshRenderer.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshFilter);
				filter.m_Mesh = new PPtr<Mesh>(mesh);
			}
			int numSubmeshes = mesh != null ? mesh.m_SubMeshes.Count : 0;
			while (meshRenderer.m_Materials.Count < numSubmeshes)
			{
				AddRendererMaterial(meshId, -1);
			}
			while (meshRenderer.m_Materials.Count > numSubmeshes)
			{
				RemoveRendererMaterial(meshId);
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Parser.m_GameObject.asset);
			}
			Changed = true;
		}

		private Transform FindTransform(uint boneHash)
		{
			string frameName = Parser.m_Avatar.instance.FindBoneName(boneHash);
			int frameId = GetTransformId(frameName);
			Transform frame = frameId >= 0 ? Frames[frameId] : null;
			return frame;
		}

		[Plugin]
		public void SetSkinnedMeshRendererRootBone(int meshId, int frameId)
		{
			SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)Meshes[meshId];
			sMesh.m_RootBone = new PPtr<Transform>(frameId >= 0 ? Frames[frameId] : null);
			if (sMesh.m_Mesh != null)
			{
				sMesh.m_Mesh.instance.m_RootBoneNameHash = frameId >= 0 ? Parser.m_Avatar.instance.BoneHash(Frames[frameId].m_GameObject.instance.m_Name) : 0;
			}
			Changed = true;
		}

		[Plugin]
		public void SetSkinnedMeshRendererAttributes(int id, int quality, bool updateWhenOffScreen, bool dirtyAABB)
		{
			SkinnedMeshRenderer mesh = (SkinnedMeshRenderer)Meshes[id];
			mesh.m_Quality = quality;
			mesh.m_UpdateWhenOffScreen = updateWhenOffScreen;
			mesh.m_DirtyAABB = dirtyAABB;

			Changed = true;
		}

		[Plugin]
		public void SetRendererAttributes(int id, bool castShadows, bool receiveShadows, int lightmap, bool lightProbes, int sortingLayer, int sortingOrder)
		{
			MeshRenderer mesh = Meshes[id];
			mesh.m_CastShadows = castShadows;
			mesh.m_ReceiveShadows = receiveShadows;
			mesh.m_LightmapIndex = (byte)lightmap;
			mesh.m_UseLightProbes = lightProbes;
			mesh.m_SortingLayerID = (uint)sortingLayer;
			mesh.m_SortingOrder = (short)sortingOrder;

			Changed = true;
		}

		[Plugin]
		public void SetMeshName(int id, string name)
		{
			MeshRenderer meshRenderer = Meshes[id];
			Mesh mesh = Operations.GetMesh(meshRenderer);
			if (mesh != null)
			{
				mesh.m_Name = name;
				Changed = true;
			}
		}

		[Plugin]
		public void SetMeshBoneHash(int id, int boneId, double hash)
		{
			MeshRenderer meshRenderer = Meshes[id];
			Mesh mesh = Operations.GetMesh(meshRenderer);
			if (mesh != null && mesh.m_BoneNameHashes.Count > 0)
			{
				mesh.m_BoneNameHashes[boneId] = (uint)hash;
				Transform boneFrame = FindTransform(mesh.m_BoneNameHashes[boneId]);
				if (boneFrame != null)
				{
					((SkinnedMeshRenderer)meshRenderer).m_Bones[boneId] = new PPtr<Transform>(boneFrame);
				}
				else
				{
					Report.ReportLog("Warning! Transform could not be found by hash value!");
				}
				Changed = true;
			}
		}

		[Plugin]
		public void SetMeshAttributes(int id, bool readable, bool keepVertices, bool keepIndices, int usageFlags)
		{
			Mesh mesh = Operations.GetMesh(Meshes[id]);
			mesh.m_IsReadable = readable;
			mesh.m_KeepVertices = keepVertices;
			mesh.m_KeepIndices = keepIndices;
			mesh.m_MeshUsageFlags = usageFlags;

			Changed = true;
		}

		[Plugin]
		public void SetSubMeshMaterial(int meshId, int subMeshId, int material)
		{
			MeshRenderer meshRenderer = Meshes[meshId];
			Material mat = Materials[material];
			if (subMeshId < meshRenderer.m_Materials.Count)
			{
				meshRenderer.m_Materials[subMeshId] = new PPtr<Material>(mat);
			}
			else
			{
				while (subMeshId >= meshRenderer.m_Materials.Count)
				{
					meshRenderer.m_Materials.Add(new PPtr<Material>(mat));
				}
				Report.ReportLog("Warning! Missing Material added");
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Parser.m_GameObject.asset);
			}
			Changed = true;
		}

		[Plugin]
		public void LoadAndSetSubMeshMaterial(int meshId, int subMeshId, int componentIndex)
		{
			MeshRenderer meshRenderer = Meshes[meshId];
			Component asset = componentIndex >= 0 ? Parser.file.Components[componentIndex] : null;
			Material mat = asset is NotLoaded ? Parser.file.LoadComponent(asset.pathID) : asset;
			if (mat != null && !Materials.Contains(mat))
			{
				AddMaterialToEditor(mat);
			}

			SetSubMeshMaterial(meshId, subMeshId, Materials.IndexOf(mat));
		}

		[Plugin]
		public void SetSubMeshTopology(int meshId, int subMeshId, int topology)
		{
			MeshRenderer meshR = Meshes[meshId];
			Mesh mesh = Operations.GetMesh(meshR);
			mesh.m_SubMeshes[subMeshId].topology = topology;
			Changed = true;
		}

		[Plugin]
		public void RemoveSubMesh(int meshId, int subMeshId)
		{
			MeshRenderer meshR = Meshes[meshId];
			Mesh mesh = Operations.GetMesh(meshR);
			if (mesh.m_SubMeshes.Count == 1 && subMeshId == 0)
			{
				Operations.SetMeshPtr(meshR, null);
				Parser.file.RemoveSubfile(mesh);
				if (Parser.file.Bundle != null)
				{
					Parser.file.Bundle.DeleteComponent(mesh);
				}
			}
			else
			{
				Operations.vMesh vMesh = new Operations.vMesh(meshR, true);
				vMesh.submeshes.RemoveAt(subMeshId);
				vMesh.Flush();
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(meshR);
			}
			Changed = true;
		}

		[Plugin]
		public void ReplaceMorph(WorkspaceMorph morph, string destMorphName, bool replaceNormals, double minSquaredDistance)
		{
			Operations.ReplaceMorph(destMorphName, Parser, morph, replaceNormals, (float)minSquaredDistance);
			Changed = true;
		}

		[Plugin]
		public void RenameMorphKeyframe(int meshId, int morphIndex, string name)
		{
			SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)Meshes[meshId];
			Mesh mesh = Operations.GetMesh(sMesh);
			if (mesh != null)
			{
				MeshBlendShapeChannel channel = mesh.m_Shapes.channels[morphIndex];
				channel.name = name;
				channel.nameHash = Animator.StringToHash(channel.name);
			}
		}

		[Plugin]
		public void SetMorphKeyframeIndexCount(int meshId, int morphIndex, int index, int count)
		{
			SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)Meshes[meshId];
			Mesh mesh = Operations.GetMesh(sMesh);
			if (mesh != null)
			{
				MeshBlendShapeChannel channel = mesh.m_Shapes.channels[morphIndex];
				channel.frameIndex = index;
				channel.frameCount = count;
			}
		}

		[Plugin]
		public void SetMaterialName(int id, string name)
		{
			Materials[id].m_Name = name;

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Materials[id]);
			}
			Changed = true;
		}

		[Plugin]
		public void SetMaterialColour(int id, string name, object[] colour)
		{
			Material mat = Materials[id];
			for (int i = 0; i < mat.m_SavedProperties.m_Colors.Count; i++)
			{
				var col = mat.m_SavedProperties.m_Colors[i];
				if (col.Key.name == name)
				{
					SetMaterialColour(id, i, colour);
					return;
				}
			}
		}

		[Plugin]
		public void SetMaterialColour(int id, int index, object[] colour)
		{
			Material mat = Materials[id];
			var col = mat.m_SavedProperties.m_Colors[index];
			mat.m_SavedProperties.m_Colors.RemoveAt(index);
			col = new KeyValuePair<FastPropertyName, Color4>(col.Key, new Color4((float)(double)colour[3], (float)(double)colour[0], (float)(double)colour[1], (float)(double)colour[2]));
			mat.m_SavedProperties.m_Colors.Insert(index, col);
			Changed = true;
		}

		[Plugin]
		public void SetMaterialValue(int id, string name, double value)
		{
			Material mat = Materials[id];
			for (int i = 0; i < mat.m_SavedProperties.m_Floats.Count; i++)
			{
				var flt = mat.m_SavedProperties.m_Floats[i];
				if (flt.Key.name == name)
				{
					SetMaterialValue(id, i, value);
					return;
				}
			}
		}

		[Plugin]
		public void SetMaterialValue(int id, int index, double value)
		{
			Material mat = Materials[id];
			var flt = mat.m_SavedProperties.m_Floats[index];
			mat.m_SavedProperties.m_Floats.RemoveAt(index);
			mat.m_SavedProperties.m_Floats.Insert(index, new KeyValuePair<FastPropertyName, float>(flt.Key, (float)value));
			Changed = true;
		}

		[Plugin]
		public void SetMaterialTexture(int id, int index, string name)
		{
			Texture2D tex = Parser.file.Parser.GetTexture(name);
			var texEnv = Materials[id].m_SavedProperties.m_TexEnvs[index].Value;
			texEnv.m_Texture = new PPtr<Texture2D>(tex);
			if (!Textures.Contains(tex))
			{
				Textures.Add(tex);
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Materials[id]);
			}
			Changed = true;
		}

		[Plugin]
		public void SetMaterialTexture(int id, int index, int componentIndex)
		{
			Texture2D tex;
			if (componentIndex >= 0)
			{
				Component asset = Parser.file.Components[componentIndex];
				int parserTexIdx = Parser.file.Parser.Textures.IndexOf(asset);
				tex = Parser.file.Parser.GetTexture(parserTexIdx);
			}
			else
			{
				tex = null;
			}
			var texEnv = Materials[id].m_SavedProperties.m_TexEnvs[index].Value;
			texEnv.m_Texture = new PPtr<Texture2D>(tex);
			if (tex != null && !Textures.Contains(tex))
			{
				Textures.Add(tex);
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Materials[id]);
			}
			Changed = true;
		}

		[Plugin]
		public void RemoveMaterial(int id)
		{
			Material mat = Materials[id];
			foreach (MeshRenderer meshRenderer in Meshes)
			{
				for (int i = 0; i < meshRenderer.m_Materials.Count; i++)
				{
					if (meshRenderer.m_Materials[i].instance == mat)
					{
						meshRenderer.m_Materials[i] = new PPtr<Material>((Component)null);
					}
				}
			}
			foreach (Material m in Materials)
			{
				Shader shader = m.m_Shader.instance;
				if (shader != null)
				{
					RemoveComponentFromShaders(shader, mat);
				}
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.DeleteComponent(mat);
			}
			Parser.file.RemoveSubfile(mat);
			Materials.RemoveAt(id);
			Changed = true;
		}

		[Plugin]
		public Material CopyMaterial(int id)
		{
			Material oldMat = Materials[id];
			string oldName = oldMat.m_Name;
			oldMat.m_Name += "_Copy";
			Material newMat = oldMat.Clone(Parser.file);
			oldMat.m_Name = oldName;

			if (!Materials.Contains(newMat))
			{
				Materials.Add(newMat);
			}
			Changed = true;
			return newMat;
		}

		[Plugin]
		public void MergeMaterial(ImportedMaterial mat)
		{
			Material dest = Operations.FindMaterial(Materials, mat.Name);
			Operations.ReplaceMaterial(dest, mat);

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(dest);
			}
			Changed = true;
		}

		[Plugin]
		public void MergeMaterial(Material mat)
		{
			Material oldMat = Operations.FindMaterial(Materials, mat.m_Name);
			if (oldMat != null)
			{
				if (oldMat == mat)
				{
					return;
				}
				mat.CopyTo(oldMat, false);
				if (Parser.file.Bundle != null)
				{
					Parser.file.Bundle.RegisterForUpdate(oldMat);
				}
			}
			else
			{
				Material newMat;
				Component m = Parser.file.Bundle != null ? Parser.file.Bundle.FindComponent(mat.m_Name, UnityClassID.Material) : null;
				if (m == null)
				{
					newMat = mat.Clone(Parser.file, false);
				}
				else
				{
					if (m is NotLoaded)
					{
						NotLoaded notLoaded = (NotLoaded)m;
						if (notLoaded.replacement != null)
						{
							m = (Material)notLoaded.replacement;
						}
						else
						{
							m = Parser.file.LoadComponent(notLoaded.pathID);
						}
					}
					oldMat = (Material)m;
					mat.CopyTo(oldMat, false);
					if (Parser.file.Bundle != null)
					{
						Parser.file.Bundle.RegisterForUpdate(oldMat);
					}
					newMat = oldMat;
				}
				if (!Materials.Contains(newMat))
				{
					Materials.Add(newMat);
				}
			}

			Changed = true;
		}

		[Plugin]
		public void MergeTexture(ImportedTexture tex)
		{
			Texture2D dstTex = Parser.file.Parser.GetTexture(tex.Name);
			bool isNew = false;
			if (dstTex == null)
			{
				dstTex = new Texture2D(Parser.file);
				isNew = true;
			}
			dstTex.LoadFrom(tex);

			if (isNew)
			{
				if (Parser.file.Bundle != null)
				{
					Parser.file.Bundle.AddComponent(dstTex);
				}
				Textures.Add(dstTex);
			}
			Changed = true;
		}

		[Plugin]
		public void MergeTexture(Texture2D tex)
		{
			if (tex.file != Parser.file)
			{
				Texture2D dstTex = Parser.file.Parser.GetTexture(tex.m_Name);
				bool isNew = false;
				if (dstTex == null)
				{
					dstTex = new Texture2D(Parser.file);
					isNew = true;
				}
				tex.CopyAttributesTo(dstTex);
				tex.CopyImageTo(dstTex);

				if (isNew)
				{
					if (Parser.file.Bundle != null)
					{
						Parser.file.Bundle.AddComponent(dstTex);
					}
					Textures.Add(dstTex);
				}
				Changed = true;
			}
		}

		[Plugin]
		public void SetTextureName(int id, string name)
		{
			Textures[id].m_Name = name;

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(Textures[id]);
			}
			Changed = true;
		}

		[Plugin]
		public void SetTextureAttributes(int id, int dimension, bool mipMap, int imageCount, int colorSpace, int lightMap, int filterMode, double mipBias, int aniso, int wrapMode)
		{
			Texture2D tex = Textures[id];
			tex.m_TextureDimension = dimension;
			tex.m_MipMap = mipMap;
			tex.m_ImageCount = imageCount;
			tex.m_ColorSpace = colorSpace;
			tex.m_LightmapFormat = lightMap;
			tex.m_TextureSettings.m_FilterMode = filterMode;
			tex.m_TextureSettings.m_MipBias = (float)mipBias;
			tex.m_TextureSettings.m_Aniso = aniso;
			tex.m_TextureSettings.m_WrapMode = wrapMode;
			Changed = true;
		}

		[Plugin]
		public void ExportTexture(int id, string path)
		{
			Textures[id].Export(path);
		}

		[Plugin]
		public void RemoveTexture(int id)
		{
			Texture2D tex = Textures[id];
			foreach (Material mat in Materials)
			{
				foreach (var texEnv in mat.m_SavedProperties.m_TexEnvs)
				{
					if (texEnv.Value.m_Texture.instance == tex)
					{
						texEnv.Value.m_Texture = new PPtr<Texture2D>((Component)null);
					}
				}

				Shader shader = mat.m_Shader.instance;
				if (shader != null)
				{
					RemoveComponentFromShaders(shader, tex);
				}
			}

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.DeleteComponent(tex);
			}
			Parser.file.RemoveSubfile(tex);
			Parser.file.Parser.Textures.Remove(tex);
			Textures.RemoveAt(id);
			Changed = true;
		}

		void RemoveComponentFromShaders(Shader shader, Component asset)
		{
			for (int i = 0; i < shader.m_Dependencies.Count; i++)
			{
				var dep = shader.m_Dependencies[i];
				if (dep.instance != null)
				{
					RemoveComponentFromShaders(dep.instance, asset);
				}
				else if (dep.asset == asset)
				{
					shader.m_Dependencies.RemoveAt(i);
					i--;
				}
			}
		}

		[Plugin]
		public void AddTexture(ImportedTexture image)
		{
			Texture2D tex = new Texture2D(Parser.file);
			Textures.Add(tex);
			tex.LoadFrom(image);

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.AddComponent(tex);
			}
			Changed = true;
		}

		[Plugin]
		public void ReplaceTexture(int id, ImportedTexture image)
		{
			var oldTex = Textures[id];
			oldTex.LoadFrom(image);

			if (Parser.file.Bundle != null)
			{
				Parser.file.Bundle.RegisterForUpdate(oldTex);
			}
			Changed = true;
		}
	}
}
