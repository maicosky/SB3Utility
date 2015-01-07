using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public static partial class Operations
	{
		public static Transform CreateTransformTree(ImportedFrame frame)
		{
			Transform trans = new Transform(null, 0, UnityClassID.Transform, UnityClassID.Transform);

			GameObject gameObj = new GameObject(null, 0, UnityClassID.GameObject, UnityClassID.GameObject);
			gameObj.m_Component = new List<KeyValuePair<UnityClassID, PPtr<Component>>>(1);
			var compKey = new KeyValuePair<UnityClassID, PPtr<Component>>(UnityClassID.Transform, new PPtr<Component>(trans));
			gameObj.m_Component.Add(compKey);
			gameObj.m_Name = (string)frame.Name.Clone();
			trans.m_GameObject = new PPtr<GameObject>(gameObj);

			Vector3 t, s;
			Quaternion r;
			frame.Matrix.Decompose(out s, out r, out t);
			trans.m_LocalRotation = r;
			trans.m_LocalPosition = t;
			trans.m_LocalScale = s;

			trans.InitChildren(frame.Count);
			for (int i = 0; i < frame.Count; i++)
			{
				trans.AddChild(CreateTransformTree(frame[i]));
			}

			return trans;
		}

		public static void CopyOrCreateUnknowns(Transform dest, Transform root)
		{
			Transform src = FindFrame(dest.m_GameObject.instance.m_Name, root);
			if (src == null)
			{
				CreateUnknowns(dest);
			}
			else
			{
				CopyUnknowns(src, dest);
			}

			for (int i = 0; i < dest.Count; i++)
			{
				CopyOrCreateUnknowns(dest[i], root);
			}
		}

		public static void ReplaceMaterial(UnityParser parser, ImportedMaterial material)
		{
			for (int i = 0; i < parser.Cabinet.Components.Count; i++)
			{
				Component comp = parser.Cabinet.Components[i];
				if (comp.classID1 == UnityClassID.Material)
				{
					Material mat = parser.Cabinet.LoadComponent(comp.pathID);
					if (mat.m_Name == material.Name)
					{
						ReplaceMaterial(mat, material);
						return;
					}
				}
			}

			throw new Exception("Replacing a material currently requires an existing material with the same name");
		}

		public static void ReplaceTexture(UnityParser parser, ImportedTexture texture)
		{
			Texture2D tex = parser.GetTexture(texture.Name);
			if (tex == null)
			{
				tex = new Texture2D(parser.Cabinet, 0, UnityClassID.Texture2D, UnityClassID.Texture2D);
				parser.Cabinet.ReplaceSubfile(-1, tex, null);
				Console.WriteLine("add tex with index=" + parser.Cabinet.Components.IndexOf(tex));
				tex.LoadFrom(texture);
				return;
			}
			ReplaceTexture(tex, texture);
		}

		public static void ReplaceMaterial(Material mat, ImportedMaterial material)
		{
			if (mat == null)
			{
				throw new Exception("Replacing a material currently requires an existing material with the same name");
			}

			for (int i = 0; i < mat.m_SavedProperties.m_Colors.Count; i++)
			{
				var col = mat.m_SavedProperties.m_Colors[i];
				Color4 att;
				switch (col.Key.name)
				{
				case "_Color":
					att = material.Diffuse;
					break;
				case "_SColor":
					att = material.Ambient;
					break;
				case "_ReflectColor":
					att = material.Emissive;
					break;
				case "_SpecColor":
					att = material.Specular;
					break;
				case "_RimColor":
				case "_OutlineColor":
				case "_ShadowColor":
				default:
					continue;
				}
				mat.m_SavedProperties.m_Colors.RemoveAt(i);
				col = new KeyValuePair<FastPropertyName, SlimDX.Color4>(col.Key, att);
				mat.m_SavedProperties.m_Colors.Insert(i, col);
			}

			for (int i = 0; i < mat.m_SavedProperties.m_Floats.Count; i++)
			{
				var flt = mat.m_SavedProperties.m_Floats[i];
				float att;
				switch (flt.Key.name)
				{
				case "_Shininess":
					att = material.Power;
					break;
				case "_RimPower":
				case "_Outline":
				default:
					continue;
				}
				mat.m_SavedProperties.m_Floats.RemoveAt(i);
				flt = new KeyValuePair<FastPropertyName, float>(flt.Key, att);
				mat.m_SavedProperties.m_Floats.Insert(i, flt);
			}
		}

		public static void ReplaceTexture(Texture2D tex, ImportedTexture texture)
		{
			if (tex == null)
			{
				throw new Exception("Replacing a texture currently requires an existing texture with the same name");
			}

			Texture2D t2d = new Texture2D(null, tex.pathID, tex.classID1, tex.classID2);
			t2d.LoadFrom(texture);
			tex.image_data = t2d.image_data;
		}
	}
}
