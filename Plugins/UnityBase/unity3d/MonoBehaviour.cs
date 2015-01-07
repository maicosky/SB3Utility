using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

	public class MonoBehaviour : Component
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public int[] unknown1 { get; set; }
		public string m_Name { get; set; }
		public List<Line> m_Lines { get; set; }

		public MonoBehaviour(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			unknown1 = reader.ReadInt32Array(5);
			m_Name = reader.ReadNameA4();

			int numLines = reader.ReadInt32();
			switch (unknown1[4])
			{
			case 2:
				m_Lines = new List<Line>(numLines);
				for (int i = 0; i < numLines; i++)
				{
					m_Lines.Add(new Line(stream));
				}
				break;
			case 102:
			default:
				throw new Exception("MonoBehaviour Subformat=" + unknown1[4] + " not supported");
			}
		}

		public static string LoadName(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			int[] unknown1 = reader.ReadInt32Array(5);
			return reader.ReadNameA4();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(unknown1);
			writer.WriteNameA4(m_Name);

			writer.Write(m_Lines.Count);
			for (int i = 0; i < m_Lines.Count; i++)
			{
				m_Lines[i].WriteTo(stream);
			}
		}

		public void Export(string path)
		{
			DirectoryInfo dirInfo = new DirectoryInfo(path);
			if (!dirInfo.Exists)
			{
				dirInfo.Create();
			}

			using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(path + "\\" + m_Name + ".MonoBehaviour"), System.Text.Encoding.UTF8))
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
			}
		}

		public static MonoBehaviour Import(string filePath)
		{
			MonoBehaviour m = new MonoBehaviour(null, 0, (UnityClassID)(int)-1, UnityClassID.MonoBehaviour);
			m.unknown1 = new int[5] { 0, 0, 1, 0, 2 };
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
