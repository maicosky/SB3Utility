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

		protected bool contentChanged = false;

		public AnimatorEditor(Animator parser)
		{
			Parser = parser;

			Frames = new List<Transform>();
			Meshes = new List<SkinnedMeshRenderer>();
			Materials = new List<Material>();
			Textures = new List<Texture2D>();
			InitLists(parser.RootTransform);
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
		public void AddFrame(ImportedFrame srcFrame, int destParentId)
		{
			Transform newFrame = Operations.CreateTransformTree(srcFrame);
			Operations.CopyOrCreateUnknowns(newFrame, Parser.RootTransform);

			AddFrame(newFrame, destParentId);
		}

		/*[Plugin]
		public void AddFrame(Transform srcFrame, List<xxMaterial> srcMaterials, List<xxTexture> srcTextures, bool appendIfMissing, int destParentId)
		{
			int[] matTranslation = CreateMaterialTranslation(srcMaterials, srcFormat, srcTextures, appendIfMissing);
			var newFrame = srcFrame.Clone(true, true, matTranslation);
			xx.ConvertFormat(newFrame, srcFormat, Parser.Format);

			AddFrame(newFrame, destParentId);
		}*/

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

			Frames.Clear();
			Meshes.Clear();
			InitLists(Parser.RootTransform);
			Changed = true;
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
			Texture2D tex = Operations.FindTexture(Textures, texture.Name);
			Operations.ReplaceTexture(tex, texture);
			Changed = true;
		}
	}
}
