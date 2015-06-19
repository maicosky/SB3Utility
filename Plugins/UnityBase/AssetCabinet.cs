using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using SB3Utility;

namespace UnityPlugin
{
	public class AssetCabinet : NeedsSourceStreamForWriting
	{
		public int UsedLength { get; protected set; }
		public int ContentLengthCopy { get; protected set; }
		public int Format { get; protected set; }
		public int DataPosition { get; protected set; }
		public int Unknown6 { get; protected set; }
		public string Version { get; protected set; }
		public int Unknown7 { get; protected set; }

		public class TypeDefinitionString
		{
			public string type;
			public string identifier;
			public int[] flags;
			public TypeDefinitionString[] children;

			public TypeDefinitionString Clone()
			{
				TypeDefinitionString clone = new TypeDefinitionString();
				clone.type = type;
				clone.identifier = identifier;
				clone.flags = (int[])flags.Clone();

				clone.children = new TypeDefinitionString[children.Length];
				for (int i = 0; i < children.Length; i++)
				{
					clone.children[i] = children[i].Clone();
				}
				return clone;
			}
		}
		public class TypeDefinition
		{
			public int typeId;
			public TypeDefinitionString definitions;

			public TypeDefinition Clone()
			{
				TypeDefinition clone = new TypeDefinition();
				clone.typeId = typeId;
				clone.definitions = definitions.Clone();
				return clone;
			}
		}
		public List<TypeDefinition> Types { get; protected set; }
		public int Unknown8 { get; protected set; }

		public List<Component> Components { get; protected set; }

		public class Reference
		{
			public Guid guid;
			public int type;
			public String filePath;
			public String assetPath;
		}
		public Reference[] References { get; protected set; }

		public Stream SourceStream { get; set; }
		public UnityParser Parser { get; set; }
		public bool loadingReferencials { get; set; }
		public List<NotLoaded> RemovedList { get; set; }
		HashSet<string> reported;
		public AssetBundle Bundle { get; set; }
		public static HashSet<Tuple<Component, Component>> IncompleteClones = new HashSet<Tuple<Component, Component>>();

		public AssetCabinet(Stream stream, UnityParser parser)
		{
			Parser = parser;
			BinaryReader reader = new BinaryReader(stream);

			UsedLength = reader.ReadInt32BE();
			ContentLengthCopy = reader.ReadInt32BE();
			Format = reader.ReadInt32BE();
			DataPosition = reader.ReadInt32BE();
			Unknown6 = reader.ReadInt32BE();
			Version = reader.ReadName0();

			Unknown7 = reader.ReadInt32();

			int numTypes = reader.ReadInt32();
			Types = new List<TypeDefinition>(numTypes);
			for (int i = 0; i < numTypes; i++)
			{
				TypeDefinition t = new TypeDefinition();
				t.typeId = reader.ReadInt32();
				t.definitions = new TypeDefinitionString();
				ReadType(reader, t.definitions);
				Types.Add(t);
			}

			Unknown8 = reader.ReadInt32();

			int numComponents = reader.ReadInt32();
			Components = new List<Component>(numComponents);
			for (int i = 0; i < numComponents; i++)
			{
				int pathID = reader.ReadInt32();
				uint offset = (uint)(parser.HeaderLength + parser.Offset) + (uint)DataPosition + reader.ReadUInt32();
				uint size = reader.ReadUInt32();
				NotLoaded comp = new NotLoaded(this, pathID, (UnityClassID)reader.ReadInt32(), (UnityClassID)reader.ReadInt32());
				comp.offset = offset;
				comp.size = size;
				Components.Add(comp);
			}

			int numRefs = reader.ReadInt32();
			References = new Reference[numRefs];
			for (int i = 0; i < numRefs; i++)
			{
				References[i] = new Reference();
				References[i].guid = new Guid(reader.ReadBytes(16));
				References[i].type = reader.ReadInt32();
				References[i].filePath = reader.ReadName0();
				References[i].assetPath = reader.ReadName0();
			}
			if (stream.Position != UsedLength + (parser.HeaderLength + parser.Offset) + 0x13)
			{
				Report.ReportLog("Unexpected Length Pos=" + stream.Position.ToString("X") + " UsedLength=" + UsedLength.ToString("X"));
			}
			long padding = (stream.Position + 16) & ~(long)15;
			if (padding != parser.HeaderLength + parser.Offset + DataPosition)
			{
				Report.ReportLog("Unexpected DataPosition");
			}

			RemovedList = new List<NotLoaded>();
			loadingReferencials = false;
			reported = new HashSet<string>();

			for (int i = 0; i < Components.Count; i++)
			{
				Component asset = Components[i];
				if (asset.classID1 == UnityClassID.AssetBundle)
				{
					Bundle = LoadComponent(stream, i, (NotLoaded)asset);
					break;
				}
			}
		}

