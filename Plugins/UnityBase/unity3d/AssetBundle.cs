using System;
using System.Collections.Generic;
using System.IO;

using SB3Utility;

namespace UnityPlugin
{
	public class AssetInfo : IObjInfo
	{
		public int preloadIndex { get; set; }
		public int preloadSize { get; set; }
		public PPtr<Object> asset { get; set; }

		private AssetCabinet file;

		public AssetInfo(AssetCabinet file)
		{
			this.file = file;
		}

		public AssetInfo(AssetCabinet file, Stream stream)
		{
			this.file = file;
			BinaryReader reader = new BinaryReader(stream);
			preloadIndex = reader.ReadInt32();
			preloadSize = reader.ReadInt32();
			PPtr<Object> objPtr = new PPtr<Object>(stream);
			if (objPtr.m_FileID == 0 && objPtr.m_PathID != 0)
			{
				Component comp = file.FindComponent(objPtr.m_PathID, false);
				if (comp == null)
				{
					comp = new NotLoaded(file, objPtr.m_PathID, 0, 0);
				}
				asset = new PPtr<Object>(comp);
			}
			else
			{
				asset = objPtr;
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(preloadIndex);
			writer.Write(preloadSize);
			asset.WriteTo(stream);
		}
	}

	public class AssetBundleScriptInfo : IObjInfo
	{
		public string className { get; set; }
		public string nameSpace { get; set; }
		public string assemblyName { get; set; }
		public uint hash { get; set; }

		public AssetBundleScriptInfo(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			className = reader.ReadNameA4();
			nameSpace = reader.ReadNameA4();
			assemblyName = reader.ReadNameA4();
			hash = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(className);
			writer.WriteNameA4(nameSpace);
			writer.WriteNameA4(assemblyName);
			writer.Write(hash);
		}
	}

	public class AssetBundle : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public List<PPtr<Object>> m_PreloadTable { get; set; }
		public List<KeyValuePair<string, AssetInfo>> m_Container { get; set; }
		public AssetInfo m_MainAsset { get; set; }
		public AssetBundleScriptInfo[] m_ScriptCompatibility { get; set; }
		public KeyValuePair<int, uint>[] m_ClassCompatibility { get; set; }
		public uint m_RuntimeCompatibility { get; set; }

		private HashSet<Component> NeedsUpdate;
		private int uniquePreloadIdx = 0;

		public AssetBundle(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;

			NeedsUpdate = new HashSet<Component>();
		}

		public AssetBundle(AssetCabinet file) :
			this(file, 0, UnityClassID.AssetBundle, UnityClassID.AssetBundle)
		{
			file.Components.Insert(0, this);
			file.Bundle = this;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();

			int numObjects = reader.ReadInt32();
			m_PreloadTable = new List<PPtr<Object>>(numObjects);
			for (int i = 0; i < numObjects; i++)
			{
				PPtr<Object> objPtr = new PPtr<Object>(stream);
				if (objPtr.m_FileID == 0)
				{
					Component comp = file.FindComponent(objPtr.m_PathID, false);
					if (comp == null)
					{
						comp = new NotLoaded(file, objPtr.m_PathID, 0, 0);
					}
					objPtr = new PPtr<Object>(comp);
				}
				m_PreloadTable.Add(objPtr);
			}

			int numContainerEntries = reader.ReadInt32();
			m_Container = new List<KeyValuePair<string, AssetInfo>>(numContainerEntries);
			for (int i = 0; i < numContainerEntries; i++)
			{
				m_Container.Add
				(
					new KeyValuePair<string, AssetInfo>
					(
						reader.ReadNameA4(), new AssetInfo(file, stream)
					)
				);
			}

			m_MainAsset = new AssetInfo(file, stream);

			int numScriptComps = reader.ReadInt32();
			m_ScriptCompatibility = new AssetBundleScriptInfo[numScriptComps];
			for (int i = 0; i < numScriptComps; i++)
			{
				m_ScriptCompatibility[i] = new AssetBundleScriptInfo(stream);
			}

			int numClassComps = reader.ReadInt32();
			m_ClassCompatibility = new KeyValuePair<int, uint>[numClassComps];
			for (int i = 0; i < numClassComps; i++)
			{
				m_ClassCompatibility[i] = new KeyValuePair<int, uint>
				(
					reader.ReadInt32(), reader.ReadUInt32()
				);
			}

			m_RuntimeCompatibility = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			UpdateComponents();

			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);

