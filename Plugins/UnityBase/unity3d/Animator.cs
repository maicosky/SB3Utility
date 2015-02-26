using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

using SB3Utility;

namespace UnityPlugin
{
	public class Animator : Component, LinkedByGameObject, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public PPtr<GameObject> m_GameObject { get; set; }
		public byte m_Enabled { get; set; }
		public PPtr<Avatar> m_Avatar { get; set; }
		public PPtr<RuntimeAnimatorController> m_Controller { get; set; }
		public int m_CullingMode { get; set; }
		public int m_UpdateMode { get; set; }
		public int m_ApplyRootMotion { get; set; } // is listed as bool
		public bool m_HasTransformHierarchy { get; set; }
		public bool m_AllowConstantClipSamplingOptimization { get; set; }

		public Transform RootTransform { get; set; }

		public static string ArgToHashExecutable { get; set; }
		public static string ArgToHashArgs { get; set; }

		public Animator(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_GameObject = new PPtr<GameObject>(stream, file);
			m_Enabled = reader.ReadByte();
			reader.ReadBytes(3);
			m_Avatar = new PPtr<Avatar>(stream, file);
			m_Controller = new PPtr<RuntimeAnimatorController>(stream);
			m_CullingMode = reader.ReadInt32();
			m_UpdateMode = reader.ReadInt32();
			m_ApplyRootMotion = reader.ReadInt32();
			m_HasTransformHierarchy = reader.ReadBoolean();
			m_AllowConstantClipSamplingOptimization = reader.ReadBoolean();

			if (m_GameObject.instance != null)
			{
				RootTransform = m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
			}
		}

		public static PPtr<GameObject> LoadGameObject(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			return new PPtr<GameObject>(stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			file.WritePPtr(m_GameObject.asset, false, stream);
			writer.Write(m_Enabled);
			writer.Write(new byte[3]);
			file.WritePPtr(m_Avatar.asset, false, stream);
			file.WritePPtr(m_Controller.asset, false, stream);
			writer.Write(m_CullingMode);
			writer.Write(m_UpdateMode);
			writer.Write(m_ApplyRootMotion);
			writer.Write(m_HasTransformHierarchy);
			writer.Write(m_AllowConstantClipSamplingOptimization);
		}

		public static uint StringToHash(string str)
		{
			Process p = Process.Start(ArgToHashExecutable, ArgToHashArgs + " \"" + str + "\"");
			if (p != null && p.WaitForExit(15000))
			{
				try
				{
					using (StreamReader reader = new StreamReader(Path.GetFileNameWithoutExtension(ArgToHashExecutable) + ".out", System.Text.ASCIIEncoding.ASCII))
					{
						string line = reader.ReadLine();
						string[] pair = line.Split(',');
						return uint.Parse(pair[0]);
					}
				}
				finally
				{
					File.Delete(Path.GetFileNameWithoutExtension(ArgToHashExecutable) + ".out");
				}
			}
			else
			{
				if (p != null)
				{
					p.Kill();
				}
				throw new Exception("Unable to start process - no hash for " + str);
			}
		}

		public static uint[] StringsToHashes(string s1, string s2)
		{
			string args = ArgToHashArgs + " \"" + s1 + "\" \"" + s2 + "\"";
			Process p = Process.Start(ArgToHashExecutable, args);
			if (p != null && p.WaitForExit(15000))
			{
				try
				{
					using (StreamReader reader = new StreamReader(Path.GetFileNameWithoutExtension(ArgToHashExecutable) + ".out", System.Text.ASCIIEncoding.ASCII))
					{
						string line = reader.ReadLine();
						string[] pair = line.Split(',');
						uint h1 = uint.Parse(pair[0]);
						line = reader.ReadLine();
						pair = line.Split(',');
						uint h2 = uint.Parse(pair[0]);
						return new uint[2] { h1, h2 };
					}
				}
				finally
				{
					File.Delete(Path.GetFileNameWithoutExtension(ArgToHashExecutable) + ".out");
				}
			}
			else
			{
				if (p != null)
				{
					p.Kill();
				}
				throw new Exception("Unable to start process - no hash for " + s1 + "," + s2);
			}
		}
	}
}