		public AssetCabinet(AssetCabinet source, UnityParser parser)
		{
			Parser = parser;

			Format = source.Format;
			Unknown6 = source.Unknown6;
			Version = (string)source.Version.Clone();

			Unknown7 = source.Unknown7;

			int numTypes = source.Types.Count;
			Types = new List<TypeDefinition>(numTypes);
			for (int i = 0; i < numTypes; i++)
			{
				TypeDefinition t = source.Types[i];
				Types.Add(t);
			}

			Unknown8 = source.Unknown8;

			int numComponents = source.Components.Count;
			Components = new List<Component>(numComponents);

			int numRefs = source.References.Length;
			References = new Reference[numRefs];
			for (int i = 0; i < numRefs; i++)
			{
				References[i] = source.References[i];
			}

			RemovedList = new List<NotLoaded>();
			loadingReferencials = false;
			reported = new HashSet<string>();
		}

		private void ReadType(BinaryReader reader, TypeDefinitionString tds)
		{
			tds.type = reader.ReadName0();
			tds.identifier = reader.ReadName0();
			tds.flags = new int[5];
			for (int i = 0; i < 5; i++)
			{
				tds.flags[i] = reader.ReadInt32();
			}
			int numChildren = reader.ReadInt32();
			tds.children = new TypeDefinitionString[numChildren];
			for (int i = 0; i < numChildren; i++)
			{
				tds.children[i] = new TypeDefinitionString();
				ReadType(reader, tds.children[i]);
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			long beginPos = stream.Position;

			stream.Position += 4 + 4 + 4 + 4;
			writer.WriteInt32BE(Unknown6);
			writer.WriteName0(Version);

			writer.Write(Unknown7);

			writer.Write(Types.Count);
			for (int i = 0; i < Types.Count; i++)
			{
				writer.Write(Types[i].typeId);
				WriteType(writer, Types[i].definitions);
			}

			writer.Write(Unknown8);

			writer.Write(Components.Count);
			long assetMetaPosition = stream.Position;
			stream.Position += Components.Count * 5 * sizeof(int);

			writer.Write(References.Length);
			for (int i = 0; i < References.Length; i++)
			{
				writer.Write(References[i].guid.ToByteArray());
				writer.Write(References[i].type);
				writer.WriteName0(References[i].filePath);
				writer.WriteName0(References[i].assetPath);
			}
			UsedLength = (int)stream.Position - (Parser.HeaderLength + Parser.Offset + 0x13);
			stream.Position = (stream.Position + 16) & ~(long)15;
			DataPosition = (int)stream.Position - (Parser.HeaderLength + Parser.Offset);

			uint[] offsets = new uint[Components.Count];
			uint[] sizes = new uint[Components.Count];
			byte[] align = new byte[3];
			Dictionary<AssetCabinet, Stream> foreignNotLoaded = new Dictionary<AssetCabinet, Stream>();
			try
			{
				for (int i = 0; i < Components.Count; i++)
				{
					offsets[i] = (uint)stream.Position;
					Component comp = Components[i];
					if (comp is NeedsSourceStreamForWriting)
					{
						if (comp.file == this)
						{
							((NeedsSourceStreamForWriting)comp).SourceStream = SourceStream;
						}
						else
						{
							Stream str;
							if (!foreignNotLoaded.TryGetValue(comp.file, out str))
							{
								str = File.OpenRead(comp.file.Parser.FilePath);
								foreignNotLoaded.Add(comp.file, str);
							}
							((NotLoaded)comp).SourceStream = str;
						}
					}
					comp.WriteTo(stream);
					sizes[i] = (uint)stream.Position - offsets[i];
					int rest = 4 - (int)(stream.Position & 3);
					if (rest < 4 && i < Components.Count - 1)
					{
						writer.Write(align, 0, rest);
					}
					Parser.worker.ReportProgress(50 + i * 49 / Components.Count);
				}
			}
			finally
			{
				foreach (var foreign in foreignNotLoaded)
				{
					foreign.Value.Close();
					if (Parser.DeleteModFiles.Contains(foreign.Key))
					{
						File.Delete(foreign.Key.Parser.FilePath);
					}
				}
			}
			Parser.ContentLength = ContentLengthCopy = (int)stream.Position - (Parser.HeaderLength + Parser.Offset);

			stream.Position = beginPos;
			writer.WriteInt32BE(UsedLength);
			writer.WriteInt32BE(ContentLengthCopy);
			writer.WriteInt32BE(Format);
			writer.WriteInt32BE(DataPosition);

			stream.Position = assetMetaPosition;
			NotLoaded newAssetBundle = null;
			for (int i = 0; i < Components.Count; i++)
			{
				Component comp = Components[i];
				writer.Write(comp.pathID);
				writer.Write(offsets[i] - (uint)DataPosition - (uint)(Parser.HeaderLength + Parser.Offset));
				writer.Write(sizes[i]);
				writer.Write((int)comp.classID1);
				writer.Write((int)comp.classID2);
				if (comp.file != this)
				{
					NotLoaded notLoaded = new NotLoaded(this, comp.pathID, comp.classID1, comp.classID2);
					notLoaded.size = sizes[i];
					ReplaceSubfile(comp, notLoaded);
					if (comp.classID1 == UnityClassID.AssetBundle)
					{
						newAssetBundle = notLoaded;
					}
					comp = notLoaded;
				}
				if (comp is NotLoaded)
				{
					((NotLoaded)comp).offset = offsets[i];
				}
			}
			if (newAssetBundle != null)
			{
				Bundle = LoadComponent(stream, newAssetBundle);
			}
		}

		private void WriteType(BinaryWriter writer, TypeDefinitionString tds)
		{
			writer.WriteName0(tds.type);
			writer.WriteName0(tds.identifier);
			for (int i = 0; i < tds.flags.Length; i++)
			{
				writer.Write(tds.flags[i]);
			}
			writer.Write(tds.children.Length);
			for (int i = 0; i < tds.children.Length; i++)
			{
				WriteType(writer, tds.children[i]);
			}
		}

		public void MergeTypeDefinition(AssetCabinet file, UnityClassID cls)
		{
			AssetCabinet.TypeDefinition clsDef = Types.Find
			(
				delegate(AssetCabinet.TypeDefinition def)
				{
					return def.typeId == (int)cls;
				}
			);
			if (clsDef == null)
			{
				clsDef = file.Types.Find
				(
					delegate(AssetCabinet.TypeDefinition def)
					{
						return def.typeId == (int)cls;
					}
				);
				if (clsDef == null)
				{
					Report.ReportLog("Warning! Class Definition for " + cls + " not found!");
					return;
				}
				Types.Add(clsDef);
			}
		}

		public static bool CompareTypes(AssetCabinet.TypeDefinition td1, AssetCabinet.TypeDefinition td2)
		{
			return CompareTypes(td1.definitions, td2.definitions);
		}

		public static bool CompareTypes(AssetCabinet.TypeDefinitionString tds1, AssetCabinet.TypeDefinitionString tds2)
		{
			if (tds1.type != tds2.type || tds1.identifier != tds2.identifier || tds1.children.Length != tds2.children.Length)
			{
				return false;
			}

			for (int i = 0; i < tds1.children.Length; i++)
			{
				if (!CompareTypes(tds1.children[i], tds2.children[i]))
				{
					return false;
				}
			}
			return true;
		}

		public void DumpType(UnityClassID cls)
		{
			for (int i = 0; i < Types.Count; i++)
			{
				if (Types[i].typeId == (int)cls)
				{
					DumpType(Types[i]);
					return;
				}
			}
		}

		public void DumpType(TypeDefinition td)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder(10000);
			sb.AppendFormat("typeId={0}\r\n", td.typeId);
			DumpTypeString(td.definitions, 0, sb);
			Report.ReportLog(sb.ToString());
		}