			writer.Write(m_PreloadTable.Count);
			for (int i = 0; i < m_PreloadTable.Count; i++)
			{
				m_PreloadTable[i].WriteTo(stream);
			}

			writer.Write(m_Container.Count);
			for (int i = 0; i < m_Container.Count; i++)
			{
				writer.WriteNameA4(m_Container[i].Key);
				m_Container[i].Value.WriteTo(stream);
			}

			m_MainAsset.WriteTo(stream);

			writer.Write(m_ScriptCompatibility.Length);
			for (int i = 0; i < m_ScriptCompatibility.Length; i++)
			{
				m_ScriptCompatibility[i].WriteTo(stream);
			}

			writer.Write(m_ClassCompatibility.Length);
			for (int i = 0; i < m_ClassCompatibility.Length; i++)
			{
				writer.Write(m_ClassCompatibility[i].Key);
				writer.Write(m_ClassCompatibility[i].Value);
			}

			writer.Write(m_RuntimeCompatibility);
		}

		public AssetBundle Clone(AssetCabinet file)
		{
			if (file.Bundle != null)
			{
				return file.Bundle;
			}

			file.MergeTypeDefinition(this.file, UnityClassID.AssetBundle);
			AssetBundle clone = new AssetBundle(file);
			clone.pathID = pathID;
			clone.m_Name = m_Name;
			clone.m_PreloadTable = new List<PPtr<Object>>(m_PreloadTable.Count);
			clone.m_PreloadTable.AddRange(m_PreloadTable);
			clone.m_Container = new List<KeyValuePair<string, AssetInfo>>(file.Components.Count);
			for (int i = 0; i < m_Container.Count; i++)
			{
				var containerEntry = m_Container[i];
				bool keepKey = true;
				int endIdx = containerEntry.Value.preloadIndex + containerEntry.Value.preloadSize;
				for (int j = containerEntry.Value.preloadIndex; j < endIdx; j++)
				{
					if (((NotLoaded)containerEntry.Value.asset.asset).replacement == null)
					{
						keepKey = false;
						break;
					}
				}
				do
				{
					if (keepKey)
					{
						clone.m_Container.Add(m_Container[i]);
					}
				} while (++i < m_Container.Count && containerEntry.Key == m_Container[i].Key);
			}

			clone.m_MainAsset = new AssetInfo(file);
			clone.m_MainAsset.asset = new PPtr<Object>((Component)null);

			clone.m_ScriptCompatibility = (AssetBundleScriptInfo[])m_ScriptCompatibility.Clone();
			clone.m_ClassCompatibility = (KeyValuePair<int, uint>[])m_ClassCompatibility.Clone();
			clone.m_RuntimeCompatibility = m_RuntimeCompatibility;
			return clone;
		}

		public Component FindComponent(string name, UnityClassID cls)
		{
			string lName = name.ToLower();
			for (int i = 0; i < m_Container.Count; i++)
			{
				if (m_Container[i].Key == lName && m_Container[i].Value.asset.asset != null && m_Container[i].Value.asset.asset.classID2 == cls)
				{
					return m_Container[i].Value.asset.asset;
				}
			}
			return null;
		}

		public int numContainerEntries(string name, UnityClassID cls)
		{
			string lName = name.ToLower();
			for (int i = 0; i < m_Container.Count; i++)
			{
				if (m_Container[i].Key == lName && m_Container[i].Value.asset.asset != null && m_Container[i].Value.asset.asset.classID2 == cls)
				{
					int j = i;
					while (++j < m_Container.Count && m_Container[j].Key == lName && m_Container[j].Value.preloadIndex == m_Container[i].Value.preloadIndex)
						;
					return j - i;
				}
			}
			return 0;
		}

		public void AddComponent(Component asset)
		{
			AddComponent(AssetCabinet.ToString(asset), asset);
		}

