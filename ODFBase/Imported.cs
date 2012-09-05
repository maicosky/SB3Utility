using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SlimDX;

using SB3Utility;

namespace ODFPluginOld
{
	public interface IImported
	{
		List<ImportedFrame> FrameList { get; }
		List<ImportedMesh> MeshList { get; }
		List<ImportedMaterial> MaterialList { get; }
		List<ImportedTexture> TextureList { get; }
		List<ImportedAnimation> AnimationList { get; }
		List<ImportedMorph> MorphList { get; }
	}

	public class ImportedFrame : ObjChildren<ImportedFrame>, IObjChild
	{
		public string Name { get; set; }
//		public string Id { get; set; }
		public Matrix Matrix { get; set; }

		public dynamic Parent { get; set; }
	}

	public class ImportedMesh
	{
		public string Name { get; set; }
//		public string Id { get; set; }
		public string MeshFrameName { get; set; }
		public string MeshFrameId { get; set; }
		public List<ImportedSubmesh> SubmeshList { get; set; }
	}

	public class ImportedSubmesh
	{
		public string Name { get; set; }
//		public string Id { get; set; }
		public List<ImportedVertex> VertexList { get; set; }
		public List<ImportedFace> FaceList { get; set; }
		public List<ImportedBone> BoneList { get; set; }
		public string Material { get; set; }
		public string[] Textures { get; set; }
		public int Index { get; set; }
		public bool WorldCoords { get; set; }
	}

	public class ImportedVertex
	{
		public Vector3 Position { get; set; }
		public Vector3 Normal { get; set; }
		public Vector2 UV { get; set; }
	}

	public class ImportedFace
	{
		public ushort[] VertexIndices { get; set; }
	}

	public class ImportedBone
	{
		public string FrameName { get; set; }
//		public string FrameId { get; set; }
		public int NumberVertices { get { return VertexIndexArray.Length; } }
		public int[] VertexIndexArray { get; set; }
		public float[] WeightArray { get; set; }
		public Matrix Matrix { get; set; }
	}

	public class ImportedMaterial
	{
		public string Name { get; set; }
//		public string Id { get; set; }
		public Color4 Diffuse { get; set; }
		public Color4 Ambient { get; set; }
		public Color4 Specular { get; set; }
		public Color4 Emissive { get; set; }
		public float Power { get; set; }
	}

	public class ImportedTexture
	{
		public string Name { get; set; }
//		public string Id { get; set; }
		public string TextureFile { get; set; }
		public byte[] Data { get; set; }

		public ImportedTexture()
		{
		}

		public ImportedTexture(string path)
		{
			Name = TextureFile = Path.GetFileName(path);
			ODFPlugin.odfTextureFile texFile = new ODFPlugin.odfTextureFile(Name, path);
			int fileSize = 0;
			using (BinaryReader reader = texFile.DecryptFile(ref fileSize))
			{
				Data = reader.ReadBytes(fileSize);
			}
		}
	}

	public class ImportedAnimation
	{
		public List<ImportedAnimationTrack> TrackList { get; set; }
	}

	public class ImportedAnimationTrack
	{
		public string Name { get; set; }
		public ImportedAnimationKeyframe[] Keyframes { get; set; }
	}

	public class ImportedAnimationKeyframe
	{
		public Vector3 Scaling { get; set; }
		public Quaternion Rotation { get; set; }
		public Vector3 Translation { get; set; }
	}

/*	public class ImportedMorph
	{
		public string Name { get; set; }  // mesh name
		public List<ImportedMorphKeyframe> KeyframeList { get; set; }
	}

	public class ImportedMorphKeyframe
	{
		public string Name { get; set; }  // shape name
		public List<ImportedVertex> VertexList { get; set; }
	}*/
}
