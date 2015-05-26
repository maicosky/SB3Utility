using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using SB3Utility;

namespace UnityPlugin
{
	public class TextAsset : Component
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public string m_Script { get; set; }
		public string m_PathName { get; set; }

		public TextAsset(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public TextAsset(AssetCabinet file)
			: this(file, 0, UnityClassID.TextAsset, UnityClassID.TextAsset)
		{
			file.ReplaceSubfile(-1, this, null);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4U8();
			m_Script = reader.ReadNameA4U8();
			m_PathName = reader.ReadNameA4U8();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4U8(m_Name);
			writer.WriteNameA4U8(m_Script);
			writer.WriteNameA4U8(m_PathName);
		}

		public TextAsset Clone(AssetCabinet file)
		{
			Component text = file.Components.Find
			(
				delegate(Component asset)
				{
					return asset.classID1 == UnityClassID.TextAsset &&
						(asset is NotLoaded ? ((NotLoaded)asset).Name : ((TextAsset)asset).m_Name) == m_Name;
				}
			);
			if (text == null)
			{
				file.MergeTypeDefinition(this.file, UnityClassID.TextAsset);

				TextAsset dest = new TextAsset(file);
				using (MemoryStream mem = new MemoryStream())
				{
					this.WriteTo(mem);
					mem.Position = 0;
					dest.LoadFrom(mem);
				}
				return dest;
			}
			else if (text is NotLoaded)
			{
				NotLoaded notLoaded = (NotLoaded)text;
				text = file.LoadComponent(file.SourceStream, notLoaded);
			}
			return (TextAsset)text;
		}

		public void Export(string path)
		{
			DirectoryInfo dirInfo = new DirectoryInfo(path);
			if (!dirInfo.Exists)
			{
				dirInfo.Create();
			}

			using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(path + "\\" + m_Name + "." + UnityClassID.TextAsset), System.Text.Encoding.UTF8))
			{
				string script = m_Script.IndexOf('\r') == -1 ? m_Script.Replace("\n", "\r\n") : m_Script;
				writer.Write(System.Text.Encoding.UTF8.GetBytes(script));
				writer.BaseStream.SetLength(writer.BaseStream.Position);
			}
		}

		public static TextAsset Import(string filePath)
		{
			TextAsset ta = new TextAsset(null, 0, UnityClassID.TextAsset, UnityClassID.TextAsset);
			ta.m_Name = Path.GetFileNameWithoutExtension(filePath);
			using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath), System.Text.Encoding.UTF8))
			{
				ta.m_Script = new string(reader.ReadChars((int)reader.BaseStream.Length));
			}
			ta.m_PathName = string.Empty;
			return ta;
		}
	}
}
