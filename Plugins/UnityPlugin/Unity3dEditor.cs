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
		public HashSet<Animator> VirtualAnimators { get; protected set; }

		protected bool contentChanged = false;

		public HashSet<Component> Marked = new HashSet<Component>();
		private HashSet<string> msgFilter = new HashSet<string>();

		public Unity3dEditor(UnityParser parser, bool showContents)
		{
			Parser = parser;

			VirtualAnimators = new HashSet<Animator>();
			if (Parser.Cabinet.Bundle != null)
			{
				for (int i = 0; i < Parser.Cabinet.Bundle.m_Container.Count; i++)
				{
					AssetInfo info = Parser.Cabinet.Bundle.m_Container[i].Value;
					if (info.asset.m_PathID == 0)
					{
						string msg = "Invalid container entry for " + Parser.Cabinet.Bundle.m_Container[i].Key;
						if (msgFilter.Add(msg))
						{
							Report.ReportLog(msg);
						}
						continue;
					}
					if (info.asset.m_FileID == 0 && info.asset.asset.classID1 == UnityClassID.GameObject)
					{
						bool animatorFound = false;
						int nextPreloadIdx = info.preloadIndex + info.preloadSize;
						for (int j = info.preloadIndex; j < nextPreloadIdx; j++)
						{
							PPtr<Object> preTabEntry = Parser.Cabinet.Bundle.m_PreloadTable[j];
							if (preTabEntry.m_FileID == 0 && preTabEntry.asset.classID1 == UnityClassID.Animator)
							{
								animatorFound = true;
								break;
							}
						}
						if (!animatorFound)
						{
							Animator animator = new Animator(Parser.Cabinet, 0, 0, 0);
							animator.m_Avatar = new PPtr<Avatar>((Component)null);
							animator.m_GameObject = new PPtr<GameObject>(info.asset.asset);
							VirtualAnimators.Add(animator);
						}
					}
				}
			}

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

		[Plugin]
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
						case UnityClassID.Shader:
						case UnityClassID.Sprite:
						case UnityClassID.TextAsset:
						case UnityClassID.Texture2D:
							comp.Name = reader.ReadNameA4();
							break;
						case UnityClassID.MonoScript:
							comp.Name = comp.pathID + " / " + reader.ReadNameA4();
							break;
						case UnityClassID.Animator:
						case UnityClassID.EllipsoidParticleEmitter:
						case UnityClassID.Light:
						case UnityClassID.ParticleAnimator:
						case UnityClassID.ParticleRenderer:
						case UnityClassID.ParticleSystem:
						case UnityClassID.ParticleSystemRenderer:
							comp.Name = GetNameFromGameObject(filter, stream);
							break;
						default:
							if ((int)comp.classID1 <= -1 && comp.classID2 == UnityClassID.MonoBehaviour)
							{
								comp.Name = GetNameFromGameObject(filter, stream);
								if (comp.Name == null)
								{
									comp.Name = MonoBehaviour.LoadName(stream);
								}
							}
							break;
						case UnityClassID.MeshFilter:
						case UnityClassID.MeshRenderer:
						case UnityClassID.SkinnedMeshRenderer:
						case UnityClassID.Transform:
							if (!filter)
							{
								comp.Name = GetNameFromGameObject(true, stream);
							}
							break;
						case UnityClassID.GameObject:
							if (!filter || IsVirtualAnimator(comp))
							{
								comp.Name = GameObject.LoadName(stream);
							}
							break;
						}
					}
					assetNames[i] = comp.Name != null ? comp.Name : comp.pathID.ToString();
				}
			}
			return assetNames;
		}

		private string GetNameFromGameObject(bool filter, Stream stream)
		{
			long pos = stream.Position;
			PPtr<GameObject> gameObjPtr = Animator.LoadGameObject(stream);
			if (gameObjPtr.m_PathID == 0)
			{
				stream.Position = pos;
				return null;
			}
			NotLoaded asset = Parser.Cabinet.FindComponent(gameObjPtr.m_PathID);
			if (filter && asset.Name == null)
			{
				stream.Position = asset.offset;
				asset.Name = GameObject.LoadName(stream);
			}
			return asset.Name;
		}

		bool IsVirtualAnimator(NotLoaded gameObject)
		{
			foreach (Animator animator in VirtualAnimators)
			{
				if (animator.m_GameObject.asset == gameObject)
				{
					return true;
				}
			}
			return false;
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

		[Plugin]
		public Animator OpenAnimator(string name)
		{
			for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
			{
				Component subfile = Parser.Cabinet.Components[i];
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
					if (animatorComp.Name == name)
					{
						bool marked = Marked.Remove(animatorComp);
						try
						{
							using (Stream stream = File.OpenRead(Parser.FilePath))
							{
								return Parser.Cabinet.LoadComponent(stream, i, animatorComp);
							}
						}
						finally
						{
							if (marked)
							{
								Marked.Add(animatorComp.replacement);
							}
						}
					}
				}
			}
			return null;
		}

		[Plugin]
		public Animator OpenAnimator(int componentIndex)
		{
			Component subfile = Parser.Cabinet.Components[componentIndex];
			if (subfile is Animator)
			{
				return (Animator)subfile;
			}
			NotLoaded animatorComp = (NotLoaded)subfile;
			bool marked = Marked.Remove(animatorComp);
			try
			{
				using (Stream stream = File.OpenRead(Parser.FilePath))
				{
					return Parser.Cabinet.LoadComponent(stream, componentIndex, animatorComp);
				}
			}
			finally
			{
				if (marked)
				{
					Marked.Add(animatorComp.replacement);
				}
			}
		}

		[Plugin]
		public Animator OpenVirtualAnimator(int componentIndex)
		{
			Component gameObj = Parser.Cabinet.Components[componentIndex];
			foreach (Animator anim in VirtualAnimators)
			{
				if (anim.m_GameObject.asset == gameObj)
				{
					if (anim.m_GameObject.instance == null)
					{
						bool marked = Marked.Remove(anim.m_GameObject.asset);
						anim.m_GameObject = new PPtr<GameObject>
						(
							Parser.Cabinet.LoadComponent(anim.m_GameObject.asset.pathID)
						);
						//anim.RootTransform = anim.m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
						if (marked)
						{
							Marked.Add(anim.m_GameObject.instance);
						}
					}
					return anim;
				}
			}
			return null;
		}

		[Plugin]
		public void ExportMonoBehaviour(Component asset, string path)
		{
			MonoBehaviour mono = LoadWhenNeeded(asset);
			mono.Export(path);
		}

		private dynamic LoadWhenNeeded(Component asset)
		{
			if (asset is NotLoaded)
			{
				Component comp = Parser.Cabinet.FindComponent(asset.pathID);
				if (comp is NotLoaded)
				{
					asset = Parser.Cabinet.LoadComponent(asset.pathID);
				}
			}
			return asset;
		}

		[Plugin]
		public void ReplaceMonoBehaviour(string path)
		{
			MonoBehaviour m = MonoBehaviour.Import(path, Parser.Cabinet);
			if (m == null)
			{
				return;
			}

			for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
			{
				Component asset = Parser.Cabinet.Components[i];
				if (asset.classID1 == m.classID1)
				{
					MonoBehaviour mono = null;
					string name;
					if (asset is NotLoaded)
					{
						name = ((NotLoaded)asset).Name;
						if (name != m.m_Name)
						{
							continue;
						}
						mono = Parser.Cabinet.LoadComponent(asset.pathID);
					}
					else
					{
						mono = (MonoBehaviour)asset;
						name = mono.m_GameObject.instance != null ? mono.m_GameObject.instance.m_Name : mono.m_Name;
						if (name != m.m_Name)
						{
							continue;
						}
					}
					if (mono.Parser.type.Members.Count > 4 && mono.Parser.type.Members[4] is UClass &&
						((UClass)mono.Parser.type.Members[4]).ClassName == "Param" &&
						((UClass)mono.Parser.type.Members[4]).Name == "list")
					{
						Uarray monoArr = (Uarray)((UClass)mono.Parser.type.Members[4]).Members[0];
						Uarray mArr = (Uarray)((UClass)m.Parser.type.Members[4]).Members[0];
						monoArr.Value = mArr.Value;
						Changed = true;
					}
					return;
				}
			}
		}

		[Plugin]
		public void ExportTexture2D(Component asset, string path)
		{
			Texture2D tex = LoadWhenNeeded(asset);
			tex.Export(path);
		}

		[Plugin]
		public void ExportCubemap(Component asset, string path)
		{
			Cubemap tex = LoadWhenNeeded(asset);
			Report.ReportLog("Warning! Cubemap exported as normal texture!");
			tex.Export(path);
		}

		[Plugin]
		public void MergeTexture(string path)
		{
			ImportedTexture texture = new ImportedTexture(path);
			Operations.ReplaceTexture(Parser, texture);
			Changed = true;
		}

		[Plugin]
		public void ExportShader(Component asset, string path)
		{
			Shader shader = LoadWhenNeeded(asset);
			shader.Export(path);
		}

		[Plugin]
		public void ReplaceShader(string path)
		{
			Shader sh = Shader.Import(path);
			if (sh.m_Script.Length > 0)
			{
				for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
				{
					Component asset = Parser.Cabinet.Components[i];
					if (asset.classID1 == UnityClassID.Shader)
					{
						Shader shader = null;
						string name;
						if (asset is NotLoaded)
						{
							name = ((NotLoaded)asset).Name;
							if (name != sh.m_Name)
							{
								continue;
							}
							shader = Parser.Cabinet.LoadComponent(asset.pathID);
						}
						else
						{
							shader = (Shader)asset;
							name = shader.m_Name;
						}
						if (name == sh.m_Name)
						{
							shader.m_Script = sh.m_Script;
							Changed = true;
							return;
						}
					}
				}
			}
		}

		[Plugin]
		public void ExportTextAsset(Component asset, string path)
		{
			TextAsset text = LoadWhenNeeded(asset);
			text.Export(path);
		}

		[Plugin]
		public void ReplaceTextAsset(string path)
		{
			TextAsset ta = TextAsset.Import(path);
			if (ta.m_Script.Length > 0)
			{
				for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
				{
					Component asset = Parser.Cabinet.Components[i];
					if (asset.classID1 == UnityClassID.TextAsset)
					{
						TextAsset text = null;
						string name;
						if (asset is NotLoaded)
						{
							name = ((NotLoaded)asset).Name;
							if (name != ta.m_Name)
							{
								continue;
							}
							text = Parser.Cabinet.LoadComponent(asset.pathID);
						}
						else
						{
							text = (TextAsset)asset;
							name = text.m_Name;
						}
						if (name == ta.m_Name)
						{
							text.m_Script = text.m_Script.IndexOf('\r') == -1
								? ta.m_Script.Replace("\r", "")
								: ta.m_Script.IndexOf('\r') == -1
									? ta.m_Script.Replace("\n", "\r\n")
									: ta.m_Script;
							Changed = true;
							return;
						}
					}
				}
			}
		}

		[Plugin]
		public void ExportAudioClip(Component asset, string path)
		{
			AudioClip audio = LoadWhenNeeded(asset);
			audio.Export(path);
		}

		[Plugin]
		public void ReplaceAudioClip(string path)
		{
			AudioClip ac = AudioClip.Import(path);
			if (ac.m_AudioData.Length > 0)
			{
				for (int i = 0; i < Parser.Cabinet.Components.Count; i++)
				{
					Component asset = Parser.Cabinet.Components[i];
					if (asset.classID1 == UnityClassID.AudioClip)
					{
						AudioClip audio = null;
						string name;
						if (asset is NotLoaded)
						{
							name = ((NotLoaded)asset).Name;
							if (name != ac.m_Name)
							{
								continue;
							}
							audio = Parser.Cabinet.LoadComponent(asset.pathID);
						}
						else
						{
							audio = (AudioClip)asset;
							name = audio.m_Name;
						}
						if (name == ac.m_Name)
						{
							audio.m_AudioData = ac.m_AudioData;
							Changed = true;
							return;
						}
					}
				}
			}
		}

		[Plugin]
		public void MarkAsset(int componentIdx)
		{
			Marked.Add(Parser.Cabinet.Components[componentIdx]);
		}

		[Plugin]
		public void UnmarkAsset(int componentIdx)
		{
			Marked.Remove(Parser.Cabinet.Components[componentIdx]);
		}

		[Plugin]
		public void PasteAllMarked()
		{
			int components = Parser.Cabinet.Components.Count;
			try
			{
				Parser.Cabinet.BeginLoadingSkippedComponents();
				foreach (object obj in Gui.Scripting.Variables.Values)
				{
					if (obj is Unity3dEditor && (Unity3dEditor)obj != this)
					{
						Unity3dEditor editor = (Unity3dEditor)obj;
						HashSet<Component> remove = new HashSet<Component>();
						HashSet<Component> replace = new HashSet<Component>();
						foreach (Component asset in editor.Marked)
						{
							Component loaded;
							if (asset is NotLoaded)
							{
								loaded = editor.Parser.Cabinet.LoadComponent(asset.pathID);
								remove.Add(asset);
								replace.Add(loaded);
							}
							else
							{
								loaded = asset;
							}
							switch (asset.classID2)
							{
							case UnityClassID.Texture2D:
								Texture2D tex = (Texture2D)loaded;
								tex.Clone(Parser.Cabinet);
								break;
							case UnityClassID.Cubemap:
								Cubemap cubemap = (Cubemap)loaded;
								cubemap.Clone(Parser.Cabinet);
								break;
							case UnityClassID.Material:
								Material mat = (Material)loaded;
								mat.Clone(Parser.Cabinet);
								break;
							case UnityClassID.Mesh:
								Mesh mesh = (Mesh)loaded;
								mesh.Clone(Parser.Cabinet);
								break;
							case UnityClassID.Shader:
								Shader shader = (Shader)loaded;
								shader.Clone(Parser.Cabinet);
								break;
							case UnityClassID.Sprite:
								Sprite sprite = (Sprite)loaded;
								sprite.Clone(Parser.Cabinet);
								break;
							case UnityClassID.Animator:
								Parser.Cabinet.MergeTypeDefinition(loaded.file, UnityClassID.GameObject);
								Parser.Cabinet.MergeTypeDefinition(loaded.file, UnityClassID.Transform);
								Animator anim = (Animator)loaded;
								anim.m_GameObject.instance.Clone(Parser.Cabinet);
								break;
							case UnityClassID.GameObject:
								Parser.Cabinet.MergeTypeDefinition(loaded.file, UnityClassID.GameObject);
								Parser.Cabinet.MergeTypeDefinition(loaded.file, UnityClassID.Transform);
								GameObject gameObj = (GameObject)loaded;
								Component clone = gameObj.Clone(Parser.Cabinet);

								Animator vAnim = new Animator(Parser.Cabinet, 0, 0, 0);
								vAnim.m_Avatar = new PPtr<Avatar>((Component)null);
								vAnim.m_GameObject = new PPtr<GameObject>(clone);
								if (loaded.file.Bundle.numContainerEntries(gameObj.m_Name, UnityClassID.GameObject) > 1)
								{
									Report.ReportLog("Warning! Animator " + gameObj.m_Name + " has multiple entries in the AssetBundle's Container.");
								}
								vAnim.file.Bundle.AddComponent(vAnim.m_GameObject.instance.m_Name, clone);
								VirtualAnimators.Add(vAnim);
								if (loaded != asset)
								{
									foreach (Animator a in editor.VirtualAnimators)
									{
										if (a.m_GameObject.asset == asset)
										{
											a.m_GameObject = new PPtr<GameObject>(loaded);
											break;
										}
									}
								}
								break;
							case UnityClassID.Avatar:
								Avatar avatar = (Avatar)loaded;
								avatar.Clone(Parser.Cabinet);
								break;
							case UnityClassID.MonoBehaviour:
								MonoBehaviour monoB = (MonoBehaviour)loaded;
								monoB.Clone(Parser.Cabinet);
								break;
							case UnityClassID.TextAsset:
								TextAsset text = (TextAsset)loaded;
								text.Clone(Parser.Cabinet);
								break;
							}
						}

						do
						{
							HashSet<Tuple<Component, Component>> loopSet = new HashSet<Tuple<Component, Component>>(AssetCabinet.IncompleteClones);
							AssetCabinet.IncompleteClones.Clear();
							foreach (var pair in loopSet)
							{
								Component src = pair.Item1;
								Component dest = pair.Item2;
								Type t = src.GetType();
								MethodInfo info = t.GetMethod("CopyTo", new Type[] { t });
								info.Invoke(src, new object[] { dest });
							}
						}
						while (AssetCabinet.IncompleteClones.Count > 0);

						foreach (Component asset in remove)
						{
							editor.Marked.Remove(asset);
						}
						foreach (Component asset in replace)
						{
							editor.Marked.Add(asset);
						}
					}
				}
			}
			finally
			{
				Parser.Cabinet.EndLoadingSkippedComponents();
				if (components != Parser.Cabinet.Components.Count)
				{
					Changed = true;
				}
			}
		}

		[Plugin]
		public bool SetAssetName(int componentIndex, string name)
		{
			Component asset = Parser.Cabinet.Components[componentIndex];
			if (asset is NotLoaded)
			{
				NotLoaded notLoaded = (NotLoaded)asset;
				asset = Parser.Cabinet.LoadComponent(asset.pathID);
				if (asset is NotLoaded)
				{
					return false;
				}
				if (asset.classID1 == UnityClassID.GameObject)
				{
					foreach (Animator anim in VirtualAnimators)
					{
						if (anim.m_GameObject.asset == notLoaded)
						{
							anim.m_GameObject = new PPtr<GameObject>(asset);
							break;
						}
					}
				}
			}
			Type t = asset.GetType();
			PropertyInfo info = t.GetProperty("m_GameObject");
			if (info != null)
			{
				PPtr<GameObject> gameObjPtr = info.GetValue(asset, null) as PPtr<GameObject>;
				if (gameObjPtr != null && gameObjPtr.instance != null)
				{
					gameObjPtr.instance.m_Name = name;
					return true;
				}
			}
			info = t.GetProperty("m_Name");
			if (info != null)
			{
				info.SetValue(asset, name, null);
				return true;
			}
			return false;
		}
	}
}
