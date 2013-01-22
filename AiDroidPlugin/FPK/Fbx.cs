using System;
using System.Collections.Generic;
using SlimDX;

using SB3Utility;

namespace AiDroidPlugin
{
	public static partial class Plugins
	{
		[Plugin]
		public static void ExportFbx([DefaultVar]remParser parser, object[] meshNames, object[] reaParsers, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, double filterPrecision, string path, string exportFormat, bool allFrames, bool skins)
		{
			List<reaParser> reaParserList = null;
			if (reaParsers != null)
			{
				reaParserList = new List<reaParser>(Utility.Convert<reaParser>(reaParsers));
			}

			List<string> meshStrList = new List<string>(Utility.Convert<string>(meshNames));
			List<remMesh> meshes = new List<remMesh>(meshStrList.Count);
			foreach (string meshName in meshStrList)
			{
				remMesh mesh = rem.FindMesh(new remId(meshName), parser.MESC);
				if (mesh != null)
				{
					meshes.Add(mesh);
				}
				else
					Report.ReportLog("Mesh " + meshName + " not found.");
			}

			REMConverter imp = new REMConverter(parser, meshes);
			if (reaParserList != null)
			{
				foreach (reaParser reaParser in reaParserList)
				{
					imp.ConvertAnimation(reaParser.ANIC, parser);
				}
			}

			FbxUtility.Export(path, imp, startKeyframe, endKeyframe, linear, EulerFilter, (float)filterPrecision, exportFormat, allFrames, skins);
		}

		public class REMConverter : IImported
		{
			public List<ImportedFrame> FrameList { get; protected set; }
			public List<ImportedMesh> MeshList { get; protected set; }
			public List<ImportedMaterial> MaterialList { get; protected set; }
			public List<ImportedTexture> TextureList { get; protected set; }
			public List<ImportedAnimation> AnimationList { get; protected set; }
			public List<ImportedMorph> MorphList { get; protected set; }

			public REMConverter(remParser parser, List<remMesh> meshes)
			{
				FrameList = new List<ImportedFrame>();
				ConvertFrames(parser.BONC.rootFrame);
				foreach (ImportedFrame frame in FrameList[0])
				{
					if (rem.FindMesh(new remId(frame.Name), parser.MESC) != null)
					{
						frame.Matrix = Matrix.Identity;
					}
					else
					{
						frame.Matrix *= Matrix.Scaling(1f, 1f, -1f);
					}
				}
				ConvertMeshes(meshes, parser);
				AnimationList = new List<ImportedAnimation>();
			}

			private ImportedFrame ConvertFrames(remBone frame)
			{
				ImportedFrame iFrame = new ImportedFrame();
				iFrame.InitChildren(frame.Count);
				iFrame.Name = frame.name;
				iFrame.Matrix = frame.matrix;

				FrameList.Add(iFrame);

				foreach (remBone child in frame)
				{
					ImportedFrame iChild = ConvertFrames(child);
					iFrame.AddChild(iChild);
				}

				return iFrame;
			}

