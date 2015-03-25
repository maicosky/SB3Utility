using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

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
				string[] names = GetAssetNames(false);
				for (int i = 0; i < names.Length; i++)
				{
					Component asset = Parser.Cabinet.Components[i];
					Console.WriteLine("PathID=" + asset.pathID.ToString("D") + " id1=" + (int)asset.classID1 + "/" + asset.classID1 + " id2=" + asset.classID2 + " " + names[i]);
				}
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
						assetNames[i] = AssetCabinet.ToString(subfile);
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
						case UnityClassID.AudioClip:
						case UnityClassID.AnimationClip:
						case UnityClassID.AnimatorController:
						case UnityClassID.Cubemap:
						case UnityClassID.Material:
						case UnityClassID.MonoScript:
						case UnityClassID.Shader:
						case UnityClassID.TextAsset:
						case UnityClassID.Texture2D:
							comp.Name = reader.ReadNameA4();
							break;
						case UnityClassID.Animator:
						case UnityClassID.EllipsoidParticleEmitter:
						case UnityClassID.ParticleAnimator:
						case UnityClassID.ParticleRenderer:
							PPtr<GameObject> gameObjPtr = Animator.LoadGameObject(stream);
							NotLoaded asset = Parser.Cabinet.FindComponent(gameObjPtr.m_PathID);
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
								asset = Parser.Cabinet.FindComponent(gameObjPtr.m_PathID);
								if (asset.Name == null)
								{
									stream.Position = asset.offset;
									asset.Name = GameObject.LoadName(stream);
								}
								comp.Name = asset.Name;
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
