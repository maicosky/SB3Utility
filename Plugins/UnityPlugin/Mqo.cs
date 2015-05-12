using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public static partial class Plugins
	{
		/// <summary>
		/// Exports the specified meshes to Metasequoia format.
		/// </summary>
		/// <param name="parser"><b>[DefaultVar]</b> The Animator.</param>
		/// <param name="meshNames"><b>(string[])</b> The names of the MeshRenderers to export.</param>
		/// <param name="dirPath">The destination directory.</param>
		/// <param name="singleMqo"><b>True</b> will export all meshes in a single file. <b>False</b> will export a file per mesh.</param>
		/// <param name="worldCoords"><b>True</b> will transform vertices into world coordinates by multiplying them by their parent frames. <b>False</b> will keep their local coordinates.</param>
		[Plugin]
		public static void ExportMqo([DefaultVar]Animator parser, object[] meshes, string dirPath, bool singleMqo, bool worldCoords, bool sortMeshes)
		{
			MeshRenderer[] meshArray = Utility.Convert<MeshRenderer>(meshes);
			List<MeshRenderer> meshList = new List<MeshRenderer>(meshArray);
			if (sortMeshes)
			{
				meshList.Sort
				(
					delegate(MeshRenderer m1, MeshRenderer m2)
					{
						if (m1 == null)
						{
							return m2 == null ? 0 : -1;
						}
						else
						{
							if (m2 == null)
							{
								return 1;
							}
							else
							{
								string m1Name = m1.m_GameObject.instance.m_Name;
								string m2Name = m2.m_GameObject.instance.m_Name;
								int retval = m1Name.Length.CompareTo(m2Name.Length);
								return retval != 0 ? retval : m1Name.CompareTo(m2Name);
							}
						}
					}
				);
			}
			Mqo.Exporter.Export(dirPath, parser, meshList, singleMqo, worldCoords);
		}

		[Plugin]
		public static void ExportMorphMqo([DefaultVar]string dirPath, Animator parser, string meshName)
		{
			SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)Operations.FindMesh(parser.RootTransform, meshName);
			if (sMesh != null)
			{
				if (dirPath == null)
				{
					dirPath = parser.file.Parser.FilePath;
					if (dirPath.ToLower().EndsWith(".unity3d"))
					{
						dirPath = dirPath.Substring(0, dirPath.Length - 8);
					}
					dirPath += @"\" + parser.m_GameObject.instance.m_Name;
				}
				Mqo.ExporterMorph.Export(dirPath, parser, sMesh);
			}
		}
	}

	public class Mqo
	{
		public class Exporter
		{
			public static void Export(string dirPath, Animator parser, List<MeshRenderer> meshes, bool singleMqo, bool worldCoords)
			{
				DirectoryInfo dir = new DirectoryInfo(dirPath);
				List<Texture2D> usedTextures = null;
				if (singleMqo)
				{
					try
					{
						string dest = Utility.GetDestFile(dir, "meshes", ".mqo");
						usedTextures = Export(dest, parser, meshes, worldCoords);
						Report.ReportLog("Finished exporting meshes to " + dest);
					}
					catch (Exception ex)
					{
						Report.ReportLog("Error exporting meshes: " + ex.Message);
					}
				}
				else
				{
					for (int i = 0; i < meshes.Count; i++)
					{
						try
						{
							string frameName = meshes[i].m_GameObject.instance.m_Name;
							string dest = dir.FullName + @"\" + frameName + ".mqo";
							List<Texture2D> texList = Export(dest, parser, new List<MeshRenderer> { meshes[i] }, worldCoords);
							foreach (Texture2D tex in texList)
							{
								if (!usedTextures.Contains(tex))
								{
									usedTextures.Add(tex);
								}
							}
							Report.ReportLog("Finished exporting mesh to " + dest);
						}
						catch (Exception ex)
						{
							Report.ReportLog("Error exporting mesh: " + ex.Message);
						}
					}
				}

				foreach (Texture2D tex in usedTextures)
				{
					tex.Export(dirPath);
				}
			}

			private static List<Texture2D> Export(string dest, Animator parser, List<MeshRenderer> meshes, bool worldCoords)
			{
				DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(dest));
				if (!dir.Exists)
				{
					dir.Create();
				}

				Plugins.UnityConverter conv = new Plugins.UnityConverter(parser, meshes, false, false);
				List<Material> materialList = new List<Material>(meshes.Count);
				using (StreamWriter writer = new StreamWriter(dest, false))
				{
					for (int i = 0; i < meshes.Count; i++)
					{
						MeshRenderer meshRenderer = meshes[i];
						ImportedMesh meshListSome = conv.MeshList[i];
						for (int j = 0; j < meshListSome.SubmeshList.Count; j++)
						{
							Material mat = meshRenderer.m_Materials[j].instance;
							if (mat != null)
							{
								if (!materialList.Contains(mat))
								{
									materialList.Add(mat);
								}
							}
							else
							{
								Report.ReportLog("Warning: Mesh " + meshes[i].m_GameObject.instance.m_Name + " Object " + j + " has an invalid material");
							}
						}
					}

					writer.WriteLine("Metasequoia Document");
					writer.WriteLine("Format Text Ver 1.0");
					writer.WriteLine();
					writer.WriteLine("Material " + materialList.Count + " {");
					for (int matIdx = 0; matIdx < materialList.Count; matIdx++)
					{
						Material mat = materialList[matIdx];
						string s = "\t\"" + mat.m_Name + "\" col(0.800 0.800 0.800 1.000) dif(0.500) amb(0.100) emi(0.500) spc(0.100) power(30.00)";
						try
						{
							Texture2D tex = mat.m_SavedProperties.m_TexEnvs[0].Value.m_Texture.instance;
							if (tex != null)
							{
								string matTexName = tex.m_Name;
								string extension = tex.m_TextureFormat == TextureFormat.DXT1 || tex.m_TextureFormat == TextureFormat.DXT5 ? ".dds" : ".tga";
								s += " tex(\"" + Path.GetFileName(matTexName) + extension + "\")";
							}
						}
						catch { }
						writer.WriteLine(s);
					}
					writer.WriteLine("}");

					Random rand = new Random();
					for (int i = 0; i < meshes.Count; i++)
					{
						MeshRenderer mesh = meshes[i];
						if (worldCoords)
						{
							Transform parent = Operations.FindFrame(meshes[i].m_GameObject.instance.m_Name, parser.RootTransform);
							conv.WorldCoordinates(i, Transform.WorldTransform(parent));
						}

						string meshName = mesh.m_GameObject.instance.m_Name;
						ImportedMesh meshListSome = conv.MeshList[i];
						for (int j = 0; j < meshListSome.SubmeshList.Count; j++)
						{
							ImportedSubmesh meshObj = meshListSome.SubmeshList[j];
							Material mat = mesh.m_Materials[j].instance;
							int mqoMatIdx = -1;
							if (mat != null)
							{
								mqoMatIdx = materialList.IndexOf(mat);
							}
							float[] color = new float[3];
							for (int k = 0; k < color.Length; k++)
							{
								color[k] = (float)((rand.NextDouble() / 2) + 0.5);
							}

							string mqoName = meshName + "[" + j + "]";
							if (worldCoords)
							{
								mqoName += "[W]";
							}
							writer.WriteLine("Object \"" + mqoName + "\" {");
							writer.WriteLine("\tshading 1");
							writer.WriteLine("\tcolor " + color[0].ToFloatString() + " " + color[1].ToFloatString() + " " + color[2].ToFloatString());
							writer.WriteLine("\tcolor_type 1");

							List<ImportedVertex> vertList = meshObj.VertexList;
							List<ImportedFace> faceList = meshObj.FaceList;
							SB3Utility.Mqo.ExporterCommon.WriteMeshObject(writer, vertList, faceList, mqoMatIdx, null);
							writer.WriteLine("}");
						}
					}
					writer.WriteLine("Eof");
				}

				List<Texture2D> usedTextures = new List<Texture2D>(meshes.Count);
				foreach (Material mat in materialList)
				{
					try
					{
						Texture2D matTex = mat.m_SavedProperties.m_TexEnvs[0].Value.m_Texture.instance;
						if (matTex != null && !usedTextures.Contains(matTex))
						{
							usedTextures.Add(matTex);
						}
					}
					catch { }
				}
				return usedTextures;
			}
		}

		public class ExporterMorph
		{
			private Animator parser = null;
			private SkinnedMeshRenderer morphObj = null;

			private bool[][] colorLists;
			private List<ImportedVertex>[] vertLists;
			private List<ImportedFace> faceList;
			private List<Texture2D> usedTextures;

			public static void Export(string dirPath, Animator parser, SkinnedMeshRenderer morphObj)
			{
				DirectoryInfo dir = new DirectoryInfo(dirPath);
				ExporterMorph exporter = new ExporterMorph(dir, parser, morphObj);
				exporter.Export(dir);
			}

			private ExporterMorph(DirectoryInfo dir, Animator parser, SkinnedMeshRenderer morphObj)
			{
				this.parser = parser;
				this.morphObj = morphObj;
			}

			private void Export(DirectoryInfo dir)
			{
				try
				{
					List<MeshRenderer> meshList = new List<MeshRenderer>(1);
					meshList.Add(morphObj);
					Mesh mesh = Operations.GetMesh(morphObj);

					colorLists = new bool[mesh.m_Shapes.shapes.Count][];
					vertLists = new List<ImportedVertex>[mesh.m_Shapes.shapes.Count];
					for (int i = 0; i < mesh.m_Shapes.shapes.Count; i++)
					{
						Plugins.UnityConverter conv = new Plugins.UnityConverter(parser, meshList, false, false);
						ImportedMesh meshObjBase = conv.MeshList[0];
						if (faceList == null)
						{
							faceList = meshObjBase.SubmeshList[0].FaceList;
						}

						List<ImportedVertex> vertList = conv.MeshList[0].SubmeshList[0].VertexList;
						vertLists[i] = vertList;
						colorLists[i] = new bool[meshObjBase.SubmeshList[0].VertexList.Count];
						int lastVertIndex = (int)(mesh.m_Shapes.shapes[i].firstVertex + mesh.m_Shapes.shapes[i].vertexCount);
						for (int j = (int)mesh.m_Shapes.shapes[i].firstVertex; j < lastVertIndex; j++)
						{
							BlendShapeVertex srcVert = mesh.m_Shapes.vertices[j];
							ImportedVertex vert = vertList[(int)srcVert.index];
							vert.Position += srcVert.vertex;
							colorLists[i][(int)srcVert.index] = true;
						}
					}

					string dest = Utility.GetDestFile(dir, morphObj.m_GameObject.instance.m_Name + "-" + mesh.m_Name + "-", ".morph.mqo");
					Material mat = morphObj.m_Materials[0].instance;
					Export(dest, mat, mat.m_SavedProperties.m_TexEnvs[0].Value.m_Texture.instance);
					foreach (Texture2D tex in usedTextures)
					{
						try
						{
							tex.Export(dir.FullName);
						}
						catch (Exception ex)
						{
							Utility.ReportException(ex);
						}
					}
					Report.ReportLog("Finished exporting morph to " + dest);
				}
				catch (Exception ex)
				{
					Report.ReportLog("Error exporting morph: " + ex.Message);
				}
			}

			private void Export(string dest, Material mat, Texture2D tex)
			{
				DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(dest));
				if (!dir.Exists)
				{
					dir.Create();
				}

				usedTextures = new List<Texture2D>(1);
				using (StreamWriter writer = new StreamWriter(dest, false))
				{
					writer.WriteLine("Metasequoia Document");
					writer.WriteLine("Format Text Ver 1.0");
					writer.WriteLine();

					if (mat != null)
					{
						writer.WriteLine("Material 1 {");
						string s = "\t\"" + mat.m_Name + "\" vcol(1) col(0.800 0.800 0.800 1.000) dif(0.500) amb(0.100) emi(0.500) spc(0.100) power(30.00)";
						if (tex != null)
						{
							string extension = tex.m_TextureFormat == TextureFormat.DXT1 || tex.m_TextureFormat == TextureFormat.DXT5 ? ".dds" : ".tga";
							s += " tex(\"" + tex.m_Name + extension + "\")";
							usedTextures.Add(tex);
						}
						writer.WriteLine(s);
						writer.WriteLine("}");
					}

					Mesh mesh = Operations.GetMesh(morphObj);
					Random rand = new Random();
					int vertListIdx = 0;
					for (int i = 0; i < mesh.m_Shapes.shapes.Count; i++)
					{
						float[] color = new float[3];
						for (int k = 0; k < color.Length; k++)
						{
							color[k] = (float)((rand.NextDouble() / 2) + 0.5);
						}

						writer.WriteLine("Object \"" + mesh.m_Shapes.channels[i].name + "\" {");
						writer.WriteLine("\tvisible 0");
						writer.WriteLine("\tshading 1");
						writer.WriteLine("\tcolor " + color[0].ToFloatString() + " " + color[1].ToFloatString() + " " + color[2].ToFloatString());
						writer.WriteLine("\tcolor_type 1");
						SB3Utility.Mqo.ExporterCommon.WriteMeshObject(writer, vertLists[vertListIdx++], faceList, mat != null ? 0 : -1, colorLists[i]);
						writer.WriteLine("}");
					}

					writer.WriteLine("Eof");
				}
			}
		}
	}
}