		void DumpTypeString(TypeDefinitionString tds, int level, System.Text.StringBuilder sb)
		{
			sb.AppendFormat("{0," + (level * 3 + tds.type.Length) + "} {1} flags=({2}, {3}, {4}, {5}, x{6:X})\r\n", tds.type, tds.identifier, tds.flags[0], tds.flags[1], tds.flags[2], tds.flags[3], tds.flags[4]);
			for (int i = 0; i < tds.children.Length; i++)
			{
				DumpTypeString(tds.children[i], level + 1, sb);
			}
		}

		public dynamic FindComponentIndex(int pathID)
		{
			for (int i = 0; i < Components.Count; i++)
			{
				Component comp = Components[i];
				if (comp.pathID == pathID)
				{
					return i;
				}
			}
			return -1;
		}

		public dynamic FindComponent(int pathID, out int index, bool show_error = true)
		{
			if (pathID == 0)
			{
				index = -1;
				return null;
			}
			index = pathID >= 0 && pathID - 1 < Components.Count ? pathID - 1 : Components.Count - 1;
			Component asset = Components[index];
			if (asset.pathID == pathID)
			{
				return asset;
			}
			try
			{
				int i = index;
				while (asset.pathID < pathID && index < Components.Count - 1)
				{
					asset = Components[++index];
				}
				if (asset.pathID == pathID)
				{
					return asset;
				}
				asset = Components[index = i - 1];
				while (asset.pathID == 0 || asset.pathID > pathID)
				{
					asset = Components[--index];
				}
				if (asset.pathID == pathID)
				{
					return asset;
				}
			}
			catch { }

			if (show_error)
			{
				Report.ReportLog("FindComponent : pathID=" + pathID + " not found");
			}
			index = -1;
			return null;
		}