		public void AddComponent(string name, Component asset)
		{
			AssetInfo info = new AssetInfo(file);
			info.preloadIndex = --uniquePreloadIdx;
			info.preloadSize = 0;
			info.asset = new PPtr<Object>(asset);
			string key = name.ToLower();
			int idx;
			for (idx = 0; idx < m_Container.Count; idx++)
			{
				if (m_Container[idx].Key.CompareTo(key) >= 0)
				{
					break;
				}
			}
			m_Container.Insert(idx, new KeyValuePair<string, AssetInfo>(key, info));

			RegisterForUpdate(asset);
		}

		public void AddComponents(string name, List<Component> assets)
		{
			string key = name.ToLower();
			int idx;
			for (idx = 0; idx < m_Container.Count; idx++)
			{
				if (m_Container[idx].Key.CompareTo(key) >= 0)
				{
					break;
				}
			}
			--uniquePreloadIdx;
			for (int i = 0; i < assets.Count; i++)
			{
				Component asset = assets[i];
				AssetInfo info = new AssetInfo(file);
				info.preloadIndex = uniquePreloadIdx;
				info.preloadSize = 0;
				info.asset = new PPtr<Object>(asset);
				m_Container.Insert(idx++, new KeyValuePair<string, AssetInfo>(key, info));
			}

			RegisterForUpdate(assets[0]);
		}

		public void AppendComponent(string name, UnityClassID cls, Component asset)
		{
			string key = name.ToLower();
			for (int idx = 0; idx < m_Container.Count; idx++)
			{
				int cmp = m_Container[idx].Key.CompareTo(key);
				if (cmp == 0)
				{
					while (m_Container[idx].Value.asset.asset.classID2 != cls)
					{
						if (++idx >= m_Container.Count)
						{
							return;
						}
						cmp = m_Container[idx].Key.CompareTo(key);
						if (cmp != 0)
						{
							return;
						}
					}

					AssetInfo info = new AssetInfo(file);
					info.preloadIndex = m_Container[idx].Value.preloadIndex;
					info.preloadSize = 0;
					info.asset = new PPtr<Object>(asset);
					
					while (++idx < m_Container.Count && m_Container[idx].Value.preloadIndex == info.preloadIndex)
					{
					}
					m_Container.Insert(idx, new KeyValuePair<string, AssetInfo>(key, info));
					return;
				}
				else if (cmp > 0)
				{
					return;
				}
			}
		}

		public void DeleteComponent(Component asset)
		{
			for (int i = 0; i < m_Container.Count; i++)
			{
				if (m_Container[i].Value.asset.asset is NotLoaded &&
						(((NotLoaded)m_Container[i].Value.asset.asset).replacement == asset
						|| m_Container[i].Value.asset.asset.pathID == asset.pathID)
					|| m_Container[i].Value.asset.asset == asset)
				{
					m_Container.RemoveAt(i--);
				}
			}

			RegisterForUpdate(asset);
		}

		public void ReplaceComponent(Component oldAsset, Component newAsset)
		{
			for (int i = 0; i < m_PreloadTable.Count; i++)
			{
				if (m_PreloadTable[i] != null &&
					(m_PreloadTable[i].asset is NotLoaded && 
						(((NotLoaded)m_PreloadTable[i].asset).replacement == oldAsset
						|| m_PreloadTable[i].asset.pathID == oldAsset.pathID)
					|| m_PreloadTable[i].asset == oldAsset))
				{
					m_PreloadTable[i] = new PPtr<Object>(newAsset);
				}
			}

			for (int i = 0; i < m_Container.Count; i++)
			{
				if (m_Container[i].Value.asset.asset is NotLoaded &&
						(((NotLoaded)m_Container[i].Value.asset.asset).replacement == oldAsset
						|| m_Container[i].Value.asset.asset.pathID == oldAsset.pathID)
					|| m_Container[i].Value.asset.asset == oldAsset)
				{
					m_Container[i].Value.asset = new PPtr<Object>(newAsset);
				}
			}
		}

		public void RegisterForUpdate(Component asset)
		{
			NeedsUpdate.Add(asset);
		}

		public void UnregisterFromUpdate(Component asset)
		{
			NeedsUpdate.Remove(asset);
		}

