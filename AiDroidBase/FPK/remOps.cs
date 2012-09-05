using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using SB3Utility;

namespace AiDroidPlugin
{
	public static partial class rem
	{
		public static ImportedTexture ImportedTexture(remId texture, string remPath, bool diffuse_else_ambient)
		{
			string matTexName = texture.ToString();
			String texh_folder = "..\\..\\TEXH\\";
			if (remPath.Contains("(v2)"))
				texh_folder = "..\\" + texh_folder;
			texh_folder = remPath + Path.DirectorySeparatorChar + texh_folder;
			String body = texture.ToString().Substring(0, matTexName.LastIndexOf('.'));
			String ext =  texture.ToString().Substring(matTexName.LastIndexOf('.'));
			String pattern = body + (diffuse_else_ambient ? "" : "_mask01") + ext;
			String[] files = null;
			try
			{
				files = Directory.GetFiles(texh_folder, pattern);
			}
			catch (DirectoryNotFoundException) { }
			if (files != null && files.Length > 0)
				matTexName = files[0].Substring(files[0].LastIndexOf('\\') + 1);
			else
			{
				texh_folder += "TexH(v2)\\";
				String pre = "zlc-";
				pattern = diffuse_else_ambient ? " *" : "_mask01*";
				try
				{
					files = Directory.GetFiles(texh_folder, pre + body + pattern + ext);
				}
				catch (DirectoryNotFoundException) { }
				if (files != null && files.Length > 0)
					matTexName = files[0].Substring(files[0].LastIndexOf('\\') + 1);
				else
					Report.ReportLog(
						(diffuse_else_ambient ? body : body + "_mask01") + ext +
							" neither found in TEXH nor in TEXH\\TexH(v2) folder. Using " + matTexName + " instead."
					);
			}
			return new ImportedTexture(files != null && files.Length > 0 ? files[0] : matTexName);
		}

		public static HashSet<string> SearchHierarchy(remParser parser, remMesh mesh)
		{
			HashSet<string> exportFrames = new HashSet<string>();
			SearchHierarchy(parser.RemFile.BONC.rootFrame, mesh, exportFrames);
			remSkin boneList = FindSkin(mesh.name, parser.RemFile.SKIC);
			if (boneList != null)
			{
				for (int i = 0; i < boneList.numWeights; i++)
				{
					if (!exportFrames.Contains(boneList[i].bone.ToString()))
					{
						remBone boneParent = FindFrame(boneList[i].bone, parser.RemFile.BONC.rootFrame);
						while (boneParent.Parent != null)
						{
							exportFrames.Add(boneParent.name.ToString());
							boneParent = (remBone)boneParent.Parent;
						}
					}
				}
			}

			return exportFrames;
		}

		static void SearchHierarchy(remBone frame, remMesh mesh, HashSet<string> exportFrames)
		{
			if (frame.name == mesh.frame)
			{
				remBone parent = frame;
				while (parent.Parent != null)
				{
					exportFrames.Add(parent.name.ToString());
					parent = (remBone)parent.Parent;
				}
				return;
			}

			for (int i = 0; i < frame.numChilds; i++)
			{
				SearchHierarchy(frame[i], mesh, exportFrames);
			}
		}

		public static remBone FindFrame(remId frameId, remBone frame)
		{
			if (frame.name == frameId)
				return frame;

			for (int i = 0; i < frame.numChilds; i++)
			{
				remBone foundFrame = FindFrame(frameId, frame[i]);
				if (foundFrame != null)
				{
					return foundFrame;
				}
			}

			return null;
		}

		public static remSkin FindSkin(remId meshId, remSKICsection skins)
		{
			for (int i = 0; i < skins.length; i++)
			{
				if (skins[i].mesh == meshId)
					return skins[i];
			}

			return null;
		}

		public static remMaterial FindMaterial(remId materialId, remMATCsection mats)
		{
			foreach (remMaterial mat in mats.materials)
			{
				if (mat.name == materialId)
					return mat;
			}

			return null;
		}
	}
}
