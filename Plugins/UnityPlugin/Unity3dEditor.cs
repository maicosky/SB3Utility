using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;

using SB3Utility;

namespace UnityPlugin
{
	[Plugin]
	public class Unity3dEditor : EditedContent
	{
		public UnityParser Parser { get; protected set; }

		protected bool contentChanged = false;

		public Unity3dEditor(UnityParser parser, bool showContents)
		{
			Parser = parser;

			if (showContents)
			{
				GetAssetNames(false);
			}
		}

		public string[] GetAssetNames(bool filter)
		{
			string[] assetNames = new string[Parser.Cabinet.Components.Count];
			using (BinaryReader reader = new BinaryReader(File.OpenRead(Parser.FilePath)))
			{
				Stream stream = reader.BaseStream;
				for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
				{
					NotLoaded comp = Parser.Cabinet.Components[i] as NotLoaded;
					if (comp == null)
					{
						Component subfile = Parser.Cabinet.Components[i];
						assetNames[i] = ToString(subfile);
						continue;
					}
					if (comp.Name == null)
					{
						stream.Position = comp.offset;
						switch (comp.classID1)
						{
						case UnityClassID.AssetBundle:
						case UnityClassID.Avatar:
						case UnityClassID.Mesh:
							if (!filter)
							{
								comp.Name = reader.ReadNameA4();
							}
							break;
						case UnityClassID.AnimationClip:
						case UnityClassID.Cubemap:
						case UnityClassID.Material:
						case UnityClassID.MonoScript:
						case UnityClassID.Shader:
						case UnityClassID.Texture2D:
							comp.Name = reader.ReadNameA4();
							break;
						case UnityClassID.Animator:
						case UnityClassID.EllipsoidParticleEmitter:
						case UnityClassID.ParticleAnimator:
						case UnityClassID.ParticleRenderer:
							PPtr<GameObject> gameObjPtr = Animator.LoadGameObject(stream);
							NotLoaded asset = (NotLoaded)Parser.Cabinet.FindComponent(gameObjPtr.m_PathID);
							if (filter)
							{
								stream.Position = asset.offset;
								asset.Name = GameObject.LoadName(stream);
							}
							comp.Name = asset.Name;
							break;
						case UnityClassID.MeshFilter:
						case UnityClassID.MeshRenderer:
						case UnityClassID.SkinnedMeshRenderer:
						case UnityClassID.Transform:
							if (!filter)
							{
								gameObjPtr = Animator.LoadGameObject(stream);
								comp.Name = ((NotLoaded)Parser.Cabinet.FindComponent(gameObjPtr.m_PathID)).Name;
							}
							break;
						case UnityClassID.GameObject:
							if (!filter)
							{
								comp.Name = GameObject.LoadName(stream);
							}
							break;
						default:
							if (comp.classID1 == (UnityClassID)(int)-1 && comp.classID2 == UnityClassID.MonoBehaviour)
							{
								comp.Name = MonoBehaviour.LoadName(stream);
							}
							break;
						}
					}
					assetNames[i] = comp.Name != null ? comp.Name : comp.pathID.ToString();
				}
			}
			return assetNames;
		}

		public Unity3dEditor(UnityParser parser) : this(parser, false) { }
		public Unity3dEditor(string path) : this(new UnityParser(path), true) { }

		public static string ToString(Component subfile)
		{
			Type t = subfile.GetType();
			PropertyInfo info = t.GetProperty("m_Name");
			if (info != null)
			{
				return info.GetValue(subfile, null).ToString();
			}
			else
			{
				info = t.GetProperty("m_GameObject");
				if (info != null)
				{
					PPtr<GameObject> gameObjPtr = info.GetValue(subfile, null) as PPtr<GameObject>;
					if (gameObjPtr != null)
					{
						return gameObjPtr.instance.m_Name;
					}
					else
					{
						GameObject gameObj = info.GetValue(subfile, null) as GameObject;
						if (gameObj != null)
						{
							return gameObj.m_Name;
						}
						throw new Exception("What reference is this!? " + subfile.pathID + " " + subfile.classID1);
					}
				}
				throw new Exception("Neither m_Name nor m_GameObject member " + subfile.pathID + " " + subfile.classID1);
			}
		}

		public bool Changed
		{
			get { return contentChanged; }
			set { contentChanged = value; }
		}

		[Plugin]
		public BackgroundWorker SaveUnity3d(bool keepBackup, string backupExtension, bool background)
		{
			return SaveUnity3d(Parser.FilePath, keepBackup, backupExtension, background);
		}

		[Plugin]
		public BackgroundWorker SaveUnity3d(string path, bool keepBackup, string backupExtension, bool background)
		{
			return Parser.WriteArchive(path, keepBackup, backupExtension, background);
		}

		[Plugin]
		public Stream ReadSubfile(int pathID)
		{
			Component subfile = Parser.Cabinet.FindComponent(pathID);
			if (subfile == null)
			{
				throw new Exception("Couldn't find the subfile " + pathID);
			}

			var readFile = subfile as IReadFile;
			if (readFile == null)
			{
				throw new Exception("The subfile " + pathID + " isn't readable");
			}

			return readFile.CreateReadStream();
		}

		[Plugin]
		public void MergeMaterial(ImportedMaterial material)
		{
			Operations.ReplaceMaterial(Parser, material);
			Changed = true;
		}
	}
}