		private void UpdateComponents()
		{
			List<Tuple<List<PPtr<Object>>, List<KeyValuePair<string, AssetInfo>>>> containerGroups = new List<Tuple<List<PPtr<Object>>, List<KeyValuePair<string, AssetInfo>>>>(m_Container.Count);
			for (int i = 0; i < m_Container.Count; i++)
			{
				bool found = false;
				if (containerGroups.Count > 0)
				{
					var group = containerGroups[containerGroups.Count - 1];
					if (group.Item2[0].Value.preloadIndex == m_Container[i].Value.preloadIndex)
					{
						group.Item2.Add(m_Container[i]);
						found = true;
					}
				}
				if (!found && m_Container[i].Value.asset.asset.pathID != 0)
				{
					List<PPtr<Object>> preloadPart = m_Container[i].Value.preloadIndex >= 0 ? m_PreloadTable.GetRange(m_Container[i].Value.preloadIndex, m_Container[i].Value.preloadSize) : new List<PPtr<Object>>();
					List<KeyValuePair<string, AssetInfo>> containerEntries = new List<KeyValuePair<string, AssetInfo>>();
					containerEntries.Add(m_Container[i]);
					containerGroups.Add(new Tuple<List<PPtr<Object>>, List<KeyValuePair<string, AssetInfo>>>(preloadPart, containerEntries));
				}
			}

			HashSet<Component> dependantAssets = new HashSet<Component>();
			Stream stream = null;
			try
			{
				file.loadingReferencials = true;
				foreach (var group in containerGroups)
				{
					foreach (PPtr<Object> assetPtr in group.Item1)
					{
						Component asset = assetPtr.asset is NotLoaded ? ((NotLoaded)assetPtr.asset).replacement : assetPtr.asset;
						if (NeedsUpdate.Contains(asset))
						{
							asset = file.FindComponent(group.Item2[0].Value.asset.asset.pathID);
							if (!dependantAssets.Contains(asset))
							{
								if (asset is NotLoaded)
								{
									if (stream == null)
									{
										stream = File.OpenRead(file.Parser.FilePath);
									}
									asset = file.LoadComponent(stream, (NotLoaded)asset);
								}
								dependantAssets.Add(asset);
							}
							break;
						}
					}
				}
			}
			finally
			{
				if (stream != null)
				{
					stream.Close();
					stream.Dispose();
				}
				file.loadingReferencials = false;
			}
			NeedsUpdate.UnionWith(dependantAssets);
			foreach (var group in containerGroups)
			{
				foreach (var containerEntries in group.Item2)
				{
					if (containerEntries.Value.asset.asset is NotLoaded)
					{
						Component asset = ((NotLoaded)containerEntries.Value.asset.asset).replacement;
						if (asset != null)
						{
							containerEntries.Value.asset = new PPtr<Object>(asset);
						}
					}
				}
			}
			foreach (Component asset in NeedsUpdate)
			{
				UpdateComponent(asset, containerGroups);
			}
			NeedsUpdate.Clear();

			m_PreloadTable.Clear();
			m_Container.Clear();
			for (int i = 0; i < containerGroups.Count; i++)
			{
				int preloadIdx = m_PreloadTable.Count;
				m_PreloadTable.AddRange(containerGroups[i].Item1);

				var containerEntries = containerGroups[i].Item2;
				string groupName = containerGroups[i].Item2[0].Key;
				foreach (var entry in containerEntries)
				{
					entry.Value.preloadIndex = preloadIdx;
				}
				if (i == 0 || m_Container[m_Container.Count - 1].Key.CompareTo(groupName) <= 0)
				{
					m_Container.AddRange(containerEntries);
				}
				else
				{
					for (int j = m_Container.Count - 1; j >= -1; j--)
					{
						if (j == -1 || m_Container[j].Key.CompareTo(groupName) <= 0)
						{
							m_Container.InsertRange(j + 1, containerEntries);
							break;
						}
					}
				}
			}

			m_MainAsset = new AssetInfo(file);
			m_MainAsset.asset = new PPtr<Object>((Component)null);
		}

