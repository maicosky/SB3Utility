using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public class SkeletonMaskElement : IObjInfo
	{
		public uint m_PathHash { get; set; }
		public float m_Weight { get; set; }

		public SkeletonMaskElement(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_PathHash = reader.ReadUInt32();
			m_Weight = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_PathHash);
			writer.Write(m_Weight);
		}
	}

	public class SkeletonMask : IObjInfo
	{
		public SkeletonMaskElement[] m_Data { get; set; }

		public SkeletonMask(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numElements = reader.ReadInt32();
			m_Data = new SkeletonMaskElement[numElements];
			for (int i = 0; i < numElements; i++)
			{
				m_Data[i] = new SkeletonMaskElement(stream);
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_Data.Length);
			for (int i = 0; i < m_Data.Length; i++)
			{
				m_Data[i].WriteTo(stream);
			}
		}
	}

	public class HumanPoseMask : IObjInfo
	{
		public uint word0 { get; set; }
		public uint word1 { get; set; }

		public HumanPoseMask(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			word0 = reader.ReadUInt32();
			word1 = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(word0);
			writer.Write(word1);
		}
	}

	public class LayerConstant : IObjInfo
	{
		public uint m_StateMachineIndex { get; set; }
		public uint m_StateMachineMotionSetIndex { get; set; }
		public HumanPoseMask m_BodyMask { get; set; }
		public SkeletonMask m_SkeletonMask { get; set; }
		public uint m_Binding { get; set; }
		public int m_LayerBlendingMode { get; set; }
		public float m_DefaultWeight { get; set; }
		public bool m_IKPass { get; set; }
		public bool m_SyncedLayerAffectsTiming { get; set; }

		public LayerConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_StateMachineIndex = reader.ReadUInt32();
			m_StateMachineMotionSetIndex = reader.ReadUInt32();
			m_BodyMask = new HumanPoseMask(stream);
			m_SkeletonMask = new SkeletonMask(stream);
			m_Binding = reader.ReadUInt32();
			m_LayerBlendingMode = reader.ReadInt32();
			m_DefaultWeight = reader.ReadSingle();
			m_IKPass = reader.ReadBoolean();
			m_SyncedLayerAffectsTiming = reader.ReadBoolean();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_StateMachineIndex);
			writer.Write(m_StateMachineMotionSetIndex);
			m_BodyMask.WriteTo(stream);
			m_SkeletonMask.WriteTo(stream);
			writer.Write(m_Binding);
			writer.Write(m_LayerBlendingMode);
			writer.Write(m_DefaultWeight);
			writer.Write(m_IKPass);
			writer.Write(m_SyncedLayerAffectsTiming);
			writer.Write(new byte[2]);
		}
	}

	public class ConditionConstant : IObjInfo
	{
		public uint m_ConditionMode { get; set; }
		public uint m_EventID { get; set; }
		public float m_EventThreshold { get; set; }
		public float m_ExitTime { get; set; }

		public ConditionConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_ConditionMode = reader.ReadUInt32();
			m_EventID = reader.ReadUInt32();
			m_EventThreshold = reader.ReadSingle();
			m_ExitTime = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_ConditionMode);
			writer.Write(m_EventID);
			writer.Write(m_EventThreshold);
			writer.Write(m_ExitTime);
		}
	}

	public class TransitionConstant : IObjInfo
	{
		public ConditionConstant[] m_ConditionConstantArray { get; set; }
		public uint m_DestinationState { get; set; }
		public uint m_ID { get; set; }
		public uint m_UserID { get; set; }
		public float m_TransitionDuration { get; set; }
		public float m_TransitionOffset { get; set; }
		public bool m_Atomic { get; set; }
		public bool m_CanTransitionToSelf { get; set; }

		public TransitionConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numConditions = reader.ReadInt32();
			m_ConditionConstantArray = new ConditionConstant[numConditions];
			for (int i = 0; i < numConditions; i++)
			{
				m_ConditionConstantArray[i] = new ConditionConstant(stream);
			}

			m_DestinationState = reader.ReadUInt32();
			m_ID = reader.ReadUInt32();
			m_UserID = reader.ReadUInt32();
			m_TransitionDuration = reader.ReadSingle();
			m_TransitionOffset = reader.ReadSingle();
			m_Atomic = reader.ReadBoolean();
			m_CanTransitionToSelf = reader.ReadBoolean();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_ConditionConstantArray.Length);
			for (int i = 0; i < m_ConditionConstantArray.Length; i++)
			{
				m_ConditionConstantArray[i].WriteTo(stream);
			}

			writer.Write(m_DestinationState);
			writer.Write(m_ID);
			writer.Write(m_UserID);
			writer.Write(m_TransitionDuration);
			writer.Write(m_TransitionOffset);
			writer.Write(m_Atomic);
			writer.Write(m_CanTransitionToSelf);
			writer.Write(new byte[2]);
		}
	}

	public class LeafInfoConstant : IObjInfo
	{
		public uint[] m_IDArray { get; set; }
		public uint m_IndexOffset { get; set; }

		public LeafInfoConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_IDArray = reader.ReadUInt32Array(reader.ReadInt32());
			m_IndexOffset = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_IDArray.Length);
			writer.Write(m_IDArray);

			writer.Write(m_IndexOffset);
		}
	}

	public class Blend1dDataConstant : IObjInfo // wrong labeled
	{
		public float[] m_ChildThresholdArray { get; set; }

		public Blend1dDataConstant(Stream stream)
		{
			LoadFrom(stream);}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_ChildThresholdArray = reader.ReadSingleArray(reader.ReadInt32());
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_ChildThresholdArray.Length);
			writer.Write(m_ChildThresholdArray);
		}
	}

	public class MotionNeighborList : IObjInfo
	{
		public uint[] m_NeighborArray { get; set; }

		public MotionNeighborList(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_NeighborArray = reader.ReadUInt32Array(reader.ReadInt32());
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_NeighborArray.Length);
			writer.Write(m_NeighborArray);
		}
	}

	public class Blend2dDataConstant : IObjInfo
	{
		public Vector2[] m_ChildPositionArray { get; set; }
		public float[] m_ChildMagnitudeArray { get; set; }
		public Vector2[] m_ChildPairVectorArray { get; set; }
		public float[] m_ChildPairAvgMagInvArray { get; set; }
		public MotionNeighborList[] m_ChildNeighborListArray { get; set; }

		public Blend2dDataConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_ChildPositionArray = reader.ReadVector2Array(reader.ReadInt32());
			m_ChildMagnitudeArray = reader.ReadSingleArray(reader.ReadInt32());
			m_ChildPairVectorArray = reader.ReadVector2Array(reader.ReadInt32());
			m_ChildPairAvgMagInvArray = reader.ReadSingleArray(reader.ReadInt32());

			int numNeighbours = reader.ReadInt32();
			m_ChildNeighborListArray = new MotionNeighborList[numNeighbours];
			for (int i = 0; i < numNeighbours; i++)
			{
				m_ChildNeighborListArray[i] = new MotionNeighborList(stream);
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_ChildPositionArray.Length);
			writer.Write(m_ChildPositionArray);

			writer.Write(m_ChildMagnitudeArray.Length);
			writer.Write(m_ChildMagnitudeArray);

			writer.Write(m_ChildPairVectorArray.Length);
			writer.Write(m_ChildPairVectorArray);

			writer.Write(m_ChildPairAvgMagInvArray.Length);
			writer.Write(m_ChildPairAvgMagInvArray);

			writer.Write(m_ChildNeighborListArray.Length);
			for (int i = 0; i < m_ChildNeighborListArray.Length; i++)
			{
				m_ChildNeighborListArray[i].WriteTo(stream);
			}
		}
	}

	public class BlendTreeNodeConstant : IObjInfo
	{
		public uint m_BlendType { get; set; }
		public uint m_BlendEventID { get; set; }
		public uint m_BlendEventYID { get; set; }
		public uint[] m_ChildIndices { get; set; }
		public Blend1dDataConstant m_Blend1dData { get; set; }
		public Blend2dDataConstant m_Blend2dData { get; set; }
		public uint m_ClipID { get; set; }
		public uint m_ClipIndex { get; set; }
		public float m_Duration { get; set; }
		public float m_CycleOffset { get; set; }
		public bool m_Mirror { get; set; }

		public BlendTreeNodeConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_BlendType = reader.ReadUInt32();
			m_BlendEventID = reader.ReadUInt32();
			m_BlendEventYID = reader.ReadUInt32();
			m_ChildIndices = reader.ReadUInt32Array(reader.ReadInt32());
			m_Blend1dData = new Blend1dDataConstant(stream);
			m_Blend2dData = new Blend2dDataConstant(stream);
			m_ClipID = reader.ReadUInt32();
			m_ClipIndex = reader.ReadUInt32();
			m_Duration = reader.ReadSingle();
			m_CycleOffset = reader.ReadSingle();
			m_Mirror = reader.ReadBoolean();
			reader.ReadBytes(3);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_BlendType);
			writer.Write(m_BlendEventID);
			writer.Write(m_BlendEventYID);

			writer.Write(m_ChildIndices.Length);
			writer.Write(m_ChildIndices);

			m_Blend1dData.WriteTo(stream);
			m_Blend2dData.WriteTo(stream);
			writer.Write(m_ClipID);
			writer.Write(m_ClipIndex);
			writer.Write(m_Duration);
			writer.Write(m_CycleOffset);
			writer.Write(m_Mirror);
			writer.Write(new byte[3]);
		}
	}

	public class BlendTreeConstant : IObjInfo
	{
		public BlendTreeNodeConstant[] m_NodeArray { get; set; }

		public BlendTreeConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numBlends = reader.ReadInt32();
			m_NodeArray = new BlendTreeNodeConstant[numBlends];
			for (int i = 0; i < numBlends; i++)
			{
				m_NodeArray[i] = new BlendTreeNodeConstant(stream);
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_NodeArray.Length);
			for (int i = 0; i < m_NodeArray.Length; i++)
			{
				m_NodeArray[i].WriteTo(stream);
			}
		}
	}

	public class StateConstant : IObjInfo
	{
		public TransitionConstant[] m_TransitionConstantArray { get; set; }
		public int[] m_BlendTreeConstantIndexArray { get; set; }
		public LeafInfoConstant[] m_LeafInfoArray { get; set; }
		public BlendTreeConstant[] m_BlendTreeConstantArray { get; set; }
		public uint m_NameID { get; set; }
		public uint m_PathID { get; set; }
		public uint m_TagID { get; set; }
		public float m_Speed { get; set; }
		public float m_CycleOffset { get; set; }
		public bool m_IKOnFeet { get; set; }
		public bool m_Loop { get; set; }
		public bool m_Mirror { get; set; }

		public StateConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numTransistions = reader.ReadInt32();
			m_TransitionConstantArray = new TransitionConstant[numTransistions];
			for (int i = 0; i < numTransistions; i++)
			{
				m_TransitionConstantArray[i] = new TransitionConstant(stream);
			}

			int numBlendIndices = reader.ReadInt32();
			m_BlendTreeConstantIndexArray = new int[numBlendIndices];
			for (int i = 0; i < numBlendIndices; i++)
			{
				m_BlendTreeConstantIndexArray[i] = reader.ReadInt32();
			}

			int numInfos = reader.ReadInt32();
			m_LeafInfoArray = new LeafInfoConstant[numInfos];
			for (int i = 0; i < numInfos; i++)
			{
				m_LeafInfoArray[i] = new LeafInfoConstant(stream);
			}

			int numBlends = reader.ReadInt32();
			m_BlendTreeConstantArray = new BlendTreeConstant[numBlends];
			for (int i = 0; i < numBlends; i++)
			{
				m_BlendTreeConstantArray[i] = new BlendTreeConstant(stream);
			}

			m_NameID = reader.ReadUInt32();
			m_PathID = reader.ReadUInt32();
			m_TagID = reader.ReadUInt32();
			m_Speed = reader.ReadSingle();
			m_CycleOffset = reader.ReadSingle();
			m_IKOnFeet = reader.ReadBoolean();
			m_Loop = reader.ReadBoolean();
			m_Mirror = reader.ReadBoolean();
			reader.ReadByte();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_TransitionConstantArray.Length);
			for (int i = 0; i < m_TransitionConstantArray.Length; i++)
			{
				m_TransitionConstantArray[i].WriteTo(stream);
			}

			writer.Write(m_BlendTreeConstantIndexArray.Length);
			writer.Write(m_BlendTreeConstantIndexArray);

			writer.Write(m_LeafInfoArray.Length);
			for (int i = 0; i < m_LeafInfoArray.Length; i++)
			{
				m_LeafInfoArray[i].WriteTo(stream);
			}

			writer.Write(m_BlendTreeConstantArray.Length);
			for (int i = 0; i < m_BlendTreeConstantArray.Length; i++)
			{
				m_BlendTreeConstantArray[i].WriteTo(stream);
			}

			writer.Write(m_NameID);
			writer.Write(m_PathID);
			writer.Write(m_TagID);
			writer.Write(m_Speed);
			writer.Write(m_CycleOffset);
			writer.Write(m_IKOnFeet);
			writer.Write(m_Loop);
			writer.Write(m_Mirror);
			writer.Write((byte)0);
		}
	}

	public class StateMachineConstant : IObjInfo
	{
		public StateConstant[] m_StateConstantArray { get; set; }
		public TransitionConstant[] m_AnyStateTransitionConstantArray { get; set; }
		public uint m_DefaultState { get; set; }
		public uint m_MotionSetCount { get; set; }

		public StateMachineConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numStates = reader.ReadInt32();
			m_StateConstantArray = new StateConstant[numStates];
			for (int i = 0; i < numStates; i++)
			{
				m_StateConstantArray[i] = new StateConstant(stream);
			}

			int numAnyStates = reader.ReadInt32();
			m_AnyStateTransitionConstantArray = new TransitionConstant[numAnyStates];
			for (int i = 0; i < numAnyStates; i++)
			{
				m_AnyStateTransitionConstantArray[i] = new TransitionConstant(stream);
			}

			m_DefaultState = reader.ReadUInt32();
			m_MotionSetCount = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_StateConstantArray.Length);
			for (int i = 0; i < m_StateConstantArray.Length; i++)
			{
				m_StateConstantArray[i].WriteTo(stream);
			}

			writer.Write(m_AnyStateTransitionConstantArray.Length);
			for (int i = 0; i < m_AnyStateTransitionConstantArray.Length; i++)
			{
				m_AnyStateTransitionConstantArray[i].WriteTo(stream);
			}

			writer.Write(m_DefaultState);
			writer.Write(m_MotionSetCount);
		}
	}

	public class ValueArray : IObjInfo
	{
		public bool[] m_BoolValues { get; set; }
		public int[] m_IntValues { get; set; }
		public float[] m_FloatValues { get; set; }
		public Vector4[] m_PositionValues { get; set; }
		public Vector4[] m_QuaternionValues { get; set; }
		public Vector4[] m_ScaleValues { get; set; }

		public ValueArray(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numBools = reader.ReadInt32();
			m_BoolValues = new bool[numBools];
			for (int i = 0; i < numBools; i++)
			{
				m_BoolValues[i] = reader.ReadBoolean();
			}
			if ((numBools & 3) > 0)
			{
				reader.ReadBytes(4 - (numBools & 3));
			}

			m_IntValues = reader.ReadInt32Array(reader.ReadInt32());
			m_FloatValues = reader.ReadSingleArray(reader.ReadInt32());
			m_PositionValues = reader.ReadVector4Array(reader.ReadInt32());
			m_QuaternionValues = reader.ReadVector4Array(reader.ReadInt32());
			m_ScaleValues = reader.ReadVector4Array(reader.ReadInt32());
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_BoolValues.Length);
			for (int i = 0; i < m_BoolValues.Length; i++)
			{
				writer.Write(m_BoolValues[i]);
			}
			if ((m_BoolValues.Length & 3) > 0)
			{
				writer.Write(new byte[4 - (m_BoolValues.Length & 3)]);
			}

			writer.Write(m_IntValues.Length);
			writer.Write(m_IntValues);

			writer.Write(m_FloatValues.Length);
			writer.Write(m_FloatValues);

			writer.Write(m_PositionValues.Length);
			writer.Write(m_PositionValues);

			writer.Write(m_QuaternionValues.Length);
			writer.Write(m_QuaternionValues);

			writer.Write(m_ScaleValues.Length);
			writer.Write(m_ScaleValues);
		}
	}

	public class ControllerConstant : IObjInfo
	{
		public LayerConstant[] m_LayerArray { get; set; }
		public StateMachineConstant[] m_StateMachineArray { get; set; }
		public ValueArrayConstant m_Values { get; set; }
		public ValueArray m_DefaultValues { get; set; }

		public ControllerConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numLayers = reader.ReadInt32();
			m_LayerArray = new LayerConstant[numLayers];
			for (int i = 0; i < numLayers; i++)
			{
				m_LayerArray[i] = new LayerConstant(stream);
			}

			int numStates = reader.ReadInt32();
			m_StateMachineArray = new StateMachineConstant[numStates];
			for (int i = 0; i < numStates; i++)
			{
				m_StateMachineArray[i] = new StateMachineConstant(stream);
			}

			m_Values = new ValueArrayConstant(stream);
			m_DefaultValues = new ValueArray(stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_LayerArray.Length);
			for (int i = 0; i < m_LayerArray.Length; i++)
			{
				m_LayerArray[i].WriteTo(stream);
			}

			writer.Write(m_StateMachineArray.Length);
			for (int i = 0; i < m_StateMachineArray.Length; i++)
			{
				m_StateMachineArray[i].WriteTo(stream);
			}

			m_Values.WriteTo(stream);
			m_DefaultValues.WriteTo(stream);
		}
	}

	public class AnimatorController : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public uint m_ControllerSize { get; set; }
		public ControllerConstant m_Controller { get; set; }
		public List<KeyValuePair<uint, string>> m_TOS { get; set; }
		public List<PPtr<AnimationClip>> m_AnimationClips { get; set; }

		public AnimatorController(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public AnimatorController(AssetCabinet file) :
			this(file, 0, UnityClassID.AnimatorController, UnityClassID.AnimatorController)
		{
			file.ReplaceSubfile(-1, this, null);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();
			m_ControllerSize = reader.ReadUInt32();
			m_Controller = new ControllerConstant(stream);

			int tosSize = reader.ReadInt32();
			m_TOS = new List<KeyValuePair<uint, string>>(tosSize);
			for (int i = 0; i < tosSize; i++)
			{
				m_TOS.Add(new KeyValuePair<uint, string>(reader.ReadUInt32(), reader.ReadNameA4()));
			}

			int numClips = reader.ReadInt32();
			m_AnimationClips = new List<PPtr<AnimationClip>>(numClips);
			for (int i = 0; i < numClips; i++)
			{
				m_AnimationClips.Add(new PPtr<AnimationClip>(stream, file));
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			writer.Write(m_ControllerSize);
			m_Controller.WriteTo(stream);

			writer.Write(m_TOS.Count);
			for (int i = 0; i < m_TOS.Count; i++)
			{
				writer.Write(m_TOS[i].Key);
				writer.WriteNameA4(m_TOS[i].Value);
			}

			writer.Write(m_AnimationClips.Count);
			for (int i = 0; i < m_AnimationClips.Count; i++)
			{
				m_AnimationClips[i].WriteTo(stream);
			}
		}

		/*public AnimatorController Clone(AssetCabinet file)
		{
			AnimatorController animCtrl = new AnimatorController(file);

			return animCtrl;
		}*/
	}
}
