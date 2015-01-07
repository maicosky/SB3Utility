using System;
using System.Collections.Generic;

namespace UnityPlugin
{
	public static partial class Operations
	{
		public static Transform FindFrame(string name, Transform root)
		{
			Transform frame = root;
			if ((frame != null) && (frame.m_GameObject.instance.m_Name == name))
			{
				return frame;
			}

			for (int i = 0; i < root.Count; i++)
			{
				if ((frame = FindFrame(name, root[i])) != null)
				{
					return frame;
				}
			}

			return null;
		}

		public static List<SkinnedMeshRenderer> FindMeshes(Transform rootFrame, List<string> nameList)
		{
			List<SkinnedMeshRenderer> meshList = new List<SkinnedMeshRenderer>(nameList.Count);
			FindMeshFrames(rootFrame, meshList, nameList);
			return meshList;
		}

		static void FindMeshFrames(Transform frame, List<SkinnedMeshRenderer> meshList, List<string> nameList)
		{
			SkinnedMeshRenderer mesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
			if ((mesh != null) && nameList.Contains(frame.m_GameObject.instance.m_Name))
			{
				meshList.Add(mesh);
			}

			for (int i = 0; i < frame.Count; i++)
			{
				FindMeshFrames(frame[i], meshList, nameList);
			}
		}

		public static Material FindMaterial(List<Material> matList, string name)
		{
			foreach (Material mat in matList)
			{
				if (mat.m_Name == name)
				{
					return mat;
				}
			}
			return null;
		}

		public static Texture2D FindTexture(List<Texture2D> texList, string name)
		{
			foreach (Texture2D tex in texList)
			{
				if (name.Contains(tex.m_Name))
				{
					return tex;
				}
			}
			return null;
		}

		public static void CopyUnknowns(Transform src, Transform dest)
		{
			dest.m_GameObject.instance.m_Layer = src.m_GameObject.instance.m_Layer;
			dest.m_GameObject.instance.m_Tag = src.m_GameObject.instance.m_Tag;
			dest.m_GameObject.instance.m_isActive = src.m_GameObject.instance.m_isActive;
		}

		public static void CreateUnknowns(Transform frame)
		{
			frame.m_GameObject.instance.m_isActive = true;
		}
	}
}