		private void UpdateComponent(Component asset, List<Tuple<List<PPtr<Object>>, List<KeyValuePair<string, AssetInfo>>>> containerGroups)
		{
			List<Component> assets = new List<Component>();
			List<Component> transforms = new List<Component>();
			List<Component> containerRelated = new List<Component>();
			GetDependantAssets(asset, assets, transforms, containerRelated);
			if (asset.classID1 == UnityClassID.GameObject)
			{
				GameObject gameObj = (GameObject)asset;
				Animator animator = gameObj.FindLinkedComponent(UnityClassID.Animator);
				if (animator == null)
				{
					foreach (Component trans in transforms)
					{
						GetDependantAssets(trans, assets, null, null);
					}
					animator = new Animator(null, 0, UnityClassID.Animator, UnityClassID.Animator);
					animator.m_Avatar = new PPtr<Avatar>((Component)null);
					animator.m_Controller = new PPtr<RuntimeAnimatorController>((Component)null);
					GetDependantAssets(animator, assets, transforms, containerRelated);
					assets.Remove(animator);
				}
			}
			foreach (var group in containerGroups)
			{
				var preloadPart = group.Item1;
				var containerEntries = group.Item2;
				for (int i = 0; i < containerEntries.Count; i++)
				{
					var assetPair = containerEntries[i];
					if (assetPair.Value.asset.asset == asset)
					{
						preloadPart.Clear();
						for (int j = 0; j < assets.Count; j++)
						{
							if (assets[j] is ExternalAsset)
							{
								ExternalAsset extAsset = (ExternalAsset)assets[j];
								PPtr<Object> preloadEntry = new PPtr<Object>((Component)null);
								preloadEntry.m_FileID = extAsset.FileID;
								preloadEntry.m_PathID = extAsset.PathID;
								preloadPart.Add(preloadEntry);
							}
							else
							{
								preloadPart.Add(new PPtr<Object>(assets[j]));
							}
						}

						string groupName = containerEntries[0].Key;
						string assetName = AssetCabinet.ToString(asset);
						if (String.Compare(groupName, assetName, true) != 0)
						{
							groupName = assetName.ToLower();
						}
						if (containerEntries.Count > 1)
						{
							for (int j = 0; j < containerEntries.Count; j++)
							{
								switch (containerEntries[j].Value.asset.asset.classID1)
								{
								case UnityClassID.Mesh:
								case UnityClassID.AnimationClip:
									containerEntries.RemoveAt(j);
									j--;
									break;
								}
							}
							for (int j = containerRelated.Count - 1; j >= 0; j--)
							{
								AnimationClip clip = containerRelated[j] as AnimationClip;
								if (clip != null)
								{
									AssetInfo info = new AssetInfo(file);
									info.asset = new PPtr<Object>(clip);
									containerEntries.Insert(1, new KeyValuePair<string, AssetInfo>(groupName, info));
								}
							}
							for (int j = containerRelated.Count - 1; j >= 0; j--)
							{
								MeshRenderer meshR = containerRelated[j] as MeshRenderer;
								if (meshR != null)
								{
									Mesh mesh = Operations.GetMesh(meshR);
									if (mesh != null)
									{
										AssetInfo info = new AssetInfo(file);
										info.asset = new PPtr<Object>(mesh);
										containerEntries.Insert(1, new KeyValuePair<string, AssetInfo>(groupName, info));
									}
								}
							}
							for (int j = 0; j < containerEntries.Count; j++)
							{
								containerEntries[j].Value.preloadSize = assets.Count;
							}
						}
						else
						{
							containerEntries[0].Value.preloadSize = assets.Count;
						}
						for (int j = 0; j < containerEntries.Count; j++)
						{
							if (containerEntries[j].Key != groupName)
							{
								containerEntries[j] = new KeyValuePair<string, AssetInfo>(groupName, containerEntries[j].Value);
							}
						}
						return;
					}
				}
			}
		}

