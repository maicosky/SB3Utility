using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SlimDX;

using SB3Utility;
using ODFPlugin;

namespace ODFPluginOld
{
	public static partial class Plugins
	{
		/// <summary>
		/// Exports the specified meshes to Metasequoia format.
		/// </summary>
		/// <param name="parser"><b>[DefaultVar]</b> The odfParser.</param>
		/// <param name="meshNames"><b>(string[])</b> The names of the meshes to export.</param>
		/// <param name="dirPath">The destination directory.</param>
		/// <param name="singleMqo"><b>True</b> will export all meshes in a single file. <b>False</b> will export a file per mesh.</param>
		/// <param name="worldCoords"><b>True</b> will transform vertices into world coordinates by multiplying them by their parent frames. <b>False</b> will keep their local coordinates.</param>
		[Plugin]
		public static void ExportMqo([DefaultVar]odfParser parser, object[] meshNames, string dirPath, bool singleMqo, bool worldCoords)
		{
			List<odfMesh> meshes = new List<odfMesh>(meshNames.Length);
			foreach (string meshName in Utility.Convert<string>(meshNames))
			{
				odfMesh mesh = odf.FindMeshListSome(meshName, parser.MeshSection);
				if (mesh != null)
				{
					meshes.Add(mesh);
				}
				else
				{
					Report.ReportLog("Mesh " + meshName + " not found");
				}
			}
			Mqo.Exporter.Export(dirPath, parser, meshes, singleMqo, worldCoords);
		}

		[Plugin]
		public static void ExportMorphMqo([DefaultVar]string dirPath, odfParser odfParser, string morphObjName, bool skipUnusedProfiles)
		{
			if (dirPath == null)
				dirPath = Path.GetDirectoryName(odfParser.ODFPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(odfParser.ODFPath);
			odfMorphObject morphObj = odf.FindMorphObject(morphObjName, odfParser.MorphSection);
			Mqo.ExporterMorph.Export(dirPath, odfParser, morphObj, skipUnusedProfiles);
		}

		[Plugin]
		public static Mqo.Importer ImportMqoAsODF([DefaultVar]string path)
		{
			Console.WriteLine("ImpMqo As ODF");
			return new Mqo.Importer(path);
		}

/*		[Plugin]
		[PluginType(PluginFlags.Importer)]
		public static Mqo.ImporterMorph ImportMorphMqoAsODF([DefaultVar]string path)
		{
			Console.WriteLine("ImpMorphMqo As ODF");
			return new Mqo.ImporterMorph(path);
		}*/
	}

	public class Mqo
	{
		public class Importer : IImported
		{
			public List<ImportedFrame> FrameList { get; protected set; }
			public List<ImportedMesh> MeshList { get; protected set; }
			public List<ImportedMaterial> MaterialList { get; protected set; }
			public List<ImportedTexture> TextureList { get; protected set; }
			public List<ImportedAnimation> AnimationList { get; protected set; }
			public List<ImportedMorph> MorphList { get; protected set; }

