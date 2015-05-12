using System;
using System.IO;

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
	}
}
