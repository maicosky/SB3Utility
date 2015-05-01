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

		public PPtr<GameObject> m_GameObject { get; set; }
		public uint Unknown1 { get; set; }
		public PPtr<MonoScript> m_MonoScript { get; set; }

		public string m_Name { get; set; }
		public List<Line> m_Lines { get; set; }

		public uint Unknown2 { get; set; }
		public List<PPtr<Object>> m_RawRefs { get; set; }
		public byte[] RawData { get; set; }

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
			throw new NotImplementedException();
		}

		public void LoadFrom(Stream stream, uint size)
		{
			try
			{
				long start = stream.Position;
				BinaryReader reader = new BinaryReader(stream);
				m_GameObject = new PPtr<GameObject>(stream, file);
				Unknown1 = reader.ReadUInt32();
				m_MonoScript = new PPtr<MonoScript>(stream, file);

				int nameLength = reader.ReadInt32();
				stream.Position -= 4;
				m_Name = nameLength > 0 && 8 + 4 + 8 + 4 + nameLength + 4 < size ? reader.ReadNameA4() : String.Empty;
				int rest = (int)(size - (stream.Position - start));
				if (m_Name.Length > 0 && rest > 4 && rest < 80 * 1024)
				{
					int numLines = reader.ReadInt32();
					m_Lines = new List<Line>(numLines);
					for (int i = 0; i < numLines; i++)
					{
						m_Lines.Add(new Line(stream));
					}
				}
				else
				{
					if (rest >= 12)
					{
						Unknown2 = reader.ReadUInt32();
						m_RawRefs = new List<PPtr<Object>>();
						PPtr<Object> testRef = new PPtr<Object>(stream);
						if ((testRef.m_FileID == 0 || testRef.m_FileID == 1) && testRef.m_PathID != 0)
						{
							if (testRef.m_FileID == 0 && rest - 12 - testRef.m_PathID * 8 >= 0)
							{
								m_RawRefs.Capacity = testRef.m_PathID;
								int numRefs = testRef.m_PathID;
								for (int i = 0; i < numRefs; i++)
								{
									testRef = new PPtr<Object>(stream);
									Component asset = file.FindComponent(testRef.m_PathID);
									if (asset != null)
									{
										if (asset is NotLoaded)
										{
											long pos = stream.Position;
											Component comp = file.LoadComponent(stream, (NotLoaded)asset);
											if (comp != null)
											{
												asset = comp;
											}
											stream.Position = pos;
										}
										PPtr<Object> newRef = new PPtr<Object>(asset);
										newRef.m_FileID = testRef.m_FileID;
										m_RawRefs.Add(newRef);
									}
								}
								rest -= 12 + numRefs * 8;
							}
							else
							{
								Component asset = file.FindComponent(testRef.m_PathID);
								if (asset != null)
								{
									if (asset is NotLoaded)
									{
										long pos = stream.Position;
										Component comp = file.LoadComponent(stream, (NotLoaded)asset);
										if (comp != null)
										{
											asset = comp;
										}
										stream.Position = pos;
									}
									rest -= 12;
									PPtr<Object> newRef = new PPtr<Object>(asset);
									newRef.m_FileID = testRef.m_FileID;
									m_RawRefs.Add(newRef);
								}
								else
								{
									stream.Position -= 12;
								}
							}
						}
						else
						{
							stream.Position -= 12;
						}
					}
					RawData = reader.ReadBytes(rest);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
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

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			file.WritePPtr(m_GameObject.asset, m_GameObject.m_FileID != 0, stream);
			writer.Write(Unknown1);
			file.WritePPtr(m_MonoScript.asset, m_MonoScript.m_FileID != 0, stream);

			if (m_Name.Length > 0)
			{
				writer.WriteNameA4(m_Name);
			}
			if (m_Lines != null)
			{
				writer.Write(m_Lines.Count);
				for (int i = 0; i < m_Lines.Count; i++)
				{
					m_Lines[i].WriteTo(stream);
				}
			}
			else
			{
				if (m_RawRefs.Count > 0)
				{
					writer.Write(Unknown2);
					if (m_RawRefs.Count > 1)
					{
						writer.Write((int)0);
						writer.Write(m_RawRefs.Count);
					}
					for (int i = 0; i < m_RawRefs.Count; i++)
					{
						file.WritePPtr(m_RawRefs[i].asset, m_RawRefs[i].m_FileID != 0, stream);
					}
				}
				writer.Write(RawData);
			}
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
			// deep compare all MonoBehaviour types
			// duplicate with unique classID1 in file
			// dynamically create that class to get all references

			MonoBehaviour dest = new MonoBehaviour(file, classID1);
			dest.Unknown1 = Unknown1;
			Component script = null;
			if (m_MonoScript.instance != null)
			{
				script = file.Bundle.FindComponent(m_MonoScript.instance.m_Name, UnityClassID.MonoScript);
				if (script == null)
				{
					script = m_MonoScript.instance.Clone(file);
					file.Bundle.AddComponent(script);
				}
			}
			dest.m_MonoScript = new PPtr<MonoScript>(script);
			dest.m_Name = m_Name;

			if (m_Lines != null)
			{
				dest.m_Lines = new List<Line>(m_Lines.Count);
				for (int i = 0; i < m_Lines.Count; i++)
				{
					Line l = m_Lines[i];
					dest.m_Lines.Add(l.Clone());
				}
			}
			else
			{
				if (m_RawRefs.Count > 0)
				{
					AssetCabinet.IncompleteClones.Add(new Tuple<Component, Component>(this, dest));
				}
				dest.RawData = (byte[])RawData.Clone();
			}
			return dest;
		}

		private static HashSet<string> msgFilter = new HashSet<string>();

		public void CopyTo(MonoBehaviour dest)
		{
			dest.Unknown2 = Unknown2;
			dest.m_RawRefs = new List<PPtr<Object>>(m_RawRefs.Count);
			for (int i = 0; i < m_RawRefs.Count; i++)
			{
				Component asset = dest.FindOrClone(m_RawRefs[i].asset, dest.file);
				if (asset == null)
				{
					string msg = "No counterpart found for " + m_RawRefs[i].asset.classID2 + " " + AssetCabinet.ToString(m_RawRefs[i].asset);
					if (!msgFilter.Contains(msg))
					{
						msgFilter.Add(msg);
						Report.ReportLog(msg);
					}
				}
				dest.m_RawRefs.Add(new PPtr<Object>(asset));
			}
		}

		Component FindOrClone(Component asset, AssetCabinet destFile)
		{
			switch (asset.classID2)
			{
			case UnityClassID.Transform:
				Transform animFrame = m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
				while (animFrame.Parent != null)
				{
					animFrame = animFrame.Parent;
				}
				return Operations.FindFrame(((Transform)asset).m_GameObject.instance.m_Name, animFrame);
			case UnityClassID.Texture2D:
				string name = AssetCabinet.ToString(asset);
				Component texFound = destFile.Bundle.FindComponent(name, UnityClassID.Texture2D);
				if (texFound != null)
				{
					return texFound;
				}
				Texture2D clone = ((Texture2D)asset).Clone(destFile);
				file.Bundle.AddComponent(clone);
				return clone;
			}
			return null;
		}

		public void Export(string path)
		{
			DirectoryInfo dirInfo = new DirectoryInfo(path);
			if (!dirInfo.Exists)
			{
				dirInfo.Create();
			}

			if (RawData != null)
			{
				string name = m_GameObject.instance != null ? m_GameObject.instance.m_Name : m_Name;
				using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(path + "\\" + name + "." + UnityClassID.MonoBehaviour)))
				{
					writer.Write(RawData);
					writer.BaseStream.SetLength(writer.BaseStream.Position);
				}
			}
			else
			{
				using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(path + "\\" + m_Name + "." + UnityClassID.MonoBehaviour), System.Text.Encoding.UTF8))
				{
					for (int i = 0; i < m_Lines.Count; i++)
					{
						for (int j = 0; j < m_Lines[i].m_Words.Count; j++)
						{
							string word = m_Lines[i].m_Words[j];
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
		}

		public static MonoBehaviour Import(string filePath)
		{
			MonoBehaviour m = new MonoBehaviour(null, 0, (UnityClassID)(int)-1, UnityClassID.MonoBehaviour);
			m.m_GameObject = new PPtr<GameObject>((Component)null);
			m.Unknown1 = 1;
			m.m_MonoScript = new PPtr<MonoScript>((Component)null);
			m.m_Name = Path.GetFileNameWithoutExtension(filePath);
			m.m_Lines = new List<Line>();
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
					m.m_Lines.Add(l);
				}
			}
			return m;
		}
	}
}