		private static void GetDependantAssets(Component asset, List<Component> assets, List<Component> transforms, List<Component> containerRelated)
		{
			if (asset != null && !assets.Contains(asset))
			{
				assets.Add(asset);
				switch (asset.classID1)
				{
				case UnityClassID.Animator:
					Animator animator = (Animator)asset;
					GetDependantAssets(animator.m_Avatar.asset, assets, transforms, containerRelated);
					GetDependantAssets(animator.m_Controller.asset, assets, transforms, containerRelated);
					foreach (Component ren in containerRelated)
					{
						GetDependantAssets(ren, assets, null, null);
						if (ren is MeshRenderer)
						{
							MeshRenderer meshR = (MeshRenderer)ren;
							PPtr<Mesh> meshPtr = Operations.GetMeshPtr(meshR);
							if (meshPtr != null)
							{
								if (meshPtr.m_FileID != 0)
								{
									Component found = assets.Find
									(
										delegate(Component c)
										{
											if (c is ExternalAsset)
											{
												ExternalAsset e = (ExternalAsset)c;
												return e.FileID == meshPtr.m_FileID && e.PathID == meshPtr.m_PathID;
											}
											return false;
										}
									);
									if (found == null)
									{
										ExternalAsset extMesh = new ExternalAsset();
										extMesh.FileID = meshPtr.m_FileID;
										extMesh.PathID = meshPtr.m_PathID;
										assets.Add(extMesh);
									}
								}
								else if (meshPtr.instance != null && !assets.Contains(meshPtr.instance))
								{
									assets.Add(meshPtr.instance);
								}
							}
							foreach (PPtr<Material> matPtr in meshR.m_Materials)
							{
								if (!assets.Contains(matPtr.asset))
								{
									GetDependantAssets(matPtr.asset, assets, null, null);
								}
							}
						}
					}
					assets.Sort
					(
						delegate(Component c1, Component c2)
						{
							if (c1 is ExternalAsset)
							{
								ExternalAsset e1 = (ExternalAsset)c1;
								if (c2 is ExternalAsset)
								{
									ExternalAsset e2 = (ExternalAsset)c2;
									return e1.FileID != e2.FileID ? e1.FileID.CompareTo(e2.FileID)
										: e1.PathID.CompareTo(e2.PathID);
								}
								return -1;
							}
							else if (c2 is ExternalAsset)
							{
								return 1;
							}
							return c1.pathID.CompareTo(c2.pathID);
						}
					);
					break;
				case UnityClassID.Avatar:
					break;
				case UnityClassID.AnimatorController:
					AnimatorController aCon = (AnimatorController)asset;
					for (int i = 0; i < aCon.m_AnimationClips.Count; i++)
					{
						assets.Add(aCon.m_AnimationClips[i].asset);
						containerRelated.Add(aCon.m_AnimationClips[i].asset);
					}
					break;
				case UnityClassID.AnimationClip:
					break;
				case UnityClassID.GameObject:
					GameObject gameObj = (GameObject)asset;
					animator = null;
					foreach (var compPair in gameObj.m_Component)
					{
						switch (compPair.Key)
						{
						case UnityClassID.Transform:
							Transform trans = (Transform)compPair.Value.instance;
							transforms.Add(trans);
							foreach (Transform child in trans)
							{
								GetDependantAssets(child.m_GameObject.asset, assets, transforms, containerRelated);
							}
							break;
						case UnityClassID.Animator:
							animator = (Animator)compPair.Value.asset;
							break;
						case UnityClassID.MeshRenderer:
						case UnityClassID.MeshFilter:
						case UnityClassID.SkinnedMeshRenderer:
							containerRelated.Add(compPair.Value.asset);
							break;
						case UnityClassID.MonoBehaviour:
							GetDependantAssets(compPair.Value.asset, assets, transforms, containerRelated);
							break;
						default:
							if (!assets.Contains(compPair.Value.asset))
							{
								assets.Add(compPair.Value.asset);
							}
							break;
						}
					}
					if (animator != null)
					{
						foreach (Component trans in transforms)
						{
							GetDependantAssets(trans, assets, null, null);
						}
						GetDependantAssets(animator, assets, transforms, containerRelated);
					}
					break;
				case UnityClassID.Light:
					break;
				default:
					if (asset.classID2 == UnityClassID.MonoBehaviour)
					{
						MonoBehaviour monoB = (MonoBehaviour)asset;
						if (!assets.Contains(monoB.m_MonoScript.asset))
						{
							assets.Add(monoB.m_MonoScript.asset);
						}
					}
					break;
				case UnityClassID.MonoScript:
					break;
				case UnityClassID.Transform:
					break;
				case UnityClassID.MeshRenderer:
				case UnityClassID.MeshFilter:
				case UnityClassID.SkinnedMeshRenderer:
					break;
				case UnityClassID.Material:
					Material mat = (Material)asset;
					foreach (var texVal in mat.m_SavedProperties.m_TexEnvs)
					{
						GetDependantAssets(texVal.Value.m_Texture.asset, assets, transforms, containerRelated);
					}
					if (mat.m_Shader.m_FileID != 0)
					{
						AddExternalAsset(assets, mat.m_Shader);
					}
					else if (mat.m_Shader.instance != null)
					{
						GetDependantAssets(mat.m_Shader.asset, assets, transforms, containerRelated);
					}
					break;
				case UnityClassID.Shader:
					Shader shader = (Shader)asset;
					foreach (PPtr<Shader> dep in shader.m_Dependencies)
					{
						if (dep.m_FileID != 0)
						{
							AddExternalAsset(assets, dep);
						}
						else if (dep.asset != null)
						{
							GetDependantAssets(dep.asset, assets, transforms, containerRelated);
						}
					}
					break;
				case UnityClassID.Sprite:
					assets.Remove(asset);
					Sprite sprite = (Sprite)asset;
					assets.Add(sprite.m_RD.texture.asset);
					assets.Add(sprite);
					break;
				case UnityClassID.Texture2D:
					break;
				}
			}
		}