			private void ConvertMeshes(List<remMesh> meshes, remParser parser)
			{
				MeshList = new List<ImportedMesh>(meshes.Count);
				MaterialList = new List<ImportedMaterial>(meshes.Count);
				TextureList = new List<ImportedTexture>(parser.MATC.Count);
				foreach (remMesh mesh in meshes)
				{
					ImportedMesh iMesh = new ImportedMesh();
					MeshList.Add(iMesh);
					iMesh.BoneList = new List<ImportedBone>();
					Dictionary<remId, byte> boneDic = new Dictionary<remId, byte>();
					remSkin skin = rem.FindSkin(mesh.name, parser.SKIC);
					rem.Mesh convertedMesh = new rem.Mesh(mesh, skin);
					iMesh.SubmeshList = new List<ImportedSubmesh>(convertedMesh.Count);
					remBone meshFrame = rem.FindFrame(mesh.frame, parser.BONC.rootFrame);
					ImportedFrame iFrame = ImportedHelpers.FindFrame(mesh.frame, FrameList[0]);
					float s = (float)Math.Round(Math.Abs(meshFrame.matrix.M11), 5);
					iFrame.Name = iMesh.Name = mesh.name + (s != 1f ? "(Scale=" + s.ToString() + ")" : String.Empty);
					foreach (rem.Submesh submesh in convertedMesh)
					{
						ImportedSubmesh iSubmesh = new ImportedSubmesh();
						iMesh.SubmeshList.Add(iSubmesh);
						remMaterial mat = rem.FindMaterial(submesh.MaterialName, parser.MATC);
						if (mat != null)
						{
							iSubmesh.Material = mat.name;
							ImportedMaterial iMat = ImportedHelpers.FindMaterial(iSubmesh.Material, MaterialList);
							if (iMat == null)
							{
								iMat = new ImportedMaterial();
								MaterialList.Add(iMat);
								iMat.Name = iSubmesh.Material;
								iMat.Diffuse = new Color4(mat.diffuse);
								iMat.Ambient = new Color4(mat.ambient);
								iMat.Specular = new Color4(mat.specular);
								iMat.Emissive = new Color4(mat.emissive);
								iMat.Power = mat.specularPower;

								iMat.Textures = new string[4] { String.Empty, String.Empty, String.Empty, String.Empty };
								if (mat.texture != null)
								{
									iMat.Textures[0] = mat.texture;
									if (ImportedHelpers.FindTexture(iMat.Textures[0], TextureList) == null)
									{
										try
										{
											ImportedTexture iTex = rem.ImportedTexture(mat.texture, parser.RemPath, true);
											TextureList.Add(iTex);
										}
										catch
										{
											Report.ReportLog("cant read texture " + iMat.Textures[0]);
										}
									}
								}
							}
						}

						List<Tuple<byte, float>>[] iSkin = new List<Tuple<byte, float>>[submesh.numVertices];
						for (int i = 0; i < submesh.numVertices; i++)
						{
							iSkin[i] = new List<Tuple<byte, float>>(4);
						}
						List<remBoneWeights> boneList = submesh.BoneList;
						if (boneList != null)
						{
							if (iMesh.BoneList.Capacity < boneList.Count)
							{
								iMesh.BoneList.Capacity += boneList.Count;
							}
							foreach (remBoneWeights boneWeights in boneList)
							{
								byte idx;
								if (!boneDic.TryGetValue(boneWeights.bone, out idx))
								{
									ImportedBone iBone = new ImportedBone();
									iMesh.BoneList.Add(iBone);
									iBone.Name = boneWeights.bone;
									Vector3 scale, translate;
									Quaternion rotate;
									meshFrame.matrix.Decompose(out scale, out rotate, out translate);
									scale.X = Math.Abs(scale.X);
									scale.Y = Math.Abs(scale.Y);
									scale.Z = Math.Abs(scale.Z);
									iBone.Matrix = Matrix.Scaling(1f, 1f, -1f) * Matrix.Invert(meshFrame.matrix) * Matrix.Scaling(scale) * boneWeights.matrix;
									boneDic.Add(boneWeights.bone, idx = (byte)boneDic.Count);
								}
								for (int i = 0; i < boneWeights.numVertIdxWts; i++)
								{
									iSkin[boneWeights.vertexIndices[i]].Add(new Tuple<byte, float>(idx, boneWeights.vertexWeights[i]));
								}
							}
						}

						iSubmesh.VertexList = new List<ImportedVertex>(submesh.numVertices);
						for (int i = 0; i < submesh.numVertices; i++)
						{
							remVertex vert = submesh.VertexList[i];
							ImportedVertex iVert = new ImportedVertex();
							iSubmesh.VertexList.Add(iVert);
							iVert.Position = new Vector3(vert.Position.X, vert.Position.Z, -vert.Position.Y);
							iVert.Normal = new Vector3(vert.Normal.X, vert.Normal.Z, -vert.Normal.Y);
							iVert.UV = new float[] { vert.UV[0], vert.UV[1] };
							iVert.BoneIndices = new byte[4];
							iVert.Weights = new float[4];
							for (int j = 0; j < 4; j++)
							{
								if (j < iSkin[i].Count)
								{
									Tuple<byte, float> vertIdxWeight = iSkin[i][j];
									iVert.BoneIndices[j] = vertIdxWeight.Item1;
									iVert.Weights[j] = vertIdxWeight.Item2;
								}
								else
								{
									iVert.BoneIndices[j] = 0xFF;
								}
							}
						}

						iSubmesh.FaceList = rem.ImportedFaceList(submesh.FaceList);
					}
				}
			}

			public void ConvertAnimation(reaANICsection animSection, remParser parser)
			{
				ImportedSampledAnimation anim = new ImportedSampledAnimation();
				anim.TrackList = new List<ImportedAnimationSampledTrack>(animSection.Count);
				foreach (reaAnimationTrack track in animSection)
				{
					ImportedAnimationSampledTrack iTrack = new ImportedAnimationSampledTrack();
					iTrack.Name = track.boneFrame;
					remBone boneFrame = rem.FindFrame(track.boneFrame, parser.BONC.rootFrame);
					bool isTopFrame = boneFrame != null && boneFrame.Parent == parser.BONC.rootFrame;
					int animLen = track.scalings[track.scalings.Length - 1].index + 1;
					iTrack.Scalings = new Vector3?[animLen];
					if (isTopFrame)
					{
						for (int i = 0; i < track.scalings.Length; i++)
						{
							Vector3 scale = new Vector3(track.scalings[i].value.X, track.scalings[i].value.Z, -track.scalings[i].value.Y);
							iTrack.Scalings[track.scalings[i].index] = scale;
						}
					}
					else
					{
						for (int i = 0; i < track.scalings.Length; i++)
						{
							Vector3 scale = new Vector3(track.scalings[i].value.X, track.scalings[i].value.Z, track.scalings[i].value.Y);
							iTrack.Scalings[track.scalings[i].index] = scale;
						}
					}
					animLen = track.rotations[track.rotations.Length - 1].index + 1;
					iTrack.Rotations = new Quaternion?[animLen];
					for (int i = 0; i < track.rotations.Length; i++)
					{
						iTrack.Rotations[track.rotations[i].index] = Quaternion.Invert(track.rotations[i].value);
					}
					animLen = track.translations[track.translations.Length - 1].index + 1;
					iTrack.Translations = new Vector3?[animLen];
					for (int i = 0; i < track.translations.Length; i++)
					{
						Vector3 translation = new Vector3(track.translations[i].value.X, track.translations[i].value.Y, track.translations[i].value.Z);
						iTrack.Translations[track.translations[i].index] = translation;
					}
					anim.TrackList.Add(iTrack);
				}
				AnimationList.Add(anim);
			}
		}
	}
}