			public Importer(string path)
			{
				try
				{
					List<string> mqoMaterials = new List<string>();
					List<MqoObject> mqoObjects = new List<MqoObject>();
					using (StreamReader reader = new StreamReader(path, Utility.EncodingShiftJIS))
					{
						string line;
						while ((line = reader.ReadLine()) != null)
						{
							if (line.Contains("Object"))
							{
								MqoObject mqoObject = ParseObject(line, reader);
								if (mqoObject != null)
								{
									mqoObjects.Add(mqoObject);
								}
							}
							else if (line.Contains("Material"))
							{
								string[] sArray = line.Split(new string[] { "\t", " " }, StringSplitOptions.RemoveEmptyEntries);
								int numMaterials = Int32.Parse(sArray[1]);
								while ((numMaterials > 0) && (line = reader.ReadLine()).Contains("\""))
								{
									int matNameStart = line.IndexOf('\"') + 1;
									int matNameEnd = line.IndexOf('\"', matNameStart);
									mqoMaterials.Add(line.Substring(matNameStart, matNameEnd - matNameStart));
									numMaterials--;
								}
							}
						}
					}

					List<List<MqoObject>> groupedMeshes = new List<List<MqoObject>>();
					for (int i = 0; i < mqoObjects.Count; i++)
					{
						bool found = false;
						for (int j = 0; j < groupedMeshes.Count; j++)
						{
							if (mqoObjects[i].name == groupedMeshes[j][0].name)
							{
								groupedMeshes[j].Add(mqoObjects[i]);
								found = true;
								break;
							}
						}
						if (!found)
						{
							List<MqoObject> group = new List<MqoObject>();
							group.Add(mqoObjects[i]);
							groupedMeshes.Add(group);
						}
					}

					MeshList = new List<ImportedMesh>(groupedMeshes.Count);
					for (int i = 0; i < groupedMeshes.Count; i++)
					{
						ImportedMesh meshList = ImportMeshList(groupedMeshes[i], mqoMaterials);
						MeshList.Add(meshList);
					}
				}
				catch (Exception e)
				{
					Report.ReportLog("Error importing .mqo: " + e.Message);
				}
			}

			private class MqoObject
			{
				public MqoVertex[] vertices = null;
				public MqoFace[] faces = null;
				public int baseIdx = -1;
				public bool worldCoords = false;
				public string name = null;
				public string fullname = null;
			}

			private class MqoVertex
			{
				public Vector3 coords;
			}

			private class MqoFace
			{
				public int materialIndex = -1;
				public int[] vertexIndices = new int[3];
				public Vector2[] UVs = new Vector2[3] { new Vector2(), new Vector2(), new Vector2() };
			}

			private class VertexMap : IComparable<VertexMap>
			{
				public int mqoIdx = -1;
				public int wsMeshIdx = -1;
				public ImportedVertex vert = null;
				public Dictionary<Vector2, VertexMap> uvDic = new Dictionary<Vector2, VertexMap>(new Vector2Comparer());

				public int CompareTo(VertexMap other)
				{
					return this.mqoIdx - other.mqoIdx;
				}
			}

			private class Vector2Comparer : IEqualityComparer<Vector2>
			{
				public bool Equals(Vector2 x, Vector2 y)
				{
					return x.Equals(y);
				}

				public int GetHashCode(Vector2 obj)
				{
					return obj.X.GetHashCode() + obj.Y.GetHashCode();
				}
			}

