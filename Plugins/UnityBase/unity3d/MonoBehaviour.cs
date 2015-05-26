using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;

using SB3Utility;

namespace UnityPlugin
{
	public class Line : IObjInfo
	{
		public List<string> m_Words { get; set; }

		public Line(Stream stream)
		{
			LoadFrom(stream);
		}

		public Line() { }

		public Line Clone()
		{
			Line clone = new Line();
			using (MemoryStream mem = new MemoryStream())
			{
				WriteTo(mem);
				mem.Position = 0;
				clone.LoadFrom(mem);
			}
			return clone;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Words = Extensions.ReadList<string>(reader, reader.ReadNameA4U8);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			Extensions.WriteList<string>(writer, writer.WriteNameA4U8, m_Words);
		}
	}

	public class MonoBehaviour : Component, LinkedByGameObject, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public PPtr<GameObject> m_GameObject
		{
			get
			{
				return new PPtr<GameObject>
					(
						((UPPtr)Parser.type.Members[0]).Value != null ?
							((UPPtr)Parser.type.Members[0]).Value.asset : null
					);
			}
			set { ((UPPtr)Parser.type.Members[0]).Value = new PPtr<Object>(value.asset); }
		}

		public PPtr<MonoScript> m_MonoScript
		{
			get { return new PPtr<MonoScript>(((UPPtr)Parser.type.Members[2]).Value.asset); }
			set { throw new NotImplementedException(); }
		}

		public string m_Name
		{
			get
			{
				if (Parser.type.Members.Count > 3 && Parser.type.Members[3] is UClass &&
					((UClass)Parser.type.Members[3]).ClassName == "string" &&
					((UClass)Parser.type.Members[3]).Name == "m_Name")
				{
					return ((UClass)Parser.type.Members[3]).GetString();
				}

				throw new Exception(classID1 + " " + classID2 + " has no m_Name member");
			}

			set
			{
				if (Parser.type.Members.Count > 3 && Parser.type.Members[3] is UClass &&
					((UClass)Parser.type.Members[3]).ClassName == "string" &&
					((UClass)Parser.type.Members[3]).Name == "m_Name")
				{
					((UClass)Parser.type.Members[3]).SetString(value);
					return;
				}

				throw new Exception(classID1 + " " + classID2 + " has no m_Name member");
			}
		}

		public TypeParser Parser { get; set; }

