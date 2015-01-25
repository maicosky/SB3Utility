using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public class FastPropertyName : IObjInfo
	{
		public string name { get; set; }

		public FastPropertyName(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			name = reader.ReadNameA4();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(name);
		}
	}

	public class UnityTexEnv : IObjInfo
	{
		public PPtr<Texture2D> m_Texture { get; set; }
		public Vector2 m_Scale { get; set; }
		public Vector2 m_Offset { get; set; }

		private AssetCabinet file;

		public UnityTexEnv(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public UnityTexEnv(AssetCabinet file)
		{
			this.file = file;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Texture = new PPtr<Texture2D>(stream, file);
			m_Scale = reader.ReadVector2();
			m_Offset = reader.ReadVector2();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			file.WritePPtr(m_Texture.asset, false, stream);
			writer.Write(m_Scale);
			writer.Write(m_Offset);
		}

		public void CopyTo(UnityTexEnv dest)
		{
			if (file != dest.file)
			{
				Texture2D destTex = dest.file.Parser.GetTexture(m_Texture.instance.m_Name);
				dest.m_Texture = new PPtr<Texture2D>(destTex);
			}
			else
			{
				dest.m_Texture = new PPtr<Texture2D>(m_Texture.asset);
			}
			dest.m_Scale = m_Scale;
			dest.m_Offset = m_Offset;
		}
	}

	public class UnityPropertySheet : IObjInfo
	{
		public List<KeyValuePair<FastPropertyName, UnityTexEnv>> m_TexEnvs { get; set; }
		public List<KeyValuePair<FastPropertyName, float>> m_Floats { get; set; }
		public List<KeyValuePair<FastPropertyName, Color4>> m_Colors { get; set; }

		private AssetCabinet file;

		public UnityPropertySheet(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public UnityPropertySheet(AssetCabinet file)
		{
			this.file = file;
			m_TexEnvs = new List<KeyValuePair<FastPropertyName, UnityTexEnv>>();
			m_Floats = new List<KeyValuePair<FastPropertyName, float>>();
			m_Colors = new List<KeyValuePair<FastPropertyName, Color4>>();
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numTexEnvs = reader.ReadInt32();
			m_TexEnvs = new List<KeyValuePair<FastPropertyName, UnityTexEnv>>(numTexEnvs);
			for (int i = 0; i < numTexEnvs; i++)
			{
				m_TexEnvs.Add
				(
					new KeyValuePair<FastPropertyName, UnityTexEnv>
					(
						new FastPropertyName(stream),
						new UnityTexEnv(file, stream)
					)
				);
			}

			int numFloats = reader.ReadInt32();
			m_Floats = new List<KeyValuePair<FastPropertyName, float>>(numFloats);
			for (int i = 0; i < numFloats; i++)
			{
				m_Floats.Add
				(
					new KeyValuePair<FastPropertyName, float>
					(
						new FastPropertyName(stream),
						reader.ReadSingle()
					)
				);
			}

			int numCols = reader.ReadInt32();
			m_Colors = new List<KeyValuePair<FastPropertyName, Color4>>(numCols);
			for (int i = 0; i < numCols; i++)
			{
				FastPropertyName name = new FastPropertyName(stream);
				Color4 col = new Color4();
				col.Red = reader.ReadSingle();
				col.Green = reader.ReadSingle();
				col.Blue = reader.ReadSingle();
				col.Alpha = reader.ReadSingle();
				m_Colors.Add(new KeyValuePair<FastPropertyName, Color4>(name, col));
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_TexEnvs.Count);
			for (int i = 0; i < m_TexEnvs.Count; i++)
			{
				m_TexEnvs[i].Key.WriteTo(stream);
				m_TexEnvs[i].Value.WriteTo(stream);
			}

			writer.Write(m_Floats.Count);
			for (int i = 0; i < m_Floats.Count; i++)
			{
				m_Floats[i].Key.WriteTo(stream);
				writer.Write(m_Floats[i].Value);
			}

			writer.Write(m_Colors.Count);
			for (int i = 0; i < m_Colors.Count; i++)
			{
				m_Colors[i].Key.WriteTo(stream);
				writer.Write(m_Colors[i].Value.Red);
				writer.Write(m_Colors[i].Value.Green);
				writer.Write(m_Colors[i].Value.Blue);
				writer.Write(m_Colors[i].Value.Alpha);
			}
		}

		public void CopyTo(Material dest)
		{
			dest.m_SavedProperties.m_TexEnvs = new List<KeyValuePair<FastPropertyName, UnityTexEnv>>(m_TexEnvs.Count);
			foreach (var src in m_TexEnvs)
			{
				UnityTexEnv texEnv = new UnityTexEnv(dest.file);
				src.Value.CopyTo(texEnv);
				dest.m_SavedProperties.m_TexEnvs.Add(new KeyValuePair<FastPropertyName, UnityTexEnv>(src.Key, texEnv));
			}

			dest.m_SavedProperties.m_Floats = new List<KeyValuePair<FastPropertyName, float>>(m_Floats.Count);
			foreach (var src in m_Floats)
			{
				dest.m_SavedProperties.m_Floats.Add(src);
			}

			dest.m_SavedProperties.m_Colors = new List<KeyValuePair<FastPropertyName, Color4>>(m_Colors.Count);
			foreach (var src in m_Colors)
			{
				dest.m_SavedProperties.m_Colors.Add(src);
			}
		}
	}

	public class Material : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public PPtr<Shader> m_Shader { get; set; }
		public List<string> m_ShaderKeywords { get; set; }
		public int m_CustomRenderQueue { get; set; }
		public UnityPropertySheet m_SavedProperties { get; set; }

		public Material(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public Material(AssetCabinet file) :
			this(file, 0, UnityClassID.Material, UnityClassID.Material)
		{
			file.ReplaceSubfile(-1, this, null);
			m_SavedProperties = new UnityPropertySheet(file);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();
			m_Shader = new PPtr<Shader>(stream, file);

			int numShaderKeywords = reader.ReadInt32();
			m_ShaderKeywords = new List<string>(numShaderKeywords);
			for (int i = 0; i < numShaderKeywords; i++)
			{
				m_ShaderKeywords.Add(reader.ReadNameA4());
			}

			m_CustomRenderQueue = reader.ReadInt32();
			m_SavedProperties = new UnityPropertySheet(file, stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			file.WritePPtr(m_Shader.asset, !(m_Shader.asset is Shader), stream);

			writer.Write(m_ShaderKeywords.Count);
			for (int i = 0; i < m_ShaderKeywords.Count; i++)
			{
				writer.WriteNameA4(m_ShaderKeywords[i]);
			}

			writer.Write(m_CustomRenderQueue);
			m_SavedProperties.WriteTo(stream);
		}

		public Material Clone(AssetCabinet file)
		{
			Material mat = new Material(file);
			CopyTo(mat);
			return mat;
		}

		public void CopyTo(Material dest)
		{
			try
			{
				dest.m_Name = m_Name;
				dest.m_Shader = dest.file == file ? m_Shader : new PPtr<Shader>((Component)null);
				dest.m_ShaderKeywords = new List<string>(m_ShaderKeywords);
				dest.m_CustomRenderQueue = m_CustomRenderQueue;
				m_SavedProperties.CopyTo(dest);
			}
			catch (Exception e)
			{
				Report.ReportLog(e.ToString());
			}
		}
	}
}