			private static ImportedMesh ImportMeshList(List<MqoObject> mqoObjects, List<string> mqoMaterials)
			{
				ImportedMesh meshList = new ImportedMesh();
				meshList.Name = mqoObjects[0].name;
				meshList.SubmeshList = new List<ImportedSubmesh>(mqoObjects.Count);

				int vertIdx = 0;
				foreach (MqoObject mqoObject in mqoObjects)
				{
					List<VertexMap>[] vertexMapList = new List<VertexMap>[mqoMaterials.Count + 1];
					Dictionary<int, VertexMap>[] vertexMapDic = new Dictionary<int, VertexMap>[mqoMaterials.Count + 1];
					List<VertexMap[]>[] faceMap = new List<VertexMap[]>[mqoMaterials.Count + 1];
					foreach (MqoFace mqoFace in mqoObject.faces)
					{
						int mqoFaceMatIdxOffset = mqoFace.materialIndex + 1;
						if (vertexMapList[mqoFaceMatIdxOffset] == null)
						{
							vertexMapList[mqoFaceMatIdxOffset] = new List<VertexMap>(mqoObject.vertices.Length);
							vertexMapDic[mqoFaceMatIdxOffset] = new Dictionary<int, VertexMap>();
							faceMap[mqoFaceMatIdxOffset] = new List<VertexMap[]>(mqoObject.faces.Length);
						}

						VertexMap[] faceMapArray = new VertexMap[mqoFace.vertexIndices.Length];
						faceMap[mqoFaceMatIdxOffset].Add(faceMapArray);
						for (int i = 0; i < mqoFace.vertexIndices.Length; i++)
						{
							VertexMap vertMap;
							if (!vertexMapDic[mqoFaceMatIdxOffset].TryGetValue(mqoFace.vertexIndices[i], out vertMap))
							{
								ImportedVertex vert = new ImportedVertex();
								vert.Normal = new Vector3();
								vert.UV = mqoFace.UVs[i];
								vert.Position = mqoObject.vertices[mqoFace.vertexIndices[i]].coords;

								vertMap = new VertexMap { mqoIdx = mqoFace.vertexIndices[i], vert = vert };
								vertexMapDic[mqoFaceMatIdxOffset].Add(mqoFace.vertexIndices[i], vertMap);
								vertMap.uvDic.Add(mqoFace.UVs[i], vertMap);
								vertexMapList[mqoFaceMatIdxOffset].Add(vertMap);
							}

							VertexMap uvVertMap;
							if (!vertMap.uvDic.TryGetValue(mqoFace.UVs[i], out uvVertMap))
							{
								ImportedVertex vert = new ImportedVertex();
								vert.Normal = new Vector3();
								vert.UV = mqoFace.UVs[i];
								vert.Position = mqoObject.vertices[mqoFace.vertexIndices[i]].coords;

								uvVertMap = new VertexMap { mqoIdx = Int32.MaxValue, vert = vert };
								vertMap.uvDic.Add(mqoFace.UVs[i], uvVertMap);
								vertexMapList[mqoFaceMatIdxOffset].Add(uvVertMap);
							}

							faceMapArray[i] = uvVertMap;
						}
					}

					for (int i = 0; i < vertexMapList.Length; i++)
					{
						if (vertexMapList[i] != null)
						{
							ImportedSubmesh mesh = new ImportedSubmesh();
							mesh.VertexList = new List<ImportedVertex>(vertexMapList[i].Count);
							mesh.FaceList = new List<ImportedFace>(faceMap[i].Count);
							mesh.Index = mqoObject.baseIdx;
							mesh.WorldCoords = mqoObject.worldCoords;
							int matIdx = i - 1;
							if ((matIdx >= 0) && (matIdx < mqoMaterials.Count))
							{
								mesh.Material = mqoMaterials[matIdx];
							}
							meshList.SubmeshList.Add(mesh);

							vertexMapList[i].Sort();
							for (int j = 0; j < vertexMapList[i].Count; j++)
							{
								vertexMapList[i][j].wsMeshIdx = j;
								mesh.VertexList.Add(vertexMapList[i][j].vert);
								vertIdx++;
							}

							for (int j = 0; j < faceMap[i].Count; j++)
							{
								ImportedFace face = new ImportedFace();
								face.VertexIndices = new ushort[3] { (ushort)faceMap[i][j][0].wsMeshIdx, (ushort)faceMap[i][j][2].wsMeshIdx, (ushort)faceMap[i][j][1].wsMeshIdx };
								mesh.FaceList.Add(face);
							}
						}
					}
				}

				return meshList;
			}

			private static void ParseVertices(StreamReader reader, MqoObject mqoObject)
			{
				MqoVertex[] vertices = null;
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					int countStart = line.IndexOf("vertex");
					if (countStart >= 0)
					{
						countStart += 7;
						int countEnd = line.IndexOf(' ', countStart);
						int vertexCount = Int32.Parse(line.Substring(countStart, countEnd - countStart));
						vertices = new MqoVertex[vertexCount];

						for (int i = 0; i < vertexCount; i++)
						{
							MqoVertex vertex = new MqoVertex();
							line = reader.ReadLine();
							string[] sArray = line.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
							float[] coords = new float[] { Utility.ParseFloat(sArray[0]), Utility.ParseFloat(sArray[1]), Utility.ParseFloat(sArray[2]) };

							for (int j = 0; j < 3; j++)
							{
								coords[j] /= 10f;
								if (coords[j].Equals(Single.NaN))
								{
									throw new Exception("vertex " + i + " has invalid coordinates in mesh object " + mqoObject.fullname);
								}
							}
							vertex.coords = new Vector3(coords[0], coords[1], coords[2]);
							vertices[i] = vertex;
						}
						break;
					}
				}
				mqoObject.vertices = vertices;
			}