		public MonoBehaviour(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public MonoBehaviour(AssetCabinet file, UnityClassID classID1) :
			this(file, 0, classID1, UnityClassID.MonoBehaviour)
		{
			file.ReplaceSubfile(-1, this, null);
		}

		public void LoadFrom(Stream stream)
		{
			for (int i = 0; i < file.Types.Count; i++)
			{
				if ((int)classID1 == file.Types[i].typeId)
				{
					Parser = new TypeParser(file, file.Types[i]);
					Parser.type.LoadFrom(stream);
					return;
				}
			}

			throw new Exception(classID2 + " " + classID1 + " not found in Types");
		}

		public static string LoadName(Stream stream)
		{
			try
			{
				BinaryReader reader = new BinaryReader(stream);
				stream.Position += 20;
				return reader.ReadNameA4();
			}
			catch
			{
				return null;
			}
		}

		public static PPtr<MonoScript> LoadMonoScriptRef(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			stream.Position += 12;
			return new PPtr<MonoScript>(stream);
		}

		public void WriteTo(Stream stream)
		{
			Parser.type.WriteTo(stream);
		}

		public MonoBehaviour Clone(AssetCabinet file)
		{
			AssetCabinet.TypeDefinition srcDef = this.file.Types.Find
			(
				delegate(AssetCabinet.TypeDefinition def)
				{
					return def.typeId == (int)this.classID1;
				}
			);
			int destId = 0, minId = 0;
			AssetCabinet.TypeDefinition destDef = null;
			for (int i = 0; i < file.Types.Count; i++)
			{
				if (AssetCabinet.CompareTypes(srcDef, file.Types[i]))
				{
					destDef = file.Types[i];
					destId = destDef.typeId;
					break;
				}
				if (file.Types[i].typeId < minId)
				{
					minId = file.Types[i].typeId;
				}
			}
			if (destId == 0)
			{
				destDef = srcDef.Clone();
				destId = destDef.typeId = minId - 1;
				file.Types.Add(destDef);
			}

			MonoBehaviour dest = new MonoBehaviour(file, (UnityClassID)destId);
			dest.Parser = new TypeParser(file, destDef);
			AssetCabinet.IncompleteClones.Add(new Tuple<Component, Component>(this, dest));
			return dest;
		}

		public void CopyTo(MonoBehaviour dest)
		{
			GameObject destGameObj = dest.m_GameObject.instance;
			if (destGameObj != null)
			{
				Transform animFrame = destGameObj.FindLinkedComponent(UnityClassID.Transform);
				while (animFrame.Parent != null)
				{
					animFrame = animFrame.Parent;
				}
				UPPtr.AnimatorRoot = animFrame;
			}
			else
			{
				UPPtr.AnimatorRoot = null;
			}
			Parser.type.CopyTo(dest.Parser.type);
		}

		public void Export(string path)
		{
			DirectoryInfo dirInfo = new DirectoryInfo(path);
			if (!dirInfo.Exists)
			{
				dirInfo.Create();
			}

			if (Parser.type.Members.Count > 4 && Parser.type.Members[4] is UClass &&
				((UClass)Parser.type.Members[4]).ClassName == "Param" &&
				((UClass)Parser.type.Members[4]).Name == "list")
			{
				Uarray arr = (Uarray)((UClass)Parser.type.Members[4]).Members[0];
				UType[] GenericMono = arr.Value;
				using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(path + "\\" + m_Name + "." + UnityClassID.MonoBehaviour), System.Text.Encoding.UTF8))
				{
					for (int i = 0; i < GenericMono.Length; i++)
					{
						UClass vectorList = (UClass)GenericMono[i].Members[0];
						arr = (Uarray)vectorList.Members[0];
						UType[] Strings = arr.Value;
						for (int j = 0; j < Strings.Length; j++)
						{
							string word = ((UClass)Strings[j]).GetString();
							for (int k = 0; k < word.Length; k++)
							{
								if (word[k] == '<' || word[k] == '>' || word[k] == '\\')
								{
									word = word.Substring(0, k) + "\\" + word.Substring(k);
								}
							}
							writer.Write(System.Text.Encoding.UTF8.GetBytes("<" + word + ">"));
						}
						writer.Write(System.Text.Encoding.UTF8.GetBytes("\r\n"));
					}
					writer.BaseStream.SetLength(writer.BaseStream.Position);
				}
			}
			else
			{
				string name = m_GameObject.instance != null ? m_GameObject.instance.m_Name : m_Name;
				using (FileStream stream = File.OpenWrite(path + "\\" + name + "." + UnityClassID.MonoBehaviour))
				{
					Parser.type.WriteTo(stream);
					stream.SetLength(stream.Position);
				}
			}
		}

		public static MonoBehaviour Import(string filePath, AssetCabinet file)
		{
			foreach (var typeDef in file.Types)
			{
				if (typeDef.definitions.type == UnityClassID.MonoBehaviour.ToString() &&
					typeDef.definitions.children.Length > 4)
				{
					var member = typeDef.definitions.children[4];
					if (member.type == "Param" && member.identifier == "list")
					{
						MonoBehaviour m = new MonoBehaviour(null, 0, (UnityClassID)typeDef.typeId, UnityClassID.MonoBehaviour);
						m.Parser = new TypeParser(file, typeDef);
						m.m_Name = Path.GetFileNameWithoutExtension(filePath);
						Uarray ParamListArr = (Uarray)m.Parser.type.Members[4].Members[0];
						List<Line> lines = LoadLines(filePath);
						ParamListArr.Value = new UType[lines.Count];
						Type genericMonoType = ParamListArr.Members[1].GetType();
						ConstructorInfo genericMonoCtrInfo = genericMonoType.GetConstructor(new Type[] { genericMonoType });
						for (int i = 0; i < lines.Count; i++)
						{
							ParamListArr.Value[i] = (UType)genericMonoCtrInfo.Invoke(new object[] { ParamListArr.Members[1] });
							UClass GenericMonoData = (UClass)ParamListArr.Value[i];
							Uarray vectorListArr = (Uarray)GenericMonoData.Members[0].Members[0];
							UClass[] Strings = new UClass[lines[i].m_Words.Count];
							vectorListArr.Value = Strings;
							Type stringType = vectorListArr.Members[1].GetType();
							ConstructorInfo stringCtrInfo = genericMonoType.GetConstructor(new Type[] { stringType });
							for (int j = 0; j < lines[i].m_Words.Count; j++)
							{
								Strings[j] = (UClass)stringCtrInfo.Invoke(new object[] { vectorListArr.Members[1] });
								Strings[j].SetString(lines[i].m_Words[j]);
							}
						}
						return m;
					}
				}
			}
			Report.ReportLog("Warning! No definition of required type found!");
			return null;
		}

		private static List<Line> LoadLines(string filePath)
		{
			List<Line> lines = new List<Line>();
			using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath), System.Text.Encoding.UTF8))
			{
				for (int lineIdx = 0; reader.BaseStream.Position < reader.BaseStream.Length; lineIdx++)
				{
					Line l = new Line();
					l.m_Words = new List<string>();
					for (int wordIdx = 0; ; wordIdx++)
					{
						StringBuilder word = new StringBuilder();
						char c = (char)0;
						try
						{
							while ((c = reader.ReadChar()) != '\r')
							{
								if (c == '\\')
								{
									c = reader.ReadChar();
								}
								else
								{
									if (c == '<')
									{
										word.Clear();
										if ((c = reader.ReadChar()) == '\\')
										{
											c = reader.ReadChar();
										}
									}
									if (c == '>')
									{
										l.m_Words.Add(word.ToString());
										break;
									}
								}
								word.Append(c);
							}
							if ((c = reader.ReadChar()) == '\r' || c == '\n')
							{
								break;
							}
						}
						catch (EndOfStreamException)
						{
							break;
						}
					}
					if (l.m_Words.Count == 0)
					{
						break;
					}
					lines.Add(l);
				}
			}
			return lines;
		}
	}
}
