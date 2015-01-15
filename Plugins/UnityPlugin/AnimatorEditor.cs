using System;
using System.Collections.Generic;

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

			foreach (SkinnedMeshRenderer smr in Meshes)
			{
				string mats = " mat=(";
				foreach (PPtr<Material> matPtr in smr.m_Materials)
				{
					mats += matPtr.instance.m_Name + ",";
				}
				mats = mats.Substring(0, mats.Length - 1) + ")";
				Console.WriteLine(smr.m_GameObject.instance.m_Name + " pathID=" + smr.pathID + " mesh=" + (smr.m_Mesh.instance != null ? smr.m_Mesh.instance.m_Name : "null") + " pathID=" + smr.m_Mesh.m_PathID + " mat="+ mats);
			}
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
			Console.WriteLine("Warning! Avatar not updated");

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

		void AddFrame(Transform newFrame, int destParentId)
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
		public void MergeFrame(ImportedFrame srcFrame, int destParentId)
		{
			Transform destParent = destParentId >= 0 ? Frames[destParentId] : Parser.RootTransform;
			Transform newFrame = Operations.CreateTransformTree(Parser, srcFrame, destParent);
			Operations.CopyOrCreateUnknowns(newFrame, Parser.RootTransform);

			MergeFrame(newFrame, destParentId);
		}

		void MergeFrame(Transform newFrame, int destParentId)
		{
			Transform srcParent = new Transform(newFrame.file);
			GameObject srcGameObj = new GameObject(newFrame.file);
			srcGameObj.AddLinkedComponent(srcParent);
			srcParent.InitChildren(1);
			srcParent.AddChild(newFrame);

			Transform destParent;
			if (destParentId < 0)
			{
				destParent = new Transform(newFrame.file);
				GameObject destGameObj = new GameObject(newFrame.file);
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
				newFrame.file.RemoveSubfile(destParent.m_GameObject.instance);
				newFrame.file.RemoveSubfile(destParent);
			}

			srcGameObj.RemoveLinkedComponent(srcParent);
			newFrame.file.RemoveSubfile(srcGameObj);
			newFrame.file.RemoveSubfile(srcParent);

			InitLists();
			Changed = true;
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
			HashSet<Mesh> meshesForRemoval = new HashSet<Mesh>();
			meshesForRemoval.Add(sMesh.m_Mesh.instance);
			RemoveUnlinkedMeshes(meshesForRemoval);
			sMesh.m_GameObject.instance.RemoveLinkedComponent(sMesh);
			Parser.file.RemoveSubfile(sMesh);
			InitLists();
		}

		[Plugin]
		public void SetMesh(SkinnedMeshRenderer sMesh, Mesh mesh)
		{
			sMesh.m_Mesh = new PPtr<Mesh>(mesh);
		}

		[Plugin]
		public void MergeMaterial(ImportedMaterial material)
		{
			Material mat = Operations.FindMaterial(Materials, material.Name);
			Operations.ReplaceMaterial(mat, material);
			Changed = true;
		}

		[Plugin]
		public void MergeTexture(ImportedTexture texture)
		{
			Operations.ReplaceTexture(Parser.file.Parser, texture);
			Changed = true;
		}
	}
}