			private static void ParseFaces(StreamReader reader, MqoObject mqoObject)
			{
				List<MqoFace> faceList = null;
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					int countStart = line.IndexOf("face");
					if (countStart >= 0)
					{
						countStart += 5;
						int countEnd = line.IndexOf(' ', countStart);
						int faceCount = Int32.Parse(line.Substring(countStart, countEnd - countStart));
						faceList = new List<MqoFace>(faceCount);

						for (int i = 0; i < faceCount; i++)
						{
							// get vertex indices & uv
							line = reader.ReadLine();
							string[] sArray = line.Split(new char[] { '\t', ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
							int numVertices = Int32.Parse(sArray[0]);
							if (numVertices > 3)
							{
								throw new Exception("Face " + i + " in mesh object " + mqoObject.fullname + " has more than 3 vertices. Triangulate the meshes");
							}
							else if (numVertices < 3)
							{
								Report.ReportLog("Warning: Skipping face " + i + " in mesh object " + mqoObject.fullname + " because it has a less than 3 vertices");
							}
							else
							{
								MqoFace face = new MqoFace();
								faceList.Add(face);

								for (int j = 1; j < sArray.Length; j++)
								{
									if (sArray[j].ToUpper() == "V")
									{
										for (int k = 0; k < face.vertexIndices.Length; k++)
										{
											face.vertexIndices[k] = Int32.Parse(sArray[++j]);
										}
									}
									else if (sArray[j].ToUpper() == "M")
									{
										face.materialIndex = Int32.Parse(sArray[++j]);
									}
									else if (sArray[j].ToUpper() == "UV")
									{
										for (int k = 0; k < face.UVs.Length; k++)
										{
											face.UVs[k] = new Vector2(Utility.ParseFloat(sArray[++j]), Utility.ParseFloat(sArray[++j]));
										}
									}
								}
							}
						}
						break;
					}
				}
				mqoObject.faces = faceList.ToArray();
			}

			private static MqoObject ParseObject(string line, StreamReader reader)
			{
				MqoObject mqoObject = new MqoObject();
				try
				{
					int nameStart = line.IndexOf('\"') + 1;
					int nameEnd = line.IndexOf('\"', nameStart);
					string name = line.Substring(nameStart, nameEnd - nameStart);
					mqoObject.fullname = name;

					if (name.Contains("[W]") || name.Contains("[w]"))
					{
						mqoObject.worldCoords = true;
						name = name.Replace("[W]", String.Empty);
						name = name.Replace("[w]", String.Empty);
					}

					int posStart;
					if ((posStart = name.LastIndexOf('[')) >= 0)
					{
						posStart++;
						int posEnd = name.LastIndexOf(']');
						int baseIdx;
						if ((posEnd > posStart) && Int32.TryParse(name.Substring(posStart, posEnd - posStart), out baseIdx))
						{
							mqoObject.baseIdx = baseIdx;
							name = name.Substring(0, posStart - 1);
						}
					}
					if ((mqoObject.baseIdx < 0) && ((posStart = name.LastIndexOf('-')) >= 0))
					{
						posStart++;
						int baseIdx;
						if (Int32.TryParse(name.Substring(posStart, name.Length - posStart), out baseIdx))
						{
							mqoObject.baseIdx = baseIdx;
							name = name.Substring(0, posStart - 1);
						}
					}
					mqoObject.name = name;

					ParseVertices(reader, mqoObject);
					ParseFaces(reader, mqoObject);
				}
				catch (Exception ex)
				{
					Report.ReportLog("Error parsing object " + mqoObject.fullname + ": " + ex.Message);
					mqoObject = null;
				}
				return mqoObject;
			}
		}

/*		public class ImporterMorph : IImported
		{
			public List<ImportedFrame> FrameList { get; protected set; }
			public List<ImportedMesh> MeshList { get; protected set; }
			public List<ImportedMaterial> MaterialList { get; protected set; }
			public List<ImportedTexture> TextureList { get; protected set; }
			public List<ImportedAnimation> AnimationList { get; protected set; }
			public List<ImportedMorph> MorphList { get; protected set; }

			public ImporterMorph(string path)
			{
				try
				{
					Importer importer = new Importer(path);
					MorphList = new List<ImportedMorph>();

					ImportedMorph morphList = new ImportedMorph();
					MorphList.Add(morphList);
					morphList.KeyframeList = new List<ImportedMorphKeyframe>(importer.MeshList.Count);
					foreach (ImportedMesh meshList in importer.MeshList)
					{
						foreach (ImportedSubmesh submesh in meshList.SubmeshList)
						{
							ImportedMorphKeyframe morph = new ImportedMorphKeyframe();
							morph.Name = meshList.Name;
							morph.VertexList = submesh.VertexList;
							morphList.KeyframeList.Add(morph);
						}
					}

					int startIdx = path.IndexOf('-') + 1;
					int endIdx = path.LastIndexOf('-');
					if (startIdx > endIdx)
					{
						int extIdx = path.ToLower().LastIndexOf(".morph.mqo");
						for (int i = extIdx - 1; i >= 0; i--)
						{
							if (!Char.IsDigit(path[i]))
							{
								endIdx = i + 1;
								break;
							}
						}
					}
					if ((startIdx > 0) && (endIdx > 0) && (startIdx < endIdx))
					{
						morphList.Name = path.Substring(startIdx, endIdx - startIdx);
					}
					if (morphList.Name == String.Empty)
					{
						morphList.Name = "(no name)";
					}
				}
				catch (Exception ex)
				{
					Report.ReportLog("Error importing .morphs.mqo: " + ex.Message);
				}
			}
		}*/

