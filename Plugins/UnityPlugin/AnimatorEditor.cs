using System;
using System.Collections.Generic;
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
		private AssetBundle Bundle { get; set; }

		protected bool contentChanged = false;

		public AnimatorEditor(Animator parser)
		{
			Parser = parser;
			Bundle = Parser.file.LoadComponent(1);
			/*for (int i = 0; i < Bundle.m_PreloadTable.Count; i++)
			{
				Component comp = Bundle.m_PreloadTable[i].asset;
				if (comp.classID1 != UnityClassID.GameObject && comp.classID1 != UnityClassID.Transform)
				{
					Report.ReportLog(i + " " + comp.pathID + " " + comp.classID1 + " " + (!(comp is NotLoaded) ? AssetCabinet.ToString(comp) : String.Empty));
				}
			}*/

			Frames = new List<Transform>();
			Meshes = new List<MeshRenderer>();
			Materials = new List<Material>();
			Textures = new List<Texture2D>();
			InitLists(parser.RootTransform);
		}

		private void InitLists()
		{
			HashSet<Transform> framesBefore = new HashSet<Transform>(Frames);
			Frames.Clear();
			Meshes.Clear();
			Materials.Clear();
			Textures.Clear();
			InitLists(Parser.RootTransform);

			HashSet<Mesh> meshesForRemoval = new HashSet<Mesh>();
			foreach (Transform trans in framesBefore)
			{
				if (!Frames.Contains(trans))
				{
					foreach (SkinnedMeshRenderer smr in Meshes)
					{
						if (smr == null)
						{
							continue;
						}
						for (int i = 0; i < smr.m_Bones.Count; i++)
						{
							PPtr<Transform> bonePtr = smr.m_Bones[i];
							string boneName = bonePtr.instance.m_GameObject.instance.m_Name;
							if (bonePtr.instance == trans)
							{
								Transform newBone = Frames.Find
								(
									delegate(Transform t)
									{
										return t.m_GameObject.instance.m_Name == boneName;
									}
								);
								if (newBone == null)
								{
									Report.ReportLog("Bone " + boneName + " in SMR " + smr.m_GameObject.instance.m_Name + " lost");
								}
								else
								{
									smr.m_Bones.RemoveAt(i);
									smr.m_Bones.Insert(i, new PPtr<Transform>(newBone));
								}
								break;
							}
						}
					}

					SkinnedMeshRenderer sMesh = trans.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
					if (sMesh != null)
					{
						sMesh.m_GameObject.instance.RemoveLinkedComponent(sMesh);
						meshesForRemoval.Add(sMesh.m_Mesh.instance);
						Parser.file.RemoveSubfile(sMesh);
					}
					trans.m_GameObject.instance.RemoveLinkedComponent(trans);
					Parser.file.RemoveSubfile(trans);
				}
			}
			RemoveUnlinkedMeshes(meshesForRemoval);
		}

		private void RemoveUnlinkedMeshes(HashSet<Mesh> meshesForRemoval)
		{
			foreach (SkinnedMeshRenderer smr in Meshes)
			{
				if (meshesForRemoval.Contains(smr.m_Mesh.instance))
				{
					meshesForRemoval.Remove(smr.m_Mesh.instance);
				}
			}
			foreach (Mesh mesh in meshesForRemoval)
			{
				Parser.file.RemoveSubfile(mesh);
				Bundle.DeleteComponent(mesh);
			}
		}

		private void InitLists(Transform frame)
		{
			Frames.Add(frame);
			MeshRenderer mesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
			if (mesh == null)
			{
				mesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshRenderer);
			}
			if (mesh != null)
			{
				Meshes.Add(mesh);
				foreach (PPtr<Material> matPtr in mesh.m_Materials)
				{
					Material mat = matPtr.instance;
					if (mat != null && !Materials.Contains(mat))
					{
						Materials.Add(mat);
						foreach (var pair in mat.m_SavedProperties.m_TexEnvs)
						{
							Texture2D tex = pair.Value.m_Texture.instance;
							if (tex != null && !Textures.Contains(tex))
							{
								Textures.Add(tex);
							}
						}
					}
				}
			}

			foreach (Transform child in frame)
			{
				InitLists(child);
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
			Parser.m_Avatar.instance.RenameBone(oldName, name);

			Changed = true;
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
			Parser.m_Avatar.instance.RemoveBone(frame.m_GameObject.instance.m_Name);

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

		[Plugin]
		public void AddFrame(Transform newFrame, int destParentId)
		{
			if (destParentId < 0)
			{
				Parser.RootTransform = newFrame;
			}
			else
			{
				Frames[destParentId].AddChild(newFrame);
			}

			InitLists();
			Changed = true;
		}

		[Plugin]
		public void AddFrame(Transform srcFrame, List<Material> srcMaterials, List<Texture2D> srcTextures, bool appendIfMissing, int destParentId)
		{
			AddFrame(srcFrame, destParentId);
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
				Parser.RootTransform = srcParent[0];
				Parser.RootTransform.m_GameObject.instance.AddLinkedComponent(Parser);
				srcParent.RemoveChild(0);
				destParent.m_GameObject.instance.RemoveLinkedComponent(destParent);
				srcFrame.file.RemoveSubfile(destParent.m_GameObject.instance);
				srcFrame.file.RemoveSubfile(destParent);
			}

			srcGameObj.RemoveLinkedComponent(srcParent);
			srcFrame.file.RemoveSubfile(srcGameObj);
			srcFrame.file.RemoveSubfile(srcParent);

			InitLists();
			Changed = true;
		}

		[Plugin]
		public void MergeFrame(Transform srcFrame, List<Material> srcMaterials, List<Texture2D> srcTextures, bool appendIfMissing, int destParentId)
		{
			MergeFrame(srcFrame, destParentId);
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
			smr.m_Mesh.instance.m_BindPose[boneId] = boneMatrix;
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

			smr.m_Mesh.instance.m_BindPose[boneId] = m;
			Changed = true;
		}

		[Plugin]
		public void ReplaceSkinnedMeshRenderer(WorkspaceMesh mesh, int frameId, int rootBoneId, bool merge, string normals, string bones, bool targetFullMesh)
		{
			var normalsMethod = (CopyMeshMethod)Enum.Parse(typeof(CopyMeshMethod), normals);
			var bonesMethod = (CopyMeshMethod)Enum.Parse(typeof(CopyMeshMethod), bones);
			Transform rootBone = rootBoneId >= 0 && rootBoneId < Frames.Count ? Frames[rootBoneId] : null;
			Operations.ReplaceSkinnedMeshRenderer(Frames[frameId], rootBone, Parser, Bundle, Materials, mesh, merge, normalsMethod, bonesMethod, targetFullMesh);

			InitLists();
			Changed = true;
		}

		[Plugin]
		public void RemoveSkinnedMeshRenderer(int id)
		{
			SkinnedMeshRenderer sMesh = Meshes[id] as SkinnedMeshRenderer;
			if (sMesh == null)
			{
				return;
			}
			if (sMesh.m_Mesh.instance != null)
			{
				HashSet<Mesh> meshesForRemoval = new HashSet<Mesh>();
				meshesForRemoval.Add(sMesh.m_Mesh.instance);
				sMesh.m_Mesh = new PPtr<Mesh>((Component)null);
				RemoveUnlinkedMeshes(meshesForRemoval);
			}
			sMesh.m_GameObject.instance.RemoveLinkedComponent(sMesh);
			Parser.file.RemoveSubfile(sMesh);

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
		public void SetMesh(int meshId, int componentIndex)
		{
			MeshRenderer meshRenderer = Meshes[meshId];
			Component asset = componentIndex >= 0 ? Parser.file.Components[componentIndex] : null;
			Mesh mesh = asset is NotLoaded ? Parser.file.LoadComponent(asset.pathID) : asset;
			if (meshRenderer is SkinnedMeshRenderer)
			{
				SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshRenderer;
				sMesh.m_Mesh = new PPtr<Mesh>(mesh);
			}
			else
			{
				MeshFilter filter = meshRenderer.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshFilter);
				filter.m_Mesh = new PPtr<Mesh>(mesh);
			}
			Changed = true;
		}

		[Plugin]
		public void SetMeshName(int id, string name)
		{
			MeshRenderer meshRenderer = Meshes[id];
			Mesh mesh;
			if (meshRenderer is SkinnedMeshRenderer)
			{
				SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshRenderer;
				mesh = sMesh.m_Mesh.instance;
			}
			else
			{
				MeshFilter filter = meshRenderer.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshFilter);
				mesh = filter.m_Mesh.instance;
			}
			if (mesh != null)
			{
				mesh.m_Name = name;
				Changed = true;
			}
		}

		[Plugin]
		public void SetSubMeshMaterial(int meshId, int subMeshId, int material)
		{
			MeshRenderer meshRenderer = Meshes[meshId];
			Material mat = Materials[material];
			meshRenderer.m_Materials[subMeshId] = new PPtr<Material>(mat);
			Changed = true;
		}

		[Plugin]
		public void SetSubMeshTopology(int meshId, int subMeshId, int topology)
		{
			MeshRenderer meshR = Meshes[meshId];
			Mesh mesh;
			if (meshR is SkinnedMeshRenderer)
			{
				SkinnedMeshRenderer smr = (SkinnedMeshRenderer)meshR;
				mesh = smr.m_Mesh.instance;
			}
			else
			{
				MeshFilter filter = meshR.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshFilter);
				mesh = filter.m_Mesh.instance;
			}
			mesh.m_SubMeshes[subMeshId].topology = topology;
			Changed = true;
		}

		[Plugin]
		public void RemoveSubMesh(int meshId, int subMeshId)
		{
			MeshRenderer meshR = Meshes[meshId];
			Operations.vMesh vMesh = new Operations.vMesh(meshR, true);
			if (vMesh.submeshes.Count == 1)
			{
				RemoveSkinnedMeshRenderer(meshId);
			}
			else
			{
				vMesh.submeshes.RemoveAt(subMeshId);
				vMesh.Flush();
				Changed = true;
			}
		}

		[Plugin]
		public void SetMaterialName(int id, string name)
		{
			Materials[id].m_Name = name;
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

			/*for (int i = 0; i < Bundle.m_PreloadTable.Count; i++)
			{
				PPtr<Object> objPtr = Bundle.m_PreloadTable[i];
				if (objPtr.asset == mat)
				{
					Bundle.m_PreloadTable.RemoveAt(i--);
				}
			}*/
			int idx = Bundle.m_Container.FindIndex
			(
				delegate(KeyValuePair<string, AssetInfo> match)
				{
					return match.Value.asset.asset == mat;
				}
			);
			if (idx >= 0)
			{
				Bundle.m_Container.RemoveAt(idx);
			}

			Parser.file.RemoveSubfile(mat);
			Materials.RemoveAt(id);
			Changed = true;
		}

		[Plugin]
		public Material CopyMaterial(int id)
		{
			Material oldMat = Materials[id];
			Material newMat = oldMat.Clone(Parser.file);
			newMat.m_Name += "_Copy";

			for (int i = 0; i < Bundle.m_PreloadTable.Count; i++)
			{
				PPtr<Object> objPtr = Bundle.m_PreloadTable[i];
				if (objPtr.asset == oldMat)
				{
					Bundle.m_PreloadTable.Insert(++i, new PPtr<Object>(newMat));
				}
			}
			int idx = Bundle.m_Container.FindIndex
			(
				delegate(KeyValuePair<string, AssetInfo> match)
				{
					return match.Value.asset.asset == oldMat;
				}
			);
			if (idx >= 0)
			{
				AssetInfo orgInfo = Bundle.m_Container[idx].Value;
				AssetInfo info = new AssetInfo(Parser.file);
				info.preloadIndex = orgInfo.preloadIndex;
				info.preloadSize = orgInfo.preloadSize;
				info.asset = new PPtr<Object>(newMat);
				Bundle.m_Container.Insert(idx + 1, new KeyValuePair<string, AssetInfo>(newMat.m_Name, info));
			}

			Materials.Add(newMat);
			Changed = true;
			return newMat;
		}

		[Plugin]
		public void MergeMaterial(ImportedMaterial mat)
		{
			Material dest = Operations.FindMaterial(Materials, mat.Name);
			Operations.ReplaceMaterial(dest, mat);
			Changed = true;
		}

		[Plugin]
		public void MergeMaterial(Material mat)
		{
			Shader oldShader = mat.m_Shader.instance != null && mat.file != Parser.file ? FindMaterialShader(mat.m_Shader.instance.m_Name) : null;
			Material newMat;
			Material oldMat = Operations.FindMaterial(Materials, mat.m_Name);
			if (oldMat != null)
			{
				if (oldMat == mat)
				{
					return;
				}
				HashSet<Component> addedComponents = new HashSet<Component>();
				mat.CopyTo(oldMat, addedComponents);
				newMat = oldMat;
			}
			else
			{
				newMat = mat.Clone(Parser.file);
			}
			if (oldShader != null)
			{
				DeleteNewShader(newMat.m_Shader.instance);
				newMat.m_Shader = new PPtr<Shader>(oldShader);
				Report.ReportLog("Warning! Using existing Shader " + newMat.m_Shader.instance.m_Name);
			}
			else if (newMat.m_Shader.instance != null && mat.file != newMat.file)
			{
				HashSet<Material> delayInsertion = new HashSet<Material>();
				delayInsertion.Add(newMat);
				HashSet<string> filterMessages = new HashSet<string>();
				ReplaceDependentObjects(newMat.m_Shader.instance, delayInsertion, filterMessages);
				delayInsertion.Remove(newMat);
				Materials.AddRange(delayInsertion);
			}
			if (oldMat == null)
			{
				Materials.Add(newMat);
			}
			Changed = true;
		}

		private void ReplaceDependentObjects(Shader shader, HashSet<Material> delayInsertion, HashSet<string> filterMessages)
		{
			for (int i = 0; i < shader.m_Dependencies.Count; i++)
			{
				PPtr<Shader> asset = shader.m_Dependencies[i];
				if (asset.instance != null)
				{
					string msg = "Warning! Using existing dependent Shader " + asset.instance.m_Name;
					Shader orgShader = FindMaterialShader(asset.instance.m_Name);
					if (orgShader != null)
					{
						shader.m_Dependencies.RemoveAt(i);
						shader.m_Dependencies.Insert(i, new PPtr<Shader>(orgShader));
						Parser.file.RemoveSubfile(asset.instance);
						if (!filterMessages.Contains(msg))
						{
							Report.ReportLog(msg);
							filterMessages.Add(msg);
						}
					}
					else
					{
						filterMessages.Add(msg);
						ReplaceDependentObjects(asset.instance, delayInsertion, filterMessages);
					}
				}
				else if (asset.asset is Material)
				{
					Material shaderMat = (Material)asset.asset;
					int matId = GetMaterialId(shaderMat.m_Name);
					if (matId >= 0)
					{
						shader.m_Dependencies.RemoveAt(i);
						shader.m_Dependencies.Insert(i, new PPtr<Shader>(Materials[matId]));
						Parser.file.RemoveSubfile(shaderMat);
						string msg = "Warning! Using existing Material " + shaderMat.m_Name;
						if (!filterMessages.Contains(msg))
						{
							Report.ReportLog(msg);
							filterMessages.Add(msg);
						}
					}
					else
					{
						if (delayInsertion.Add(shaderMat) && shaderMat.m_Shader.instance != null)
						{
							ReplaceDependentObjects(shaderMat.m_Shader.instance, delayInsertion, filterMessages);
						}
					}
				}
				else if (asset.asset is Texture2D)
				{
					Texture2D shaderTex = (Texture2D)asset.asset;
					string msg = "Warning! Using existing Texture " + shaderTex.m_Name;
					int texId = GetTextureId(shaderTex.m_Name);
					if (texId >= 0)
					{
						shader.m_Dependencies.RemoveAt(i);
						shader.m_Dependencies.Insert(i, new PPtr<Shader>(Textures[texId]));
						Parser.file.RemoveSubfile(shaderTex);
						Parser.file.Parser.Textures.Remove(shaderTex);
						if (!filterMessages.Contains(msg))
						{
							Report.ReportLog(msg);
							filterMessages.Add(msg);
						}
					}
					else
					{
						filterMessages.Add(msg);
						Textures.Add(shaderTex);
					}
				}
			}
		}

		Shader FindMaterialShader(string name)
		{
			Shader result = null;
			Materials.Find
			(
				delegate(Material mat)
				{
					Shader shader = mat.m_Shader.instance;
					if (shader != null)
					{
						result = FindMaterialShader(shader, name);
						return result != null;
					}
					return false;
				}
			);
			return result;
		}

		Shader FindMaterialShader(Shader shader, string name)
		{
			if (shader.m_Name == name)
			{
				return shader;
			}
			for (int i = 0; i < shader.m_Dependencies.Count; i++)
			{
				PPtr<Shader> asset = shader.m_Dependencies[i];
				if (asset.instance != null)
				{
					Shader depShader = FindMaterialShader(asset.instance, name);
					if (depShader != null)
					{
						return depShader;
					}
				}
			}
			return null;
		}

		void DeleteNewShader(Shader shader)
		{
			Parser.file.RemoveSubfile(shader);
			for (int i = 0; i < shader.m_Dependencies.Count; i++)
			{
				PPtr<Shader> asset = shader.m_Dependencies[i];
				if (asset.instance != null)
				{
					DeleteNewShader(asset.instance);
				}
				else if (asset.asset is Material)
				{
					Material mat = (Material)asset.asset;
					Parser.file.RemoveSubfile(mat);
					if (mat.m_Shader.instance != null)
					{
						DeleteNewShader(mat.m_Shader.instance);
					}
				}
				else if (asset.asset is Texture2D)
				{
					Parser.file.RemoveSubfile(asset.asset);
				}
			}
		}

		[Plugin]
		public void MergeTexture(ImportedTexture tex)
		{
			Texture2D oldTex = Parser.file.Parser.GetTexture(tex.Name);
			if (oldTex == null)
			{
				oldTex = new Texture2D(Parser.file);
				Textures.Add(oldTex);
			}
			oldTex.LoadFrom(tex);
			Changed = true;
		}

		[Plugin]
		public void MergeTexture(Texture2D tex)
		{
			if (tex.file != Parser.file)
			{
				Texture2D oldTex = Parser.file.Parser.GetTexture(tex.m_Name);
				if (oldTex == null)
				{
					oldTex = new Texture2D(Parser.file);
					Textures.Add(oldTex);
				}
				tex.CopyAttributesTo(oldTex);
				tex.CopyImageTo(oldTex);
				Changed = true;
			}
		}

		[Plugin]
		public void SetTextureName(int id, string name)
		{
			Textures[id].m_Name = name;
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
			Changed = true;
		}

		[Plugin]
		public void ReplaceTexture(int id, ImportedTexture image)
		{
			var oldTex = Textures[id];
			oldTex.LoadFrom(image);
			Changed = true;
		}
	}
}
