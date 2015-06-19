using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public enum UnityClassID
	{
		GameObject = 1,
		Component = 2,
		LevelGameManager = 3,
		Transform = 4,
		TimeManager = 5,
		GlobalGameManager = 6,
		GameManager7 = 7,
		Behaviour = 8,
		GameManager9 = 9,
		AudioManager = 11,
		ParticleAnimator = 12,
		InputManager = 13,
		EllipsoidParticleEmitter = 15,
		Pipeline = 17,
		EditorExtension = 18,
		Physics2DSettings = 19,
		Camera = 20,
		Material = 21,
		MeshRenderer = 23,
		Renderer = 25,
		ParticleRenderer = 26,
		Texture = 27,
		Texture2D = 28,
		Scene = 29,
		RenderManager = 30,
		PipelineManager = 31,
		MeshFilter = 33,
		GameManager35 = 35,
		OcclusionPortal = 41,
		Mesh = 43,
		Skybox = 45,
		GameManager46 = 46,
		QualitySettings = 47,
		Shader = 48,
		TextAsset = 49,
		Rigidbody2D = 50,
		NotificationManager = 52,
		Rigidbody = 54,
		PhysicsManager = 55,
		Collider = 56,
		Joint = 57,
		CircleCollider2D = 58,
		HingeJoint = 59,
		PolygonCollider2D = 60,
		BoxCollider2D = 61,
		PhysicsMaterial2D = 62,
		GameManager63 = 63,
		MeshCollider = 64,
		BoxCollider = 65,
		AnimationManager = 71,
		AnimationClip = 74,
		ConstantForce = 75,
		WorldParticleCollider = 76,
		TagManager = 78,
		AudioListener = 81,
		AudioSource = 82,
		AudioClip = 83,
		RenderTexture = 84,
		MeshParticleEmitter = 87,
		ParticleEmitter = 88,
		Cubemap = 89,
		Avatar = 90,
		AnimatorController = 91,
		GUILayer = 92,
		ScriptMapper = 94,
		Animator = 95,
		TrailRenderer = 96,
		DelayedCallManager = 98,
		TextMesh = 102,
		RenderSettings = 104,
		Light = 108,
		CGProgram = 109,
		Animation = 111,
		MonoBehaviour = 114,
		MonoScript = 115,
		MonoManager = 116,
		Texture3D = 117,
		Projector = 119,
		LineRenderer = 120,
		Flare = 121,
		Halo = 122,
		LensFlare = 123,
		FlareLayer = 124,
		HaloLayer = 125,
		NavMeshLayers = 126,
		HaloManager = 127,
		Font = 128,
		PlayerSettings = 129,
		NamedObject = 130,
		GUITexture = 131,
		GUIText = 132,
		GUIElement = 133,
		PhysicMaterial = 134,
		SphereCollider = 135,
		CapsuleCollider = 136,
		SkinnedMeshRenderer = 137,
		FixedJoint = 138,
		RaycastCollider = 140,
		BuildSettings = 141,
		AssetBundle = 142,
		CharacterController = 143,
		CharacterJoint = 144,
		SpringJoint = 145,
		WheelCollider = 146,
		ResourceManager = 147,
		NetworkView = 148,
		NetworkManager = 149,
		PreloadData = 150,
		MovieTexture = 152,
		ConfigurableJoint = 153,
		TerrainCollider = 154,
		MasterServerInterface = 155,
		TerrainData = 156,
		LightmapSettings = 157,
		WebCamTexture = 158,
		EditorSettings = 159,
		InteractiveCloth = 160,
		ClothRenderer = 161,
		SkinnedCloth = 163,
		AudioReverbFilter = 164,
		AudioHighPassFilter = 165,
		AudioChorusFilter = 166,
		AudioReverbZone = 167,
		AudioEchoFilter = 168,
		AudioLowPassFilter = 169,
		AudioDistortionFilter = 170,
		AudioBehaviour = 180,
		AudioFilter = 181,
		WindZone = 182,
		Cloth = 183,
		SubstanceArchive = 184,
		ProceduralMaterial = 185,
		ProceduralTexture = 186,
		OffMeshLink = 191,
		OcclusionArea = 192,
		Tree = 193,
		NavMesh = 194,
		NavMeshAgent = 195,
		NavMeshSettings = 196,
		LightProbeCloud = 197,
		ParticleSystem = 198,
		ParticleSystemRenderer = 199,
		LODGroup = 205,
		NavMeshObstacle = 208,
		SpriteRenderer = 212,
		Sprite = 213,
		LightProbeGroup = 220,
		AnimatorOverrideController = 221,
		LinkToGameObject = 222,
		LinkToGameObject223 = 223,
		MultiLink = 224,
		LinkToGameObject225 = 225,
		SpringJoint2D = 231,
		HingeJoint2D = 233,
		Prefab = 1001,
		EditorExtensionImpl = 1002,
		AssetImporter = 1003,
		AssetDatabase = 1004,
		Mesh3DSImporter = 1005,
		TextureImporter = 1006,
		ShaderImporter = 1007,
		AudioImporter = 1020,
		HierarchyState = 1026,
		GUIDSerializer = 1027,
		AssetMetaData = 1028,
		DefaultAsset = 1029,
		DefaultImporter = 1030,
		TextScriptImporter = 1031,
		NativeFormatImporter = 1034,
		MonoImporter = 1035,
		AssetServerCache = 1037,
		LibraryAssetImporter = 1038,
		ModelImporter = 1040,
		FBXImporter = 1041,
		TrueTypeFontImporter = 1042,
		MovieImporter = 1044,
		EditorBuildSettings = 1045,
		DDSImporter = 1046,
		InspectorExpandedState = 1048,
		AnnotationManager = 1049,
		MonoAssemblyImporter = 1050,
		EditorUserBuildSettings = 1051,
		PVRImporter = 1052,
		SubstanceImporter = 1112,
	}

	public class PPtr<T> where T : Component
	{
		public int m_FileID { get; set; }
		public int m_PathID { get; set; }

		public Component asset { get; protected set; }
		public T instance { get; protected set; }

		public PPtr(Stream stream)
		{
			LoadFrom(stream);
		}

		public PPtr(Stream stream, AssetCabinet file)
		{
			LoadFrom(stream);
			if (m_FileID == 0)
			{
				int index;
				Component comp = file.FindComponent(m_PathID, out index);
				if (comp is NotLoaded)
				{
					long pos = stream.Position;
					asset = file.LoadComponent(stream, index, (NotLoaded)comp);
					if (asset == null)
					{
						asset = comp;
					}
					stream.Position = pos;
				}
				else
				{
					asset = comp;
				}
				if (asset is T)
				{
					instance = (T)asset;
				}
			}
		}

		public PPtr(Component asset)
		{
			m_FileID = 0;
			if (asset != null)
			{
				m_PathID = asset.pathID;
				this.asset = asset;
				if (asset is T)
				{
					instance = (T)asset;
				}
			}
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_FileID = reader.ReadInt32();
			m_PathID = reader.ReadInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_FileID);
			writer.Write(asset != null ? asset.pathID : m_PathID);
		}

		public void UpdateOrLoad()
		{
			if (asset is NotLoaded)
			{
				NotLoaded notLoaded = (NotLoaded)asset;
				if (notLoaded.replacement != null)
				{
					asset = instance = (T)notLoaded.replacement;
				}
				else
				{
					asset = instance = asset.file.LoadComponent(asset.file.SourceStream, notLoaded);
				}
			}
		}
	}

	public abstract class Object : Component
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public abstract void LoadFrom(Stream stream);
		public abstract void WriteTo(Stream stream);
	}

	public class xform : IObjInfo
	{
		public Vector4 t { get; set; }
		public Quaternion q { get; set; }
		public Vector4 s { get; set; }

		public xform(Vector4 t, Quaternion q, Vector4 s)
		{
			this.t = t;
			this.q = q;
			this.s = s;
		}

		public xform(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			t = reader.ReadVector4();
			q = reader.ReadQuaternion();
			s = reader.ReadVector4();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(t);
			writer.Write(q);
			writer.Write(s);
		}
	}

	public class AABB : IObjInfo
	{
		public Vector3 m_Center { get; set; }
		public Vector3 m_Extend { get; set; }

		public AABB(Stream stream)
		{
			LoadFrom(stream);
		}

		public AABB() { }

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Center = reader.ReadVector3();
			m_Extend = reader.ReadVector3();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_Center);
			writer.Write(m_Extend);
		}

		public AABB Clone()
		{
			AABB aabb = new AABB();
			aabb.m_Center = m_Center;
			aabb.m_Extend = m_Extend;
			return aabb;
		}
	}

	public class BitField : IObjInfo
	{
		uint m_Bits { get; set; }

		public BitField(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Bits = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_Bits);
		}
	}

	public class UnityParser
	{
		public string FilePath { get; protected set; }

		public byte[] ExtendedSignature { get; protected set; }
		public int FileLength { get; protected set; }
		public int HeaderLength { get; protected set; }
		public int Unknown1 { get; protected set; }
		public int Unknown2 { get; protected set; }
		public int Entry1Length { get; protected set; }
		public int Entry1LengthCopy { get; protected set; }
		public int FileLengthCopy { get; protected set; }
		public int CabinetOffset { get; protected set; }
		public byte LastByte { get; protected set; }
		public string Name { get; protected set; }
		public int Unknown3 { get; protected set; }
		public int Offset { get; protected set; }
		public int ContentLength { get; set; }
		public AssetCabinet Cabinet { get; protected set; }

		public List<Component> Textures { get; set; }

		private string destPath;
		private bool keepBackup;
		private string backupExt;
		private bool keepPathIDs;
		public BackgroundWorker worker;
		public HashSet<AssetCabinet> DeleteModFiles { get; protected set; }

		public UnityParser(string path)
		{
			FilePath = path;
			using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
			{
				byte[] extendedSignature = reader.ReadBytes(27);
				if (System.Text.UTF8Encoding.Default.GetString(extendedSignature, 0, 8) == "UnityRaw")
				{
					ExtendedSignature = extendedSignature;
					FileLength = reader.ReadInt32BE();
					HeaderLength = reader.ReadInt32BE();
					Unknown1 = reader.ReadInt32BE();
					Unknown2 = reader.ReadInt32BE(); // number of entries
					Entry1Length = reader.ReadInt32BE();
					Entry1LengthCopy = reader.ReadInt32BE();
					FileLengthCopy = reader.ReadInt32BE();
					CabinetOffset = reader.ReadInt32BE();
					LastByte = reader.ReadByte();

					if (reader.BaseStream.Length != FileLength || reader.BaseStream.Length != FileLengthCopy)
					{
						throw new Exception("Unsupported Unity3d file");
					}

					Unknown3 = reader.ReadInt32BE();
					Name = reader.ReadName0();
					Offset = reader.ReadInt32BE();
					ContentLength = reader.ReadInt32BE();
					reader.BaseStream.Position = HeaderLength + Offset;
				}
				else
				{
					reader.BaseStream.Position -= extendedSignature.Length;
				}
				Cabinet = new AssetCabinet(reader.BaseStream, this);
			}

			InitTextures();
			DeleteModFiles = new HashSet<AssetCabinet>();
		}

		private void InitTextures()
		{
			Textures = new List<Component>();
			foreach (Component asset in Cabinet.Components)
			{
				if (asset.classID1 == UnityClassID.Texture2D || asset.classID1 == UnityClassID.Cubemap)
				{
					Textures.Add(asset);
				}
			}
		}

		public UnityParser(UnityParser source)
		{
			FilePath = source.FilePath;

			if (source.ExtendedSignature != null)
			{
				ExtendedSignature = (byte[])source.ExtendedSignature.Clone();
				HeaderLength = source.HeaderLength;
				Unknown1 = source.Unknown1;
				Unknown2 = source.Unknown2;
				CabinetOffset = source.CabinetOffset;
				LastByte = source.LastByte;

				Unknown3 = source.Unknown3;
				Name = (string)source.Name.Clone();
				Offset = source.Offset;
			}
			Cabinet = new AssetCabinet(source.Cabinet, this);

			InitTextures();
			DeleteModFiles = new HashSet<AssetCabinet>();
		}

		public BackgroundWorker WriteArchive(string destPath, bool keepBackup, string backupExtension, bool background, bool keepPathIDs = false)
		{
			this.destPath = destPath;
			this.keepBackup = keepBackup;
			this.backupExt = backupExtension;
			this.keepPathIDs = keepPathIDs;

			worker = new BackgroundWorker();
			worker.WorkerSupportsCancellation = true;
			worker.WorkerReportsProgress = true;

			worker.DoWork += new DoWorkEventHandler(writeArchiveWorker_DoWork);

			if (!background)
			{
				writeArchiveWorker_DoWork(worker, new DoWorkEventArgs(null));
			}

			return worker;
		}

		void writeArchiveWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			string dirName = Path.GetDirectoryName(destPath);
			if (dirName == String.Empty)
			{
				dirName = @".\";
			}
			DirectoryInfo dir = new DirectoryInfo(dirName);
			if (!dir.Exists)
			{
				dir.Create();
			}

			string newName = destPath + ".$$$";
			try
			{
				using (BinaryWriter writer = new BinaryWriter(File.Create(newName)))
				{
					if (ExtendedSignature != null)
					{
						writer.BaseStream.Position = 27 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 1 + 4 + (Name.Length + 1) + 4 + 4;
						if ((writer.BaseStream.Position & 3) > 0)
						{
							writer.BaseStream.Position += 4 - (writer.BaseStream.Position & 3);
						}
					}

					bool needsLoadingRefs = false;
					if (!keepPathIDs)
					{
						for (int i = 0; i < Cabinet.Components.Count; i++)
						{
							if (Cabinet.Components[i].pathID - 1 == i)
							{
								continue;
							}
							needsLoadingRefs = true;
							while (i < Cabinet.Components.Count && Cabinet.Components[i].pathID == 0)
							{
								Cabinet.Components[i].pathID = i + 1;
								i++;
							}
							if (i == Cabinet.Components.Count)
							{
								needsLoadingRefs = false;
							}
							break;
						}
					}
					worker.ReportProgress(1);
					using (Stream stream = File.OpenRead(FilePath))
					{
						if (needsLoadingRefs)
						{
							List<UnityClassID> storeRefClasses = new List<UnityClassID>
							(
								new UnityClassID[]
								{
									UnityClassID.AnimationClip,
									UnityClassID.Animator,
									UnityClassID.AnimatorController,
									UnityClassID.AssetBundle,
									UnityClassID.AudioListener,
									UnityClassID.AudioSource,
									UnityClassID.BoxCollider,
									UnityClassID.Camera,
									UnityClassID.CapsuleCollider,
									UnityClassID.Cubemap,
									UnityClassID.EllipsoidParticleEmitter,
									UnityClassID.FlareLayer,
									UnityClassID.GameObject,
									UnityClassID.Light,
									UnityClassID.LinkToGameObject,
									UnityClassID.LinkToGameObject223,
									UnityClassID.LinkToGameObject225,
									UnityClassID.Material,
									UnityClassID.MeshCollider,
									UnityClassID.MeshFilter,
									UnityClassID.MeshRenderer,
									UnityClassID.MonoBehaviour,
									UnityClassID.MultiLink,
									UnityClassID.ParticleAnimator,
									UnityClassID.ParticleRenderer,
									UnityClassID.ParticleSystem,
									UnityClassID.ParticleSystemRenderer,
									UnityClassID.Projector,
									UnityClassID.Rigidbody,
									UnityClassID.Shader,
									UnityClassID.SkinnedMeshRenderer,
									UnityClassID.SphereCollider,
									UnityClassID.Sprite,
									UnityClassID.SpriteRenderer,
									UnityClassID.Transform
								}
							);
							Cabinet.loadingReferencials = true;
							for (int i = 0; i < Cabinet.Components.Count; i++)
							{
								NotLoaded asset = Cabinet.Components[i] as NotLoaded;
								if (asset != null && storeRefClasses.Contains(asset.classID2))
								{
									Cabinet.LoadComponent(stream, i, asset);
									worker.ReportProgress(1 + i * 49 / Cabinet.Components.Count);
								}
							}
							for (int i = 0; i < Cabinet.Components.Count; i++)
							{
								Cabinet.Components[i].pathID = i + 1;
							}
							Cabinet.loadingReferencials = false;
						}
						Cabinet.SourceStream = stream;
						Cabinet.WriteTo(writer.BaseStream);
					}

					FileLength = FileLengthCopy = (int)writer.BaseStream.Length;
					Entry1Length = Entry1LengthCopy = FileLength - HeaderLength;

					if (ExtendedSignature != null)
					{
						writer.BaseStream.Position = 0;
						writer.Write(ExtendedSignature);
						writer.WriteInt32BE(FileLength);
						writer.WriteInt32BE(HeaderLength);
						writer.WriteInt32BE(Unknown1);
						writer.WriteInt32BE(Unknown2);
						writer.WriteInt32BE(Entry1Length);
						writer.WriteInt32BE(Entry1LengthCopy);
						writer.WriteInt32BE(FileLengthCopy);
						writer.WriteInt32BE(CabinetOffset);
						writer.Write(LastByte);
						writer.WriteInt32BE(Unknown1);
						writer.WriteName0(Name);
						writer.WriteInt32BE(Offset);
						writer.WriteInt32BE(ContentLength);
					}
				}

				if (FilePath == destPath)
				{
					if (keepBackup)
					{
						string backup = Utility.GetDestFile(dir, Path.GetFileNameWithoutExtension(destPath) + ".bak", backupExt);
						File.Move(FilePath, backup);
					}
					else
					{
						File.Delete(FilePath);
					}
				}
				File.Move(newName, destPath);
				FilePath = destPath;
			}
			catch (Exception ex)
			{
				File.Delete(newName);
				Utility.ReportException(ex);
			}
		}

		public dynamic LoadAsset(int pathID)
		{
			return Cabinet.LoadComponent(pathID);
		}

		public Texture2D GetTexture(string name)
		{
			if (name == null)
			{
				return null;
			}
			Stream stream = null;
			try
			{
				foreach (Component asset in Textures)
				{
					if (asset is Texture2D)
					{
						Texture2D tex = (Texture2D)asset;
						if (name == tex.m_Name || Regex.IsMatch(name, tex.m_Name + "-[^-]+-[^-]+-[^-]+"))
						{
							return tex;
						}
					}
					else
					{
						NotLoaded comp = (NotLoaded)asset;
						if (comp.Name == null)
						{
							if (stream == null)
							{
								stream = File.OpenRead(FilePath);
							}
							stream.Position = comp.offset;
							comp.Name = Texture2D.LoadName(stream);
						}
						if (name == comp.Name || Regex.IsMatch(name, comp.Name + "-[^-]+-[^-]+-[^-]+"))
						{
							if (stream == null)
							{
								stream = File.OpenRead(FilePath);
							}
							return LoadTexture(stream, comp);
						}
					}
				}
				return null;
			}
			finally
			{
				if (stream != null)
				{
					stream.Close();
					stream.Dispose();
					stream = null;
				}
			}
		}

		public Component FindTexture(string name)
		{
			Component texFound = Textures.Find
			(
				delegate(Component tex)
				{
					return (tex is NotLoaded ? ((NotLoaded)tex).Name : AssetCabinet.ToString(tex)) == name;
				}
			);
			return texFound;
		}

		public Texture2D GetTexture(int index)
		{
			Texture2D tex = Textures[index] as Texture2D;
			if (tex != null)
			{
				return tex;
			}

			NotLoaded comp = (NotLoaded)Textures[index];
			using (Stream stream = File.OpenRead(FilePath))
			{
				tex = LoadTexture(stream, comp);
			}
			return tex;
		}

		public Texture2D LoadTexture(Stream stream, NotLoaded comp)
		{
			Texture2D tex = comp.replacement != null ? (Texture2D)comp.replacement : Cabinet.LoadComponent(stream, comp);
			if (tex != null)
			{
				int index = Textures.IndexOf(comp);
				Textures.RemoveAt(index);
				Textures.Insert(index, tex);
			}
			return tex;
		}

		public Texture2D AddTexture(ImportedTexture texture)
		{
			Texture2D tex = new Texture2D(Cabinet, 0, UnityClassID.Texture2D, UnityClassID.Texture2D);
			tex.LoadFrom(texture);
			Cabinet.ReplaceSubfile(-1, tex, null);
			Cabinet.Bundle.AddComponent(tex);
			Textures.Add(tex);
			return tex;
		}
	}
}
