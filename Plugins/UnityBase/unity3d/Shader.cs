using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using SB3Utility;

namespace UnityPlugin
{
	public class Shader : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public string m_Script { get; set; }
		public string m_PathName { get; set; }
		public List<PPtr<Shader>> m_Dependencies { get; set; }
		public bool m_ShaderIsBaked { get; set; }

		public Shader(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public Shader(AssetCabinet file) :
			this(file, 0, UnityClassID.Shader, UnityClassID.Shader)
		{
			file.ReplaceSubfile(-1, this, null);

			m_Dependencies = new List<PPtr<Shader>>();
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();
			m_Script = reader.ReadNameA4();
			m_PathName = reader.ReadNameA4();

			int numDependencies = reader.ReadInt32();
			m_Dependencies = new List<PPtr<Shader>>(numDependencies);
			for (int i = 0; i < numDependencies; i++)
			{
				m_Dependencies.Add(new PPtr<Shader>(stream, file));
			}

			m_ShaderIsBaked = reader.ReadBoolean();
			reader.ReadBytes(3);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			writer.WriteNameA4(m_Script);
			writer.WriteNameA4(m_PathName);

			writer.Write(m_Dependencies.Count);
			for (int i = 0; i < m_Dependencies.Count; i++)
			{
				m_Dependencies[i].WriteTo(stream);
			}

			writer.Write(m_ShaderIsBaked);
			writer.Write(new byte[3]);
		}

		public Shader Clone(AssetCabinet file)
		{
			Component sha = file.Bundle != null ? file.Bundle.FindComponent(m_Name, UnityClassID.Shader) : null;
			if (sha == null)
			{
				file.MergeTypeDefinition(this.file, UnityClassID.Shader);

				Shader dest = new Shader(file);
				if (file.Bundle != null)
				{
					file.Bundle.AddComponent(m_Name, dest);
				}
				dest.m_Name = m_Name;
				dest.m_Script = m_Script;
				dest.m_PathName = m_PathName;
				foreach (PPtr<Shader> asset in m_Dependencies)
				{
					sha = asset.asset;
					if (sha != null)
					{
						Type t = sha.GetType();
						MethodInfo info = t.GetMethod("Clone", new Type[] { typeof(AssetCabinet) });
						sha = (Component)info.Invoke(sha, new object[] { file });
					}
					dest.m_Dependencies.Add(new PPtr<Shader>(sha));
				}
				dest.m_ShaderIsBaked = m_ShaderIsBaked;
				return dest;
			}
			else if (sha is NotLoaded)
			{
				NotLoaded notLoaded = (NotLoaded)sha;
				if (notLoaded.replacement != null)
				{
					sha = notLoaded.replacement;
				}
				else
				{
					sha = file.LoadComponent(file.SourceStream, notLoaded);
				}
			}
			return (Shader)sha;
		}

		public void Export(string path)
		{
			DirectoryInfo dirInfo = new DirectoryInfo(path);
			if (!dirInfo.Exists)
			{
				dirInfo.Create();
			}

			using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(path + "\\" + m_Name + "." + UnityClassID.Shader), System.Text.Encoding.UTF8))
			{
				writer.Write(System.Text.Encoding.UTF8.GetBytes(m_Script));
				writer.Write(System.Text.Encoding.UTF8.GetBytes("\n// Dependencies:\n"));
				foreach (PPtr<Shader> shaderPtr in m_Dependencies)
				{
					Component shader = shaderPtr.asset;
					writer.Write(System.Text.Encoding.UTF8.GetBytes("//\t" + (shader != null ? shader.classID1 + " " + AssetCabinet.ToString(shader) : "NULL") + "\n"));
				}
				writer.BaseStream.SetLength(writer.BaseStream.Position);
			}
		}

		public static Shader Import(string filePath)
		{
			Shader s = new Shader(null, 0, UnityClassID.Shader, UnityClassID.Shader);
			s.m_Name = Path.GetFileNameWithoutExtension(filePath);
			using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath), System.Text.Encoding.UTF8))
			{
				s.m_Script = new string(reader.ReadChars((int)reader.BaseStream.Length));
			}
			s.m_PathName = string.Empty;
			return s;
		}
	}
}