		private static void AddExternalAsset(List<Component> assets, PPtr<Shader> dep)
		{
			Component found = assets.Find
			(
				delegate(Component c)
				{
					if (c is ExternalAsset)
					{
						ExternalAsset e = (ExternalAsset)c;
						return e.FileID == dep.m_FileID && e.PathID == dep.m_PathID;
					}
					return false;
				}
			);
			if (found == null)
			{
				ExternalAsset extShader = new ExternalAsset();
				extShader.FileID = dep.m_FileID;
				extShader.PathID = dep.m_PathID;
				assets.Add(extShader);
			}
		}

		public void Dump()
		{
			string msg = String.Empty;
			for (int i = 0; i < m_PreloadTable.Count; i++)
			{
				PPtr<Object> objPtr = m_PreloadTable[i];
				if (objPtr.m_FileID == 0)
				{
					Component comp = file.FindComponent(objPtr.asset.pathID);
					if (comp == null)
					{
						comp = new NotLoaded(file, objPtr.asset.pathID, 0, 0);
					}
					//if (comp.classID1 == UnityClassID.Material // comp.pathID != 0 && comp.classID1 != UnityClassID.GameObject && comp.classID1 != UnityClassID.Transform)
					{
						msg += i + " PathID=" + objPtr.asset.pathID + " " + comp.classID1 + " " + (!(comp is NotLoaded) ? AssetCabinet.ToString(comp) : ((NotLoaded)comp).Name) + "\r\n";
					}
				}
				else
				{
					msg += i + " external FileID=" + objPtr.m_FileID + " PathID=" + objPtr.m_PathID + "\r\n";
				}
			}
			Report.ReportLog(msg);

			msg = string.Empty;
			for (int i = 0; i < m_Container.Count; i++)
			{
				if (m_Container[i].Value.asset.asset.pathID != 0)
				{
					Component asset = file.FindComponent(m_Container[i].Value.asset.asset.pathID);
					msg += i + " " + m_Container[i].Key + " PathID=" + m_Container[i].Value.asset.asset.pathID + " i=" + m_Container[i].Value.preloadIndex + " s=" + m_Container[i].Value.preloadSize + " " + asset.classID1.ToString() + " " + (asset is NotLoaded ? ((NotLoaded)asset).Name : AssetCabinet.ToString(asset)) + "\r\n";
				}
				else
				{
					msg += "NULL! " + i + " " + m_Container[i].Key + " PathID=" + m_Container[i].Value.asset.asset.pathID + " i=" + m_Container[i].Value.preloadIndex + " s=" + m_Container[i].Value.preloadSize + " NULL!\r\n";
				}
			}
			Report.ReportLog(msg);

			if (m_MainAsset.asset.asset != null)
			{
				Component asset = file.FindComponent(m_MainAsset.asset.asset.pathID);
				msg = "Main Asset i=" + m_MainAsset.preloadIndex + " s=" + m_MainAsset.preloadSize + " " + asset.classID1.ToString() + " " + (asset is NotLoaded ? ((NotLoaded)asset).Name : AssetCabinet.ToString(asset));
				Report.ReportLog(msg);
			}
		}
	}
}
