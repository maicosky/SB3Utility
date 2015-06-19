using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

using SB3Utility;

namespace UnityPlugin
{
	public static partial class Plugins
	{
		[Plugin]
		public static UnityParser OpenUnity3d([DefaultVar]string path)
		{
			return new UnityParser(path);
		}

		[Plugin]
		public static void WriteUnity3d([DefaultVar]UnityParser parser)
		{
			parser.WriteArchive(parser.FilePath, true, ".unit-y3d", false);
		}

		/// <summary>
		/// Extracts an asset with the specified pathID and writes it to the specified path.
		/// </summary>
		/// <param name="parser"><b>[DefaultVar]</b> The UnityParser with the asset.</param>
		/// <param name="pathID">The pathID of the asset.</param>
		/// <param name="path">The destination path to write the asset.</param>
		[Plugin]
		public static void ExportAsset([DefaultVar]UnityParser parser, int pathID, string path)
		{
			Component asset = parser.Cabinet.FindComponent(pathID);
			if (asset != null)
			{
				FileInfo file = new FileInfo(path + "." + asset.classID1);
				DirectoryInfo dir = file.Directory;
				if (!dir.Exists)
				{
					dir.Create();
				}

				using (FileStream fs = file.Create())
				{
					NeedsSourceStreamForWriting notLoaded = asset as NeedsSourceStreamForWriting;
					if (notLoaded != null)
					{
						notLoaded.SourceStream = File.OpenRead(parser.FilePath);
					}
					asset.WriteTo(fs);
					if (notLoaded != null)
					{
						notLoaded.SourceStream.Close();
						notLoaded.SourceStream.Dispose();
						notLoaded.SourceStream = null;
					}
				}
			}
		}