		private static class ExporterCommon
		{
			public static void WriteMeshObject(StreamWriter writer, List<ImportedVertex> vertexList, List<ImportedFace> faceList, int mqoMatIdx, bool[] colorVertex)
			{
				writer.WriteLine("\tvertex " + vertexList.Count + " {");
				for (int i = 0; i < vertexList.Count; i++)
				{
					ImportedVertex vertex = vertexList[i];
					Vector3 pos = vertex.Position * 10f;
					writer.WriteLine("\t\t" + pos.X.ToFloatString() + " " + pos.Y.ToFloatString() + " " + pos.Z.ToFloatString());
				}
				writer.WriteLine("\t}");

				writer.WriteLine("\tface " + faceList.Count + " {");
				for (int i = 0; i < faceList.Count; i++)
				{
					ImportedFace face = faceList[i];
					int[] vertIndices = new int[] { face.VertexIndices[0], face.VertexIndices[2], face.VertexIndices[1] };
					Vector2 uv1 = vertexList[vertIndices[0]].UV;
					Vector2 uv2 = vertexList[vertIndices[1]].UV;
					Vector2 uv3 = vertexList[vertIndices[2]].UV;

					writer.Write("\t\t3 V(" + vertIndices[0] + " " + vertIndices[1] + " " + vertIndices[2] + ")");
					if (mqoMatIdx >= 0)
					{
						writer.Write(" M(" + mqoMatIdx + ")");
					}
					writer.Write(" UV("
						+ uv1[0].ToFloatString() + " " + uv1[1].ToFloatString() + " "
						+ uv2[0].ToFloatString() + " " + uv2[1].ToFloatString() + " "
						+ uv3[0].ToFloatString() + " " + uv3[1].ToFloatString() + ")");
					if ((colorVertex != null) && (colorVertex[vertIndices[0]] || colorVertex[vertIndices[1]] || colorVertex[vertIndices[2]]))
					{
						string s = " COL(";
						for (int j = 0; j < vertIndices.Length; j++)
						{
							if (colorVertex[vertIndices[j]])
							{
								s += 0xFFFF0000 + " ";
							}
							else
							{
								s += 0xFF000000 + " ";
							}
						}
						s = s.Substring(0, s.Length - 1) + ")";
						writer.Write(s);
					}
					writer.WriteLine();
				}
				writer.WriteLine("\t}");
			}
		}

