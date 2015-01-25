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
		public List<SkinnedMeshRenderer> Meshes { get; set; }
		public List<Material> Materials { get; set; }
		public List<Texture2D> Textures { get; set; }

		public Animator Parser { get; protected set; }
		private AssetBundle Bundle { get; set; }

		protected bool contentChanged = false;

		public AnimatorEditor(Animator parser)
		{
			Parser = parser;
			Bundle = Parser.file.LoadComponent(1);

			Frames = new List<Transform>();
			Meshes = new List<SkinnedMeshRenderer>();
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
			SkinnedMeshRenderer mesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
			if (mesh != null)
			{
				Meshes.Add(mesh);
				foreach (PPtr<Material> matPtr in mesh.m_Materials)
				{
					Material mat = matPtr.instance;
					if (!Materials.Contains(mat))
					{
						Materials.Add(mat);
						foreach (var pair in mat.m_SavedProperties.m_TexEnvs)
						{
							Texture2D tex = pair.Value.m_Texture.instance;
							if (!Textures.Contains(tex))
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
		public int GetSkinnedMeshRendererId(string name)
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
		public void RemoveFrame(int id)
		{
			var frame = Frames[id];
			var parent = (Transform)frame.Parent;
			if (parent == null)
			{
				throw new Exception("The root transform can't be removed");
			}

			parent.RemoveChild(frame);
			Report.ReportLog("Warning! Avatar not updated");

			InitLists();
			Changed = true;
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
		public void ReplaceSkinnedMeshRenderer(WorkspaceMesh mesh, int frameId, int rootBoneId, bool merge, string normals, string bones)
		{
			var normalsMethod = (CopyMeshMethod)Enum.Parse(typeof(CopyMeshMethod), normals);
			var bonesMethod = (CopyMeshMethod)Enum.Parse(typeof(CopyMeshMethod), bones);
			Transform rootBone = rootBoneId >= 0 && rootBoneId < Frames.Count ? Frames[rootBoneId] : null;
			Operations.ReplaceSkinnedMeshRenderer(Frames[frameId], rootBone, Parser, Bundle, Materials, mesh, merge, normalsMethod, bonesMethod);

			InitLists();
			Changed = true;
		}

		[Plugin]
		public void RemoveSkinnedMeshRenderer(int id)
		{
			SkinnedMeshRenderer sMesh = Meshes[id];
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
		public void SetMesh(SkinnedMeshRenderer sMesh, Mesh mesh)
		{
			if (sMesh.m_Mesh.instance != mesh)
			{
				sMesh.m_Mesh = new PPtr<Mesh>(mesh);
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
		public void SetMaterialPhong(int id, object[] diffuse, object[] ambient, object[] specular, object[] emissive, double shininess)
		{
			Material mat = Materials[id];
			for (int i = 0; i < mat.m_SavedProperties.m_Colors.Count; i++)
			{
				var col = mat.m_SavedProperties.m_Colors[i];
				object[] newVal = null;
				switch (col.Key.name)
				{
				case "_Color":
					newVal = diffuse;
					break;
				case "_SColor":
					newVal = ambient;
					break;
				case "_ReflectColor":
					newVal = emissive;
					break;
				case "_SpecColor":
					newVal = specular;
					break;
				case "_RimColor":
				case "_OutlineColor":
				case "_ShadowColor":
				default:
					continue;
				}
				mat.m_SavedProperties.m_Colors.RemoveAt(i);
				col =  new KeyValuePair<FastPropertyName, Color4>(col.Key, new Color4((float)(double)newVal[3], (float)(double)newVal[0], (float)(double)newVal[1], (float)(double)newVal[2]));
				mat.m_SavedProperties.m_Colors.Insert(i, col);
			}

			for (int i = 0; i < mat.m_SavedProperties.m_Floats.Count; i++)
			{
				var flt = mat.m_SavedProperties.m_Floats[i];
				float newVal;
				switch (flt.Key.name)
				{
				case "_Shininess":
					newVal = (float)shininess;
					break;
				case "_RimPower":
				case "_Outline":
				default:
					continue;
				}
				mat.m_SavedProperties.m_Floats.RemoveAt(i);
				mat.m_SavedProperties.m_Floats.Insert(i, new KeyValuePair<FastPropertyName,float>(flt.Key, newVal));
			}
			Changed = true;
		}

		[Plugin]
		public void RemoveMaterial(int id)
		{
			Parser.file.RemoveSubfile(Materials[id]);
			Materials.RemoveAt(id);
			Changed = true;
		}

		[Plugin]
		public void CopyMaterial(int id)
		{
			Material newMat = Materials[id].Clone(Parser.file);
			newMat.m_Name += "_Copy";
			Materials.Add(newMat);
			Changed = true;
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
			Material oldMat = Operations.FindMaterial(Materials, mat.m_Name);
			if (oldMat != null)
			{
				mat.CopyTo(oldMat);
			}
			else
			{
				Material newMat = mat.Clone(Parser.file);
				Materials.Add(newMat);
			}
			Changed = true;
		}

		[Plugin]
		public void SetMaterialTexture(int id, int index, string name)
		{
			Texture2D tex = Parser.file.Parser.GetTexture(name);
			if (tex != null)
			{
				var texEnv = Materials[id].m_SavedProperties.m_TexEnvs[index].Value;
				texEnv.m_Texture = new PPtr<Texture2D>(tex);
				Changed = true;
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
		}
	}
}