		[Plugin]
		public static void ExportUnity3d([DefaultVar]UnityParser parser, string path)
		{
			if (path == String.Empty)
			{
				path = @".\";
			}
			DirectoryInfo dir = new DirectoryInfo(path);
			if (!dir.Exists)
			{
				dir.Create();
			}

			using (Stream sourceStream = File.OpenRead(parser.FilePath))
			{
				for (int i = 0; i < parser.Cabinet.Components.Count; i++)
				{
					var asset = parser.Cabinet.Components[i];
					using (FileStream fs = File.Create(dir.FullName + @"\" + asset.pathID + "." + asset.classID1))
					{
						NeedsSourceStreamForWriting notLoaded = asset as NeedsSourceStreamForWriting;
						if (notLoaded != null)
						{
							notLoaded.SourceStream = sourceStream;
						}
						asset.WriteTo(fs);
						if (notLoaded != null)
						{
							notLoaded.SourceStream = null;
						}
					}
				}
			}
		}

		[Plugin]
		public static void RemoveAsset([DefaultVar]UnityParser parser, int pathID)
		{
			Component asset = parser.Cabinet.FindComponent(pathID);
			if (asset == null)
			{
				throw new Exception("Couldn't find asset PathID=" + pathID);
			}
			parser.Cabinet.RemoveSubfile(asset);
		}

		[Plugin]
		public static void RemoveAssets([DefaultVar]UnityParser parser, int start, int end)
		{
			for (int pathID = start; pathID <= end; pathID++)
			{
				Component asset = parser.Cabinet.FindComponent(pathID);
				if (asset == null)
				{
					throw new Exception("Couldn't find asset PathID=" + pathID);
				}
				parser.Cabinet.RemoveSubfile(asset);
			}
		}

		[Plugin]
		public static void ExportTexture([DefaultVar]UnityParser parser, string name)
		{
			ExportTexture(parser, name, parser.FilePath);
		}

		[Plugin]
		public static void ExportTexture([DefaultVar]UnityParser parser, string name, string path)
		{
			string folder = Path.GetDirectoryName(path);
			if (folder.Length > 0)
			{
				folder += "\\";
			}
			folder += Path.GetFileNameWithoutExtension(path);
			if (name != "*")
			{
				Texture2D tex = parser.GetTexture(name);
				if (tex != null)
				{
					tex.Export(folder);
				}
			}
			else
			{
				for (int i = 0; i < parser.Textures.Count; i++)
				{
					Texture2D tex = parser.GetTexture(i);
					tex.Export(folder);
				}
			}
		}

		[Plugin]
		public static void MergeTexture(UnityParser parser, ImportedTexture texture)
		{
			Operations.ReplaceTexture(parser, texture);
		}

		[Plugin]
		public static void MergeTexture(UnityParser parser, string path)
		{
			ImportedTexture texture = new ImportedTexture(path);
			MergeTexture(parser, texture);
		}

		[Plugin]
		public static void MergeTextures(UnityParser parser, string folder)
		{
			DirectoryInfo dir = new DirectoryInfo(folder);
			if (!dir.Exists)
			{
				throw new Exception("Directory <" + folder + "> does not exist");
			}
			foreach (FileInfo file in dir.EnumerateFiles())
			{
				MergeTexture(parser, file.FullName);
			}
		}

		[Plugin]
		public static void ExportShader(UnityParser parser, string name, string path)
		{
			for (int i = 0; i < parser.Cabinet.Components.Count; i++)
			{
				Component asset = parser.Cabinet.Components[i];
				if (asset.classID1 == UnityClassID.Shader)
				{
					Shader sic = parser.Cabinet.LoadComponent(asset.pathID);
					if (name == "*" || sic.m_Name == name)
					{
						sic.Export(path);
					}
				}
			}
		}

		[Plugin]
		public static void ReplaceShader(UnityParser parser, string path)
		{
			Shader s = Shader.Import(path);
			if (s.m_Script.Length > 0)
			{
				for (int i = 0; i < parser.Cabinet.Components.Count; i++)
				{
					Component asset = parser.Cabinet.Components[i];
					if (asset.classID1 == UnityClassID.Shader)
					{
						Shader sic = parser.Cabinet.LoadComponent(asset.pathID);
						if (sic.m_Name == s.m_Name)
						{
							sic.m_Script = s.m_Script;
							return;
						}
					}
				}
			}
		}

		[Plugin]
		public static void ExportTextAsset(UnityParser parser, string name, string path)
		{
			for (int i = 0; i < parser.Cabinet.Components.Count; i++)
			{
				Component asset = parser.Cabinet.Components[i];
				if (asset.classID1 == UnityClassID.TextAsset)
				{
					TextAsset taic = parser.Cabinet.LoadComponent(asset.pathID);
					if (name == "*" || taic.m_Name == name)
					{
						taic.Export(path);
					}
				}
			}
		}

		[Plugin]
		public static void ReplaceTextAsset(UnityParser parser, string path)
		{
			TextAsset ta = TextAsset.Import(path);
			if (ta.m_Script.Length > 0)
			{
				for (int i = 0; i < parser.Cabinet.Components.Count; i++)
				{
					Component asset = parser.Cabinet.Components[i];
					if (asset.classID1 == UnityClassID.TextAsset)
					{
						TextAsset taic = parser.Cabinet.LoadComponent(asset.pathID);
						if (taic.m_Name == ta.m_Name)
						{
							taic.m_Script = ta.m_Script;
							return;
						}
					}
				}
			}
		}

		[Plugin]
		public static void ExportAudioClip(UnityParser parser, string name, string path)
		{
			for (int i = 0; i < parser.Cabinet.Components.Count; i++)
			{
				Component asset = parser.Cabinet.Components[i];
				if (asset.classID1 == UnityClassID.AudioClip)
				{
					AudioClip ac = parser.Cabinet.LoadComponent(asset.pathID);
					if (name == "*" || ac.m_Name == name)
					{
						ac.Export(path);
					}
				}
			}
		}

		[Plugin]
		public static void ReplaceAudioClip(UnityParser parser, string path)
		{
			AudioClip ac = AudioClip.Import(path);
			if (ac.m_AudioData.Length > 0)
			{
				for (int i = 0; i < parser.Cabinet.Components.Count; i++)
				{
					Component asset = parser.Cabinet.Components[i];
					if (asset.classID1 == UnityClassID.AudioClip)
					{
						AudioClip acic = parser.Cabinet.LoadComponent(asset.pathID);
						if (acic.m_Name == ac.m_Name)
						{
							acic.m_AudioData = ac.m_AudioData;
							return;
						}
					}
				}
			}
		}

		[Plugin]
		public static Animator OpenAnimator([DefaultVar]UnityParser parser, string name)
		{
			foreach (Component subfile in parser.Cabinet.Components)
			{
				if (subfile.classID1 == UnityClassID.Animator)
				{
					if (subfile is Animator)
					{
						Animator a = (Animator)subfile;
						if (a.m_GameObject.instance.m_Name == name)
						{
							return a;
						}
						continue;
					}
					NotLoaded animatorComp = (NotLoaded)subfile;
					using (Stream stream = File.OpenRead(parser.FilePath))
					{
						stream.Position = animatorComp.offset;
						PPtr<GameObject> gameObjPtr = Animator.LoadGameObject(stream);
						for (int i = 0; i < parser.Cabinet.Components.Count; i++)
						{
							Component gameObjSubfile = parser.Cabinet.Components[i];
							if (gameObjSubfile.pathID == gameObjPtr.m_PathID)
							{
								if (gameObjSubfile is GameObject)
								{
									GameObject gameObj = (GameObject)gameObjSubfile;
									if (gameObj.m_Name == name)
									{
										return parser.Cabinet.LoadComponent(stream, animatorComp);
									}
									break;
								}
								NotLoaded gameObjComp = (NotLoaded)gameObjSubfile;
								stream.Position = gameObjComp.offset;
								if (GameObject.LoadName(stream) == name)
								{
									return parser.Cabinet.LoadComponent(stream, animatorComp);
								}
								break;
							}
						}
					}
				}
			}
			return null;
		}

		[Plugin]
		public static UnityParser DeployCollect(UnityParser parser, object[] animatorEditors, object[] singleAssets)
		{
			HashSet<Component> collection = new HashSet<Component>();

			if (animatorEditors != null)
			{
				AnimatorEditor[] editors = Utility.Convert<AnimatorEditor>(animatorEditors);
				foreach (AnimatorEditor animEditor in editors)
				{
					Collect(animEditor, collection);
				}
			}

			if (singleAssets != null)
			{
				double[] singles = Utility.Convert<double>(singleAssets);
				foreach (int pathID in singles)
				{
					Component asset = parser.Cabinet.LoadComponent(pathID);
					collection.Add(asset);
				}
			}

			collection.Remove(null);

			UnityParser p = new UnityParser(parser);
			p.Cabinet.Components.AddRange(collection);
			p.Cabinet.Components.Sort
			(
				delegate(Component a, Component b)
				{
					return a.pathID - b.pathID;
				}
			);
			if (parser.Cabinet.Bundle != null)
			{
				parser.Cabinet.Bundle.Clone(p.Cabinet);
			}
			return p;
		}

		static void Collect(AnimatorEditor editor, HashSet<Component> collection)
		{
			foreach (Transform frame in editor.Frames)
			{
				GameObject gameObj = frame.m_GameObject.instance;
				collection.Add(gameObj);
				foreach (var comp in gameObj.m_Component)
				{
					collection.Add(comp.Value.asset);
					if (comp.Value.asset.classID1 == UnityClassID.Animator)
					{
						Animator anim = (Animator)comp.Value.asset;
						if (anim.m_Avatar.instance != null)
						{
							collection.Add(anim.m_Avatar.instance);
						}
					}
				}
				if (frame.UnknownChilds != null)
				{
					foreach (PPtr<Transform> childPtr in frame.UnknownChilds)
					{
						if (!(childPtr.asset is Transform))
						{
							collection.Add(childPtr.asset);
							MultiLink mLink = childPtr.asset as MultiLink;
							if (mLink != null)
							{
								foreach (PPtr<Object> objPtr in mLink.Links)
								{
									collection.Add(objPtr.asset);
								}
							}
						}
					}
				}
			}
			foreach (MeshRenderer meshR in editor.Meshes)
			{
				collection.Add(Operations.GetMesh(meshR));
				foreach (PPtr<Material> matPtr in meshR.m_Materials)
				{
					if (matPtr.instance != null)
					{
						Material mat = matPtr.instance;
						collection.Add(mat);
						Collect(mat.m_Shader.instance, collection);
						foreach (var texPair in mat.m_SavedProperties.m_TexEnvs)
						{
							collection.Add(texPair.Value.m_Texture.asset);
						}
					}
				}
			}
		}

		static void Collect(Shader shader, HashSet<Component> collection)
		{
			if (shader != null)
			{
				collection.Add(shader);
				foreach (var dep in shader.m_Dependencies)
				{
					Collect(dep.instance, collection);
				}
			}
		}

		[Plugin]
		public static string AddFirstNewPathID(UnityParser originalParser, UnityParser modParser, string path)
		{
			int firstNewPathID = -1;
			for (int i = 0; i < modParser.Cabinet.Components.Count; i++)
			{
				int pathID = modParser.Cabinet.Components[i].pathID;
				Component comp = originalParser.Cabinet.FindComponent(pathID);
				if (comp == null)
				{
					firstNewPathID = pathID;
					break;
				}
			}
			return Path.GetDirectoryName(path) + @"\" + Path.GetFileNameWithoutExtension(path) + "-" + firstNewPathID + Path.GetExtension(path);
		}

		[Plugin]
		public static BackgroundWorker SaveMod(UnityParser modParser, string path, bool background)
		{
			return modParser.WriteArchive(path, false, null, background, true);
		}

		[Plugin]
		public static bool ApplyMod(UnityParser parser, UnityParser modParser, bool saveOriginals)
		{
			UnityParser originals = new UnityParser(parser);
			int firstNewPathID = -1;
			for (int i = 0; i < modParser.Cabinet.Components.Count; i++)
			{
				int pathID = modParser.Cabinet.Components[i].pathID;
				Component comp = parser.Cabinet.FindComponent(pathID);
				if (comp != null)
				{
					originals.Cabinet.Components.Add(comp);
				}
				else
				{
					if (firstNewPathID < 0)
					{
						firstNewPathID = pathID;
					}
					originals.Cabinet.Components.Add(modParser.Cabinet.Components[i]);
				}
			}
			Match m = Regex.Match(modParser.FilePath, @"(--*\d+)\.", RegexOptions.CultureInvariant);
			int idx = m.Success ? m.Groups[1].Index : modParser.FilePath.Length;
			string originalsName = modParser.FilePath.Substring(0, idx) + "-org-" + firstNewPathID + Path.GetExtension(parser.FilePath);
			if (File.Exists(originalsName))
			{
				Report.ReportLog(Path.GetFileName(modParser.FilePath) + " has already been applied.");
				return false;
			}
			if (saveOriginals)
			{
				originals.WriteArchive(originalsName, false, null, false, true);
			}

			bool success = true;
			using (Stream stream = File.OpenRead(modParser.FilePath))
			{
				m = Regex.Match(modParser.FilePath, @"-(-*\d+)\.", RegexOptions.CultureInvariant);
				if (!m.Success || !int.TryParse(m.Groups[1].Value, out firstNewPathID))
				{
					Report.ReportLog("Warning! Mod filename \"" + Path.GetFileName(modParser.FilePath) + "\" is malformed or lies in a folder with a confusing name.");
					firstNewPathID = -1;
				}
				List<KeyValuePair<string, AssetInfo>> srcContainer = null;
				for (int i = 0; i < modParser.Cabinet.Components.Count; i++)
				{
					Component src = modParser.Cabinet.Components[i];
					NotLoaded asset = src as NotLoaded;
					if (asset != null)
					{
						src = modParser.Cabinet.LoadComponent(stream, i, asset);
					}
					if (firstNewPathID == -1 || src.pathID < firstNewPathID)
					{
						Component dest = parser.Cabinet.FindComponent(src.pathID);
						if (dest.classID2 == src.classID2)
						{
							if (src.classID2 == UnityClassID.AssetBundle)
							{
								srcContainer = ((AssetBundle)src).m_Container;
							}
							else
							{
								parser.Cabinet.ReplaceSubfile(dest, src);
							}

							if (parser.Cabinet.Bundle != null)
							{
								switch (src.classID2)
								{
								case UnityClassID.Animator:
									parser.Cabinet.Bundle.RegisterForUpdate(((Animator)src).m_GameObject.asset);
									break;
								case UnityClassID.MeshRenderer:
								case UnityClassID.SkinnedMeshRenderer:
								case UnityClassID.Material:
								case UnityClassID.Shader:
								case UnityClassID.Texture2D:
									parser.Cabinet.Bundle.RegisterForUpdate(src);
									break;
								}
							}
						}
						else
						{
							Report.ReportLog("Unexpected asset in " + Path.GetFileName(parser.FilePath) + " with PathID=" + src.pathID + " was not replaced.");
							success = false;
						}
					}
					else
					{
						parser.Cabinet.ReplaceSubfile(-1, src, null);

						if (parser.Cabinet.Bundle != null)
						{
							switch (src.classID2)
							{
							case UnityClassID.TextAsset:
							case UnityClassID.Material:
							case UnityClassID.MonoBehaviour:
							case UnityClassID.Shader:
							case UnityClassID.Texture2D:
								parser.Cabinet.Bundle.AddComponent(src);
								break;
							}
						}
					}
				}
				if (parser.Cabinet.Bundle != null && modParser.Cabinet.Bundle != null)
				{
					for (int j = 0; j < srcContainer.Count; )
					{
						var firstEntry = srcContainer[j++];
						Component firstAsset = ((NotLoaded)firstEntry.Value.asset.asset).replacement;
						if (firstAsset == null)
						{
							continue;
						}
						if (parser.Cabinet.Bundle.FindComponent(firstEntry.Key, firstEntry.Value.asset.asset.classID2) == null)
						{
							List<Component> entries = new List<Component>();
							entries.Add(firstAsset);
							while (j < srcContainer.Count && srcContainer[j].Key == firstEntry.Key && srcContainer[j].Value.preloadIndex == firstEntry.Value.preloadIndex)
							{
								Component asset = ((NotLoaded)srcContainer[j++].Value.asset.asset).replacement;
								entries.Add(asset);
							}
							parser.Cabinet.Bundle.AddComponents(firstEntry.Key, entries);
						}
						else
						{
							parser.Cabinet.Bundle.RegisterForUpdate(firstAsset);
						}
					}

					Unity3dEditor editor = new Unity3dEditor(modParser);
					foreach (Animator vAnim in editor.VirtualAnimators)
					{
						Component gameObj = parser.Cabinet.FindComponent(vAnim.m_GameObject.asset.pathID);
						parser.Cabinet.Bundle.AddComponent(gameObj);
					}
				}
			}
			return success;
		}

		[Plugin]
		public static bool RemoveMod(UnityParser parser, UnityParser originalsParser, bool deleteOriginals)
		{
			Match m = Regex.Match(originalsParser.FilePath, @"-(-*\d+)\.", RegexOptions.CultureInvariant);
			int firstNewPathID = -1;
			if (!m.Success || !int.TryParse(m.Groups[1].Value, out firstNewPathID))
			{
				Report.ReportLog("Warning! Mod filename \"" + Path.GetFileName(originalsParser.FilePath) + "\" is malformed or lies in a folder with a confusing name.");
				firstNewPathID = -1;
			}
			bool error = false;
			for (int i = originalsParser.Cabinet.Components.Count - 1; i >= 0; i--)
			{
				int pathID = originalsParser.Cabinet.Components[i].pathID;
				Component comp = parser.Cabinet.FindComponent(pathID);
				if (comp != null)
				{
					if (comp.classID2 == originalsParser.Cabinet.Components[i].classID2)
					{
						if (firstNewPathID == -1 || pathID < firstNewPathID)
						{
							parser.Cabinet.ReplaceSubfile(comp, originalsParser.Cabinet.Components[i]);
						}
						else
						{
							if (parser.Cabinet.Components.IndexOf(comp) == parser.Cabinet.Components.Count - 1)
							{
								parser.Cabinet.RemoveSubfile(comp);
							}
							else
							{
								Report.ReportLog("Asset with PathID=" + pathID + " is not located at the end. Other mods will become unremovable when this asset would be removed.");
								error = true;
							}
						}
					}
					else
					{
						Report.ReportLog("Unexpected asset in " + Path.GetFileName(parser.FilePath) + " with PathID=" + pathID + " was not " + (firstNewPathID == -1 || pathID < firstNewPathID ? "replaced." : "removed."));
						error = true;
					}
				}
				else
				{
					Report.ReportLog("Asset with PathID=" + pathID + " is not present in " + Path.GetFileName(parser.FilePath));
					error = true;
				}
			}
			if (error)
			{
				Report.ReportLog(Path.GetFileName(originalsParser.FilePath) + " was partially removed. Saving " + Path.GetFileName(parser.FilePath) + " may corrupt the file.");
				return false;
			}
			if (deleteOriginals)
			{
				parser.DeleteModFiles.Add(originalsParser.Cabinet);
			}
			return true;
		}
	}
}