		public class Exporter
		{
			public static void Export(string dirPath, odfParser parser, List<odfMesh> meshes, bool singleMqo, bool worldCoords)
			{
				DirectoryInfo dir = new DirectoryInfo(dirPath);
				List<odfTexture> usedTextures = new List<odfTexture>(parser.TextureSection.Count);
				if (singleMqo)
				{
					try
					{
						string dest = Utility.GetDestFile(dir, "meshes", ".odf.mqo");
						List<odfTexture> texList = Export(dest, parser, meshes, worldCoords);
						foreach (odfTexture tex in texList)
						{
							if (!usedTextures.Contains(tex))
							{
								usedTextures.Add(tex);
							}
						}
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
							string frameName = meshes[i].Name;
							string dest = dir.FullName + @"\" + frameName + ".odf.mqo";
							List<odfTexture> texList = Export(dest, parser, new List<odfMesh> { meshes[i] }, worldCoords);
							foreach (odfTexture tex in texList)
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

				foreach (odfTexture tex in usedTextures)
				{
					String texFilePath = Path.GetDirectoryName(parser.ODFPath) + @"\" + tex.TextureFile;
					odfTextureFile odfTex = new odfTextureFile(tex.Name, texFilePath);
					odf.ExportTexture(odfTex, dir.FullName + @"\" + tex.TextureFile);
				}
			}

			private static List<odfTexture> Export(string dest, odfParser parser, List<odfMesh> meshes, bool worldCoords)
			{
				List<odfTexture> usedTextures = new List<odfTexture>(parser.TextureSection.Count);
				DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(dest));
				if (!dir.Exists)
				{
					dir.Create();
				}

				List<odfMaterial> materialList = new List<odfMaterial>(parser.MaterialSection.Count);
				Dictionary<ObjectName, ObjectName> matTexDic = new Dictionary<ObjectName, ObjectName>();
				using (StreamWriter writer = new StreamWriter(dest, false))
				{
					for (int i = 0; i < meshes.Count; i++)
					{
						odfMesh meshListSome = meshes[i];
						for (int j = 0; j < meshListSome.Count; j++)
						{
							odfSubmesh meshObj = meshListSome[j];
							odfMaterial mat = odf.FindMaterialInfo(meshObj.MaterialId, parser.MaterialSection);
							if (mat != null)
							{
								if (!materialList.Contains(mat))
								{
									materialList.Add(mat);
								}
								odfTexture tex = odf.FindTextureInfo(meshObj.TextureIds[0], parser.TextureSection);
								if (tex != null && !usedTextures.Contains(tex))
								{
									usedTextures.Add(tex);
								}
								if (tex != null && !matTexDic.ContainsKey(mat.Name))
								{
									matTexDic.Add(mat.Name, tex.Name);
								}
							}
							else
							{
								Report.ReportLog("Warning: Mesh " + meshes[i].Name + " Object " + meshObj.Name + " has an invalid material");
							}
						}
					}

					writer.WriteLine("Metasequoia Document");
					writer.WriteLine("Format Text Ver 1.0");
					writer.WriteLine();
					writer.WriteLine("Material " + materialList.Count + " {");
					foreach (odfMaterial mat in materialList)
					{
						string s = "\t\"" + mat.Name + "\" col(0.800 0.800 0.800 1.000) dif(0.500) amb(0.100) emi(0.500) spc(0.100) power(30.00)";
						ObjectName matTexName;
						if (matTexDic.TryGetValue(mat.Name, out matTexName))
						{
							s += " tex(\"" + matTexName + "\")";
						}
						writer.WriteLine(s);
					}
					writer.WriteLine("}");

					Random rand = new Random();
					for (int i = 0; i < meshes.Count; i++)
					{
						Matrix transform = Matrix.Identity;
						if (worldCoords)
						{
							odfFrame parent = odf.FindMeshFrame(meshes[i].Id, parser.FrameSection.RootFrame);
							while (parent is odfFrame)
							{
								transform = parent.Matrix * transform;
								parent = parent.Parent;
							}
						}

						odfMesh meshListSome = meshes[i];
						string meshName = meshes[i].Name;
						if (meshName == String.Empty)
							meshName = meshes[i].Id.ToString();
						for (int j = 0; j < meshListSome.Count; j++)
						{
							odfSubmesh meshObj = meshListSome[j];
							odfMaterial mat = odf.FindMaterialInfo(meshObj.MaterialId, parser.MaterialSection);
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
							string meshObjName = meshObj.Name;
							if (meshObjName != String.Empty)
							{
								mqoName += meshObjName;
							}
							writer.WriteLine("Object \"" + mqoName + "\" {");
							writer.WriteLine("\tshading 1");
							writer.WriteLine("\tcolor " + color[0].ToFloatString() + " " + color[1].ToFloatString() + " " + color[2].ToFloatString());
							writer.WriteLine("\tcolor_type 1");

							List<ImportedVertex> vertList = odf.ImportedVertexListOld(meshObj.VertexList);
							List<ImportedFace> faceList = odf.ImportedFaceListOld(meshObj.FaceList);
							if (worldCoords)
							{
								for (int k = 0; k < vertList.Count; k++)
								{
									Vector4 v4 = Vector3.Transform(vertList[k].Position, transform);
									vertList[k].Position = new Vector3(v4.X, v4.Y, v4.Z);
								}
							}

							ExporterCommon.WriteMeshObject(writer, vertList, faceList, mqoMatIdx, null);
							writer.WriteLine("}");
						}
					}
					writer.WriteLine("Eof");
				}

				return usedTextures;
			}
		}

		public class ExporterMorph
		{
			private odfParser parser = null;
			private odfMorphObject morphObj = null;
			private bool skipUnusedProfiles = false;

			private bool[] colorVertex = null;

			private List<List<ImportedVertex>> vertLists;
			private List<ImportedFace> faceList;
			private List<odfTexture> usedTextures;

			public static void Export(string dirPath, odfParser parser, odfMorphObject morphObj, bool skipUnusedProfiles)
			{
				DirectoryInfo dir = new DirectoryInfo(dirPath);
				ExporterMorph exporter = new ExporterMorph(dir, parser, morphObj, skipUnusedProfiles);
				exporter.Export(dir);
			}

			private ExporterMorph(DirectoryInfo dir, odfParser parser, odfMorphObject morphObj, bool skipUnusedProfiles)
			{
				this.parser = parser;
				this.morphObj = morphObj;
				this.skipUnusedProfiles = skipUnusedProfiles;
			}

			private void Export(DirectoryInfo dir)
			{
				try
				{
					odfMorphSection morphSection = parser.MorphSection;
					ushort[] meshIndices = morphObj.MeshIndices;

					odfSubmesh meshObjBase = odf.FindMeshObject(morphObj.SubmeshId, parser.MeshSection);
					colorVertex = new bool[meshObjBase.VertexList.Count];
					for (int i = 0; i < meshIndices.Length; i++)
					{
						colorVertex[meshIndices[i]] = true;
					}

					vertLists = new List<List<ImportedVertex>>(morphObj.Count);
					ImportedVertex[] vertArr = new ImportedVertex[meshObjBase.VertexList.Count];
					for (int i = 0; i < morphObj.Count; i++)
					{
						if (skipUnusedProfiles)
						{
							bool skip = true;
							for (int j = 0; j < morphObj.SelectorList.Count; j++)
							{
								if (morphObj.SelectorList[j].ProfileIndex == i)
								{
									skip = false;
									break;
								}
							}
							if (skip)
								continue;
						}

						List<ImportedVertex> vertList = odf.ImportedVertexListOld(meshObjBase.VertexList);
						vertLists.Add(vertList);

						for (int j = 0; j < meshIndices.Length; j++)
						{
							ImportedVertex vert = vertList[meshIndices[j]];
							vert.Position = morphObj[i].VertexList[j].Position;
						}
					}

					faceList = odf.ImportedFaceListOld(meshObjBase.FaceList);
					string dest = Utility.GetDestFile(dir, meshObjBase.Parent.Name + "-" + morphObj.Name + "-", ".morph.mqo");
					odfMaterial mat = odf.FindMaterialInfo(meshObjBase.MaterialId, parser.MaterialSection);
					Export(dest, mat, odf.FindTextureInfo(meshObjBase.TextureIds[0], parser.TextureSection));
					foreach (odfTexture tex in usedTextures)
					{
						String texFilePath = Path.GetDirectoryName(parser.ODFPath) + @"\" + tex.TextureFile;
						odfTextureFile odfTex = new odfTextureFile(tex.Name, texFilePath);
						odf.ExportTexture(odfTex, dir.FullName + @"\" + tex.TextureFile);
					}
					Report.ReportLog("Finished exporting morph to " + dest);
				}
				catch (Exception ex)
				{
					Report.ReportLog("Error exporting morph: " + ex.Message);
				}
			}

			private void Export(string dest, odfMaterial mat, odfTexture tex)
			{
				DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(dest));
				if (!dir.Exists)
				{
					dir.Create();
				}

				usedTextures = new List<odfTexture>(parser.TextureSection.Count);
				using (StreamWriter writer = new StreamWriter(dest, false))
				{
					writer.WriteLine("Metasequoia Document");
					writer.WriteLine("Format Text Ver 1.0");
					writer.WriteLine();

					if (mat != null)
					{
						writer.WriteLine("Material 1 {");
						string s = "\t\"" + mat.Name + "\" vcol(1) col(0.800 0.800 0.800 1.000) dif(0.500) amb(0.100) emi(0.500) spc(0.100) power(30.00)";
						if (tex != null)
						{
							s += " tex(\"" + tex.TextureFile + "\")";
							usedTextures.Add(tex);
						}
						writer.WriteLine(s);
						writer.WriteLine("}");
					}

					Random rand = new Random();
					for (int i = 0; i < vertLists.Count; i++)
					{
						float[] color = new float[3];
						for (int k = 0; k < color.Length; k++)
						{
							color[k] = (float)((rand.NextDouble() / 2) + 0.5);
						}

						writer.WriteLine("Object \"" + morphObj[i].Name + "\" {");
						writer.WriteLine("\tvisible 0");
						writer.WriteLine("\tshading 1");
						writer.WriteLine("\tcolor " + color[0].ToFloatString() + " " + color[1].ToFloatString() + " " + color[2].ToFloatString());
						writer.WriteLine("\tcolor_type 1");
						ExporterCommon.WriteMeshObject(writer, vertLists[i], faceList, mat != null ? 0 : -1, colorVertex);
						writer.WriteLine("}");
					}

					writer.WriteLine("Eof");
				}
			}
		}
	}
}
