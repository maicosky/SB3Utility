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

		public AssetBundle AssetBundle { get; protected set; }
		public HashSet<Transform> RootFrames { get; protected set; }
		public List<Transform> Frames { get; protected set; }
		public List<SkinnedMeshRenderer> Meshes { get; protected set; }
		public List<Material> Materials { get; protected set; }
		public List<Texture2D> Textures { get; protected set; }

		protected bool contentChanged = false;

		public Unity3dEditor(UnityParser parser, bool showContents)
		{
			Parser = parser;

			/*RootFrames = new HashSet<Transform>();
			for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
			{
				Component comp = Parser.Cabinet.Components[i];
				if (comp is NotLoaded)
				{
					switch (comp.classID1)
					{
					case UnityClassID.Transform:
						Transform trans = Parser.Cabinet.LoadComponent(comp.pathID);
						while (trans.m_Father != null)
						{
							trans = trans.m_Father;
						}
						RootFrames.Add(trans);
						break;
					}
				}
			}*/

			if (showContents)
			{
				GetAssetNames(false);
			}

			/*for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
			{
				Component comp = Parser.Cabinet.Components[i];
				if (comp.classID1 == UnityClassID.AssetBundle)
				{
					if (comp is NotLoaded)
					{
						AssetBundle = Parser.Cabinet.LoadComponent(comp.pathID);
					}
					break;
				}
			}*/

			//InitLists();
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
						case UnityClassID.Material:
						case UnityClassID.MonoScript:
						case UnityClassID.Shader:
						case UnityClassID.Texture2D:
							comp.Name = reader.ReadNameA4();
							break;
						case UnityClassID.Animator:
							PPtr<GameObject> gameObjPtr = Animator.LoadGameObject(stream);
							if (filter)
							{
								NotLoaded asset = (NotLoaded)Parser.Cabinet.FindComponent(gameObjPtr.m_PathID);
								stream.Position = asset.offset;
								comp.Name = asset.Name = GameObject.LoadName(stream);
							}
							else
							{
								comp.Name = ((NotLoaded)Parser.Cabinet.FindComponent(gameObjPtr.m_PathID)).Name;
							}
							break;
						case UnityClassID.Transform:
						case UnityClassID.SkinnedMeshRenderer:
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
						if (comp.Name == null)
						{
							comp.Name = comp.pathID.ToString();
						}
					}
					assetNames[i] = comp.Name;
				}
			}
			return assetNames;
		}

		private void InitLists()
		{
			Frames = new List<Transform>();
			Meshes = new List<SkinnedMeshRenderer>();
			Materials = new List<Material>();
			Textures = new List<Texture2D>();
			for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
			{
				switch (Parser.Cabinet.Components[i].classID1)
				{
				case UnityClassID.Material:
					Material mat = Parser.Cabinet.LoadComponent(Parser.Cabinet.Components[i].pathID);
					Materials.Add(mat);

					Parser.Cabinet.Components.RemoveAt(i--);
					break;
				case UnityClassID.Texture2D:
					Texture2D tex = Parser.Cabinet.LoadComponent(Parser.Cabinet.Components[i].pathID);
					Textures.Add(tex);

					Parser.Cabinet.Components.RemoveAt(i--);
					break;
				case UnityClassID.Transform:
					Transform trans = (Transform)Parser.Cabinet.Components[i];
					Frames.Add(trans);

					Parser.Cabinet.Components.RemoveAt(i--);
					break;
				case UnityClassID.SkinnedMeshRenderer:
					SkinnedMeshRenderer sMesh = Parser.Cabinet.LoadComponent(Parser.Cabinet.Components[i].pathID);
					Meshes.Add(sMesh);

					Parser.Cabinet.Components.RemoveAt(i--);
					break;
				}
			}
			for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
			{
				switch (Parser.Cabinet.Components[i].classID1)
				{
				case UnityClassID.Mesh:
				case UnityClassID.GameObject:

					Parser.Cabinet.Components.RemoveAt(i--);
					break;
				}
			}
		}

		private void InitFrames(HashSet<Transform> roots)
		{
			foreach (Transform root in roots)
			{
				InitFrames(root);
			}
		}

		private void InitFrames(Transform trans)
		{
			foreach (Transform child in trans)
			{
				InitFrames(child);
			}

			Frames.Add(trans);
			if (trans.m_GameObject.instance.m_Component.Count > 1)
			{
				foreach (var compPair in trans.m_GameObject.instance.m_Component)
				{
					if (compPair.Key == UnityClassID.SkinnedMeshRenderer)
					{
						Meshes.Add((SkinnedMeshRenderer)compPair.Value.asset);
					}
				}
			}
		}

		public Unity3dEditor(UnityParser parser) : this(parser, false) { }
		public Unity3dEditor(string path) : this(new UnityParser(path), true) { }

		private string ToString(Component subfile)
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
		public void RemoveSubfileFromAssetBundle(int pathID)
		{
			//AssetBundle.DeleteComponent(pathID);
		}

		public int FindSubfile(string Name, UnityClassID classID)
		{
			using (BinaryReader reader = new BinaryReader(File.OpenRead(Parser.FilePath)))
			{
				Stream stream = reader.BaseStream;
				for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
				{
					if (Parser.Cabinet.Components[i].classID1 == classID)
					{
						NotLoaded asset = Parser.Cabinet.Components[i] as NotLoaded;
						if (asset != null)
						{
							if (asset.Name == Name)
							{
								return i;
							}
							stream.Position = asset.offset;
							asset.Name = reader.ReadNameA4();
							if (asset.Name == Name)
							{
								return i;
							}
						}
						else
						{
							Type t = Parser.Cabinet.Components[i].GetType();
							PropertyInfo info = t.GetProperty("m_Name");
							if (info != null)
							{
								if (info.GetValue(Parser.Cabinet.Components[i], null).ToString() == Name)
								{
									return i;
								}
							}
							else
							{
								info = t.GetProperty("m_GameObject");
								if (info != null)
								{
									GameObject gameObj = (GameObject)info.GetValue(Parser.Cabinet.Components[i], null);
									if (gameObj.m_Name == Name)
									{
										return i;
									}
								}
							}
						}
					}
				}
				return -1;
			}
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
		public Transform FindTransform(string rootName, string name)
		{
			Transform root = null;
			foreach (Transform trans in RootFrames)
			{
				if (trans.m_GameObject.instance.m_Name == rootName)
				{
					root = trans;
					break;
				}
			}
			if (root == null)
			{
				return null;
			}
			return FindTransform(root, name);
		}

		public Transform FindTransform(Transform trans, string name)
		{
			if (trans.m_GameObject.instance.m_Name == name)
			{
				return trans;
			}
			foreach (Transform child in trans)
			{
				Transform found = FindTransform(child, name);
				if (found != null)
				{
					return found;
				}
			}
			return null;
		}

		[Plugin]
		public SkinnedMeshRenderer FindSkinnedMeshRenderer(int pathID)
		{
			foreach (SkinnedMeshRenderer sMesh in Meshes)
			{
				if (sMesh.pathID == pathID)
				{
					return sMesh;
				}
			}
			return null;
		}

		[Plugin]
		public SkinnedMeshRenderer FindSkinnedMeshRenderer(string rootFrame, string name)
		{
			Transform meshFrame = FindTransform(rootFrame, name);
			if (meshFrame == null)
			{
				return null;
			}
			foreach (var compPair in meshFrame.m_GameObject.instance.m_Component)
			{
				if (compPair.Key == UnityClassID.SkinnedMeshRenderer)
				{
					return (SkinnedMeshRenderer)compPair.Value.asset;
				}
			}
			return null;
		}

		[Plugin]
		public void RehashBone(string boneName, int newHash)
		{
			uint oldHash = ReplaceHashInAvatar(boneName, (uint)newHash);
			if (oldHash > 0)
			{
				Changed = true;
				for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
				{
					Component asset = Parser.Cabinet.Components[i];
					if (asset.classID1 == UnityClassID.SkinnedMeshRenderer)
					{
						SkinnedMeshRenderer smr = Parser.Cabinet.LoadComponent(asset.pathID);
						Mesh mesh = smr.m_Mesh.instance;
						if (mesh != null)
						{
							for (int j = 0; j < mesh.m_BoneNameHashes.Count; j++)
							{
								uint hash = mesh.m_BoneNameHashes[j];
								if (hash == oldHash)
								{
									mesh.m_BoneNameHashes[j] = (uint)newHash;
									break;
								}
							}
						}
					}
				}
			}
		}

		uint ReplaceHashInAvatar(string boneName, uint newHash)
		{
			for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
			{
				Component asset = Parser.Cabinet.Components[i];
				if (asset.classID1 == UnityClassID.Avatar)
				{
					Avatar avatar = Parser.Cabinet.LoadComponent(asset.pathID);
					for (int j = 0; j < avatar.m_TOS.Count; j++)
					{
						var boneHash = avatar.m_TOS[j];
						if (boneHash.Value.Substring(boneHash.Value.LastIndexOf('/') + 1) == boneName)
						{
							uint oldHash = boneHash.Key;
							boneHash = new KeyValuePair<uint, string>((uint)newHash, boneHash.Value);
							avatar.m_TOS.RemoveAt(j);
							avatar.m_TOS.Insert(j, boneHash);
							return oldHash;
						}
					}
				}
			}
			return 0;
		}

		[Plugin]
		public void MergeMaterial(ImportedMaterial material)
		{
			Operations.ReplaceMaterial(Parser, material);
			Changed = true;
		}
	}
}