		public dynamic FindComponent(int pathID, bool show_error = true)
		{
			int index_not_required;
			return FindComponent(pathID, out index_not_required, show_error);
		}

		public void BeginLoadingSkippedComponents()
		{
			SourceStream = File.OpenRead(Parser.FilePath);
		}

		public void EndLoadingSkippedComponents()
		{
			SourceStream.Close();
			SourceStream.Dispose();
			SourceStream = null;
		}

		public dynamic LoadComponent(int pathID)
		{
			if (pathID == 0)
			{
				return null;
			}

			int index;
			Component subfile = FindComponent(pathID, out index);
			NotLoaded comp = subfile as NotLoaded;
			if (comp == null)
			{
				return subfile;
			}

			using (Stream stream = File.OpenRead(Parser.FilePath))
			{
				//stream.Position = comp.offset;
				//using (PartialStream ps = new PartialStream(stream, comp.size))
				{
					Component asset = LoadComponent(/*ps*/stream, index, comp);
					/*if (!loadingReferencials &&
						comp.offset + comp.size != stream.Position &&
						(comp.classID1 != UnityClassID.SkinnedMeshRenderer ||
							comp.offset + comp.size - 3 != stream.Position))
					{
						Console.WriteLine(comp.classID1 + " ctr read bad length" + (stream.Position - comp.offset - comp.size));
					}*/
					return asset != null ? asset : subfile;
				}
			}
		}

		public dynamic LoadComponent(Stream stream, NotLoaded comp)
		{
			return LoadComponent(stream, Components.IndexOf(comp), comp);
		}

