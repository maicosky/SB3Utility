using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;

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

		public Transform RootTransform
		{
			get
			{
				if (m_GameObject.instance == null)
				{
					return null;
				}
				Transform rootTransform = m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
				while (rootTransform.Parent != null)
				{
					rootTransform = rootTransform.Parent;
				}
				return rootTransform;
			}
		}

		public static string ArgToHashExecutable { get; set; }
		public static string ArgToHashArgs { get; set; }
		private static Process ArgToHashProcess;

		public Animator(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public Animator(AssetCabinet file) :
			this(file, 0, UnityClassID.Animator, UnityClassID.Animator)
		{
			file.ReplaceSubfile(-1, this, null);
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
		}

		public static PPtr<GameObject> LoadGameObject(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			return new PPtr<GameObject>(stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			m_GameObject.WriteTo(stream);
			writer.Write(m_Enabled);
			writer.Write(new byte[3]);
			m_Avatar.WriteTo(stream);
			m_Controller.WriteTo(stream);
			writer.Write(m_CullingMode);
			writer.Write(m_UpdateMode);
			writer.Write(m_ApplyRootMotion);
			writer.Write(m_HasTransformHierarchy);
			writer.Write(m_AllowConstantClipSamplingOptimization);
		}

		public Animator Clone(AssetCabinet file)
		{
			Component gameObj = file.Bundle.FindComponent(m_GameObject.instance.m_Name, UnityClassID.GameObject);
			if (gameObj == null)
			{
				file.MergeTypeDefinition(this.file, UnityClassID.Animator);

				Animator dest = new Animator(file);
				AssetCabinet.IncompleteClones.Add(new Tuple<Component, Component>(this, dest));
				return dest;
			}
			else if (gameObj is NotLoaded)
			{
				NotLoaded notLoaded = (NotLoaded)gameObj;
				if (notLoaded.replacement != null)
				{
					gameObj = notLoaded.replacement;
				}
				else
				{
					gameObj = file.LoadComponent(file.SourceStream, notLoaded);
				}
			}
			return ((GameObject)gameObj).FindLinkedComponent(UnityClassID.Animator);
		}

		public void CopyTo(Animator dest)
		{
			if (file.Bundle.numContainerEntries(m_GameObject.instance.m_Name, UnityClassID.GameObject) > 1)
			{
				Report.ReportLog("Warning! Animator " + m_GameObject.instance.m_Name + " has multiple entries in the AssetBundle's Container.");
			}
			dest.file.Bundle.AddComponent(m_GameObject.instance.m_Name, dest.m_GameObject.instance);
			dest.m_Enabled = m_Enabled;
			dest.m_Avatar = new PPtr<Avatar>(m_Avatar.instance != null ? m_Avatar.instance.Clone(dest.file) : null);

			dest.m_Controller = new PPtr<RuntimeAnimatorController>((Component)null);
			if (m_Controller.asset != null)
			{
				Report.ReportLog("Warning! " + m_Controller.asset.classID1 + " " + AssetCabinet.ToString(m_Controller.asset) + " not duplicated!");
			}

			dest.m_CullingMode = m_CullingMode;
			dest.m_UpdateMode = m_UpdateMode;
			dest.m_ApplyRootMotion = m_ApplyRootMotion;
			dest.m_HasTransformHierarchy = m_HasTransformHierarchy;
			dest.m_AllowConstantClipSamplingOptimization = m_AllowConstantClipSamplingOptimization;
		}

		public static uint StringToHash(string str)
		{
			if (ArgToHashProcess == null)
			{
				LaunchArgToHash();
			}

			ArgToHashProcess.StandardInput.Write(str + "\n");
			string output = ReadLineBlocking(ArgToHashProcess.StandardOutput);
			string[] pair = output.Split(',');
			return (uint)int.Parse(pair[0]);
		}

		private static void LaunchArgToHash()
		{
			ArgToHashProcess = new Process();
			ArgToHashProcess.StartInfo = new ProcessStartInfo(ArgToHashExecutable, ArgToHashArgs);
			ArgToHashProcess.StartInfo.UseShellExecute = false;
			ArgToHashProcess.StartInfo.RedirectStandardInput = true;
			ArgToHashProcess.StartInfo.RedirectStandardOutput = true;
			ArgToHashProcess.Start();
			while (!ArgToHashProcess.StandardOutput.EndOfStream)
			{
				string msg = ReadLineBlocking(ArgToHashProcess.StandardOutput);
				if (msg.Contains("ArgToHash started"))
				{
					break;
				}
			}
		}

		public static void TerminateArgToHash()
		{
			if (ArgToHashProcess != null)
			{
				ArgToHashProcess.StandardInput.Write("~QUIT~\n");
				ReadLineBlocking(ArgToHashProcess.StandardOutput);
				ArgToHashProcess.Close();
				ArgToHashProcess = null;
			}
		}

		private static string ReadLineBlocking(StreamReader reader)
		{
			StringBuilder sb = new StringBuilder(1000);
			char[] buffer = new char[1];
			int charsRead;
			do
			{
				charsRead = reader.ReadBlock(buffer, 0, 1);
				if (charsRead < 1)
				{
					break;
				}
				if (buffer[0] >= ' ')
				{
					sb.Append(buffer);
				}
			} while (buffer[0] != '\x0A');
			return sb.ToString();
		}
	}
}