		public dynamic LoadComponent(Stream stream, int index, NotLoaded comp)
		{
			stream.Position = comp.offset;
			try
			{
				switch (comp.classID1)
				{
				case UnityClassID.AnimationClip:
					{
						AnimationClip animationClip = new AnimationClip(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, animationClip, comp);
						animationClip.LoadFrom(stream);
						return animationClip;
					}
				case UnityClassID.Animator:
					{
						Animator animator = new Animator(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, animator, comp);
						animator.LoadFrom(stream);
						return animator;
					}
				case UnityClassID.AnimatorController:
					{
						AnimatorController animatorController = new AnimatorController(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, animatorController, comp);
						animatorController.LoadFrom(stream);
						return animatorController;
					}
				case UnityClassID.AssetBundle:
					{
						AssetBundle assetBundle = new AssetBundle(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, assetBundle, comp);
						assetBundle.LoadFrom(stream);
						return assetBundle;
					}
				case UnityClassID.AudioClip:
					{
						if (loadingReferencials)
						{
							return comp;
						}
						AudioClip ac = new AudioClip(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, ac, comp);
						ac.LoadFrom(stream);
						return ac;
					}
				case UnityClassID.AudioListener:
					{
						AudioListener audioListener = new AudioListener(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, audioListener, comp);
						audioListener.LoadFrom(stream);
						return audioListener;
					}
				case UnityClassID.AudioSource:
					{
						AudioSource audioSrc = new AudioSource(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, audioSrc, comp);
						audioSrc.LoadFrom(stream);
						return audioSrc;
					}
				case UnityClassID.Avatar:
					{
						if (loadingReferencials)
						{
							return comp;
						}
						Avatar avatar = new Avatar(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, avatar, comp);
						avatar.LoadFrom(stream);
						return avatar;
					}
				case UnityClassID.BoxCollider:
					{
						BoxCollider boxCol = new BoxCollider(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, boxCol, comp);
						boxCol.LoadFrom(stream);
						return boxCol;
					}
				case UnityClassID.Camera:
					{
						Camera camera = new Camera(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, camera, comp);
						camera.LoadFrom(stream);
						return camera;
					}
				case UnityClassID.CapsuleCollider:
					{
						CapsuleCollider capsuleCol = new CapsuleCollider(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, capsuleCol, comp);
						capsuleCol.LoadFrom(stream);
						return capsuleCol;
					}
				case UnityClassID.Cubemap:
					{
						Cubemap cubemap = new Cubemap(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, cubemap, comp);
						cubemap.LoadFrom(stream);
						Parser.Textures.Add(cubemap);
						return cubemap;
					}
				case UnityClassID.EllipsoidParticleEmitter:
					{
						EllipsoidParticleEmitter ellipsoid = new EllipsoidParticleEmitter(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, ellipsoid, comp);
						ellipsoid.LoadFrom(stream);
						return ellipsoid;
					}
				case UnityClassID.FlareLayer:
					{
						FlareLayer flareLayer = new FlareLayer(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, flareLayer, comp);
						flareLayer.LoadFrom(stream);
						return flareLayer;
					}
				case UnityClassID.Light:
					{
						Light light = new Light(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, light, comp);
						light.LoadFrom(stream);
						return light;
					}
				case UnityClassID.LinkToGameObject:
					{
						LinkToGameObject link = new LinkToGameObject(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, link, comp);
						link.LoadFrom(stream);
						return link;
					}
				case UnityClassID.LinkToGameObject223:
					{
						LinkToGameObject223 link = new LinkToGameObject223(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, link, comp);
						link.LoadFrom(stream);
						return link;
					}
				case UnityClassID.LinkToGameObject225:
					{
						LinkToGameObject225 link = new LinkToGameObject225(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, link, comp);
						link.LoadFrom(stream);
						return link;
					}
				case UnityClassID.GameObject:
					{
						GameObject gameObj = new GameObject(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, gameObj, comp);
						gameObj.LoadFrom(stream);
						return gameObj;
					}
				case UnityClassID.Material:
					{
						Material mat = new Material(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, mat, comp);
						mat.LoadFrom(stream);
						return mat;
					}
				case UnityClassID.Mesh:
					{
						if (loadingReferencials)
						{
							return comp;
						}
						Mesh mesh = new Mesh(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, mesh, comp);
						mesh.LoadFrom(stream);
						return mesh;
					}
				case UnityClassID.MeshCollider:
					{
						MeshCollider meshCol = new MeshCollider(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, meshCol, comp);
						meshCol.LoadFrom(stream);
						return meshCol;
					}
				case UnityClassID.MeshFilter:
					{
						MeshFilter meshFilter = new MeshFilter(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, meshFilter, comp);
						meshFilter.LoadFrom(stream);
						return meshFilter;
					}
				case UnityClassID.MeshRenderer:
					{
						MeshRenderer meshRenderer = new MeshRenderer(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, meshRenderer, comp);
						meshRenderer.LoadFrom(stream);
						return meshRenderer;
					}
				default:
					if (comp.classID2 == UnityClassID.MonoBehaviour)
					{
						if (Types.Count > 0)
						{
							MonoBehaviour monoBehaviour = new MonoBehaviour(this, comp.pathID, comp.classID1, comp.classID2);
							ReplaceSubfile(index, monoBehaviour, comp);
							monoBehaviour.LoadFrom(stream);
							return monoBehaviour;
						}
						else
						{
							string message = comp.classID2 + " unhandled because of absence of Types in Cabinet (*.assets)";
							if (!reported.Contains(message))
							{
								Report.ReportLog(message);
								reported.Add(message);
							}
							return comp;
						}
					}
					else
					{
						string message = "Unhandled class: " + comp.classID1 + "/" + comp.classID2;
						if (!reported.Contains(message))
						{
							Report.ReportLog(message);
							reported.Add(message);
						}
					}
					break;
				case UnityClassID.MonoScript:
					{
						if (loadingReferencials)
						{
							return comp;
						}
						MonoScript monoScript = new MonoScript(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, monoScript, comp);
						monoScript.LoadFrom(stream);
						return monoScript;
					}
				case UnityClassID.MultiLink:
					{
						MultiLink multi = new MultiLink(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, multi, comp);
						multi.LoadFrom(stream);
						return multi;
					}
				case UnityClassID.ParticleAnimator:
					{
						ParticleAnimator particleAnimator = new ParticleAnimator(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, particleAnimator, comp);
						particleAnimator.LoadFrom(stream);
						return particleAnimator;
					}
				case UnityClassID.ParticleRenderer:
					{
						ParticleRenderer particleRenderer = new ParticleRenderer(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, particleRenderer, comp);
						particleRenderer.LoadFrom(stream);
						return particleRenderer;
					}
				case UnityClassID.ParticleSystem:
					{
						ParticleSystem particleSystem = new ParticleSystem(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, particleSystem, comp);
						particleSystem.LoadFrom(stream);
						return particleSystem;
					}
				case UnityClassID.ParticleSystemRenderer:
					{
						ParticleSystemRenderer particleSystemRenderer = new ParticleSystemRenderer(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, particleSystemRenderer, comp);
						particleSystemRenderer.LoadFrom(stream);
						return particleSystemRenderer;
					}
				case UnityClassID.Projector:
					{
						Projector projector = new Projector(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, projector, comp);
						projector.LoadFrom(stream);
						return projector;
					}
				case UnityClassID.Rigidbody:
					{
						RigidBody rigidBody = new RigidBody(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, rigidBody, comp);
						rigidBody.LoadFrom(stream);
						return rigidBody;
					}
				case UnityClassID.Shader:
					{
						Shader shader = new Shader(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, shader, comp);
						shader.LoadFrom(stream);
						return shader;
					}
				case UnityClassID.SkinnedMeshRenderer:
					{
						SkinnedMeshRenderer sMesh = new SkinnedMeshRenderer(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, sMesh, comp);
						sMesh.LoadFrom(stream);
						return sMesh;
					}
				case UnityClassID.SphereCollider:
					{
						SphereCollider sphereCol = new SphereCollider(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, sphereCol, comp);
						sphereCol.LoadFrom(stream);
						return sphereCol;
					}
				case UnityClassID.Sprite:
					{
						Sprite sprite = new Sprite(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, sprite, comp);
						sprite.LoadFrom(stream);
						return sprite;
					}
				case UnityClassID.SpriteRenderer:
					{
						SpriteRenderer spriteRenderer = new SpriteRenderer(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, spriteRenderer, comp);
						spriteRenderer.LoadFrom(stream);
						return spriteRenderer;
					}
				case UnityClassID.TextAsset:
					{
						if (loadingReferencials)
						{
							return comp;
						}
						TextAsset ta = new TextAsset(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, ta, comp);
						ta.LoadFrom(stream);
						return ta;
					}
				case UnityClassID.Texture2D:
					{
						if (loadingReferencials)
						{
							return comp;
						}
						Texture2D tex = new Texture2D(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, tex, comp);
						tex.LoadFrom(stream);
						Parser.Textures.Add(tex);
						return tex;
					}
				case UnityClassID.Transform:
					{
						Transform trans = new Transform(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, trans, comp);
						trans.LoadFrom(stream);
						return trans;
					}
				}
			}
			catch
			{
				Report.ReportLog("Failed to load " + comp.classID1 + "/" + comp.classID2 + " PathID=" + comp.pathID);
				foreach (NotLoaded notLoaded in RemovedList)
				{
					if (notLoaded == comp)
					{
						RemovedList.Remove(notLoaded);
						Components.RemoveAt(index);
						notLoaded.replacement = null;
						Components.Insert(index, notLoaded);
						break;
					}
				}
			}
			return null;
		}

		public void RemoveSubfile(Component asset)
		{
			if (Components.Remove(asset))
			{
				asset.pathID = 0;
				if (!(asset is NotLoaded))
				{
					foreach (NotLoaded replaced in RemovedList)
					{
						if (replaced.replacement == asset)
						{
							replaced.replacement = null;
							replaced.pathID = 0;
							break;
						}
					}
				}
			}
		}

		public void ReplaceSubfile(int index, Component file, NotLoaded replaced)
		{
			if (index >= 0)
			{
				Components.RemoveAt(index);
				replaced.replacement = file;
				RemovedList.Add(replaced);
			}
			else
			{
				/*for (int i = Components.Count - 1; i >= 0; i--)
				{
					if (Components[i].classID1 == file.classID1)
					{
						index = i + 1;
						break;
					}
				}
				if (index < 0)*/
				{
					index = Components.Count;
				}
			}
			Components.Insert(index, file);
		}

		public void ReplaceSubfile(Component replaced, Component file)
		{
			Components.Remove(file);
			int index = Components.IndexOf(replaced);
			Components.RemoveAt(index);
			for (int i = 0; i < RemovedList.Count; i++)
			{
				NotLoaded asset = RemovedList[i];
				if (asset.replacement == replaced)
				{
					asset.replacement = file;
					break;
				}
			}
			Components.Insert(index, file);
			file.pathID = replaced.pathID;
		}

		public void UnloadSubfile(Component comp)
		{
			int idx = Components.IndexOf(comp);
			if (idx >= 0)
			{
				foreach (NotLoaded notLoaded in RemovedList)
				{
					if (notLoaded.replacement == comp)
					{
						Components.RemoveAt(idx);
						Components.Insert(idx, notLoaded);
						break;
					}
				}
			}
		}

		public static string ToString(Component subfile)
		{
			Type t = subfile.GetType();
			PropertyInfo info = t.GetProperty("m_GameObject");
			if (info != null)
			{
				PPtr<GameObject> gameObjPtr = info.GetValue(subfile, null) as PPtr<GameObject>;
				if (gameObjPtr != null)
				{
					if (gameObjPtr.instance != null)
					{
						return gameObjPtr.instance.m_Name;
					}
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
			info = t.GetProperty("m_Name");
			if (info != null)
			{
				return info.GetValue(subfile, null).ToString();
			}
			throw new Exception("Neither m_Name nor m_GameObject member " + subfile.pathID + " " + subfile.classID1);
		}
	}
}
