using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public class Keyframe<T>
	{
		public float time { get; set; }
		public T value { get; set; }
		public T inSlope { get; set; }
		public T outSlope { get; set; }

		public Keyframe(BinaryReader reader, Func<T> readerFunc)
		{
			LoadFrom(reader, readerFunc);
		}

		public void LoadFrom(BinaryReader reader, Func<T> readerFunc)
		{
			time = reader.ReadSingle();
			value = readerFunc();
			inSlope = readerFunc();
			outSlope = readerFunc();
		}

		public void WriteTo(BinaryWriter writer, Action<T> writerFunc)
		{
			writer.Write(time);
			writerFunc(value);
			writerFunc(inSlope);
			writerFunc(outSlope);
		}
	}

	public class AnimationCurve<T>
	{
		public List<Keyframe<T>> m_Curve { get; set; }
		public int m_PreInfinity { get; set; }
		public int m_PostInfinity { get; set; }

		public AnimationCurve(BinaryReader reader, Func<T> readerFunc)
		{
			LoadFrom(reader, readerFunc);
		}

		public void LoadFrom(BinaryReader reader, Func<T> readerFunc)
		{
			int numCurves = reader.ReadInt32();
			m_Curve = new List<Keyframe<T>>(numCurves);
			for (int i = 0; i < numCurves; i++)
			{
				m_Curve.Add(new Keyframe<T>(reader, readerFunc));
			}

			m_PreInfinity = reader.ReadInt32();
			m_PostInfinity = reader.ReadInt32();
		}

		public void WriteTo(BinaryWriter writer, Action<T> writerFunc)
		{
			writer.Write(m_Curve.Count);
			for (int i = 0; i < m_Curve.Count; i++)
			{
				m_Curve[i].WriteTo(writer, writerFunc);
			}

			writer.Write(m_PreInfinity);
			writer.Write(m_PostInfinity);
		}
	}

	public class QuaternionCurve : IObjInfo
	{
		public AnimationCurve<Quaternion> curve { get; set; }
		public string path { get; set; }

		public QuaternionCurve(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			curve = new AnimationCurve<Quaternion>(reader, reader.ReadQuaternion);
			path = reader.ReadNameA4();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			curve.WriteTo(writer, writer.Write);
			writer.WriteNameA4(path);
		}
	}

	public class Vector3Curve : IObjInfo
	{
		public AnimationCurve<Vector3> curve { get; set; }
		public string path { get; set; }

		public Vector3Curve(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			curve = new AnimationCurve<Vector3>(reader, reader.ReadVector3);
			path = reader.ReadNameA4();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			curve.WriteTo(writer, writer.Write);
			writer.WriteNameA4(path);
		}
	}

	public class FloatCurve : IObjInfo
	{
		public AnimationCurve<float> curve { get; set; }
		public string attribute { get; set; }
		public string path { get; set; }
		public int classID { get; set; }
		public PPtr<MonoScript> script { get; set; }

		private AssetCabinet file;

		public FloatCurve(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			curve = new AnimationCurve<float>(reader, reader.ReadSingle);
			attribute = reader.ReadNameA4();
			path = reader.ReadNameA4();
			classID = reader.ReadInt32();
			script = new PPtr<MonoScript>(stream, file);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			curve.WriteTo(writer, writer.Write);
			writer.WriteNameA4(attribute);
			writer.WriteNameA4(path);
			writer.Write(classID);
			file.WritePPtr(script.asset, false, stream);
		}
	}

	public class PPtrKeyframe : IObjInfo
	{
		public float time { get; set; }
		public PPtr<Object> value { get; set; }

		private AssetCabinet file;

		public PPtrKeyframe(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			time = reader.ReadSingle();
			value = new PPtr<Object>(stream, file);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(time);
			file.WritePPtr(value.asset, false, stream);
		}
	}

	public class PPtrCurve : IObjInfo
	{
		public List<PPtrKeyframe> curve { get; set; }
		public string attribute { get; set; }
		public string path { get; set; }
		public int classID { get; set; }
		public PPtr<MonoScript> script { get; set; }

		private AssetCabinet file;

		public PPtrCurve(AssetCabinet file, Stream stream)
		{
			this.file = file;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numCurves = reader.ReadInt32();
			curve = new List<PPtrKeyframe>(numCurves);
			for (int i = 0; i < numCurves; i++)
			{
				curve.Add(new PPtrKeyframe(file, stream));
			}

			attribute = reader.ReadNameA4();
			path = reader.ReadNameA4();
			classID = reader.ReadInt32();
			script = new PPtr<MonoScript>(stream, file);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(curve.Count);
			for (int i = 0; i < curve.Count; i++)
			{
				curve[i].WriteTo(stream);
			}

			writer.WriteNameA4(attribute);
			writer.WriteNameA4(path);
			writer.Write(classID);
			file.WritePPtr(script.asset, false, stream);
		}
	}

	public class HumanGoal : IObjInfo
	{
		public xform m_X { get; set; }
		public float m_WeightT { get; set; }
		public float m_WeightR { get; set; }

		public HumanGoal(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_X = new xform(stream);
			m_WeightT = reader.ReadSingle();
			m_WeightR = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			m_X.WriteTo(stream);
			writer.Write(m_WeightT);
			writer.Write(m_WeightR);
		}
	}

	public class HandPose : IObjInfo
	{
		public xform m_GrabX { get; set; }
		public float[] m_DoFArray { get; set; }
		public float m_Override { get; set; }
		public float m_CloseOpen { get; set; }
		public float m_InOut { get; set; }
		public float m_Grab { get; set; }

		public HandPose(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_GrabX = new xform(stream);

			int numDoFs = reader.ReadInt32();
			m_DoFArray = reader.ReadSingleArray(numDoFs);

			m_Override = reader.ReadSingle();
			m_CloseOpen = reader.ReadSingle();
			m_InOut = reader.ReadSingle();
			m_Grab = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			m_GrabX.WriteTo(stream);

			writer.Write(m_DoFArray.Length);
			writer.Write(m_DoFArray);

			writer.Write(m_Override);
			writer.Write(m_CloseOpen);
			writer.Write(m_InOut);
			writer.Write(m_Grab);
		}
	}

	public class HumanPose : IObjInfo
	{
		public xform m_RootX { get; set; }
		public Vector4 m_LookAtPosition { get; set; }
		public Vector4 m_LookAtWeight { get; set; }
		public List<HumanGoal> m_GoalArray { get; set; }
		public HandPose m_LeftHandPose { get; set; }
		public HandPose m_RightHandPose { get; set; }
		public float[] m_DoFArray { get; set; }

		public HumanPose(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_RootX = new xform(stream);
			m_LookAtPosition = reader.ReadVector4();
			m_LookAtWeight = reader.ReadVector4();

			int numGoals = reader.ReadInt32();
			m_GoalArray = new List<HumanGoal>(numGoals);
			for (int i = 0; i < numGoals; i++)
			{
				m_GoalArray.Add(new HumanGoal(stream));
			}

			m_LeftHandPose = new HandPose(stream);
			m_RightHandPose = new HandPose(stream);

			int numDoFs = reader.ReadInt32();
			m_DoFArray = reader.ReadSingleArray(numDoFs);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			m_RootX.WriteTo(stream);
			writer.Write(m_LookAtPosition);
			writer.Write(m_LookAtWeight);

			writer.Write(m_GoalArray.Count);
			for (int i = 0; i < m_GoalArray.Count; i++)
			{
				m_GoalArray[i].WriteTo(stream);
			}

			m_LeftHandPose.WriteTo(stream);
			m_RightHandPose.WriteTo(stream);

			writer.Write(m_DoFArray.Length);
			writer.Write(m_DoFArray);
		}
	}

	public class StreamedClip : IObjInfo
	{
		public uint[] data { get; set; }
		public uint curveCount { get; set; }

		public StreamedClip(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			data = reader.ReadUInt32Array(reader.ReadInt32());
			curveCount = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(data.Length);
			writer.Write(data);

			writer.Write(curveCount);
		}
	}

	public class DenseClip : IObjInfo
	{
		public int m_FrameCount { get; set; }
		public uint m_CurveCount { get; set; }
		public float m_SampleRate { get; set; }
		public float m_BeginTime { get; set; }
		public float[] m_SampleArray { get; set; }

		public DenseClip(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_FrameCount = reader.ReadInt32();
			m_CurveCount = reader.ReadUInt32();
			m_SampleRate = reader.ReadSingle();
			m_BeginTime = reader.ReadSingle();

			int numSamples = reader.ReadInt32();
			m_SampleArray = reader.ReadSingleArray(numSamples);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_FrameCount);
			writer.Write(m_CurveCount);
			writer.Write(m_SampleRate);
			writer.Write(m_BeginTime);

			writer.Write(m_SampleArray.Length);
			writer.Write(m_SampleArray);
		}
	}

	public class ConstantClip : IObjInfo
	{
		public float[] data { get; set; }

		public ConstantClip(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			data = reader.ReadSingleArray(reader.ReadInt32());
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(data.Length);
			writer.Write(data);
		}
	}

	public class ValueConstant : IObjInfo
	{
		public uint m_ID { get; set; }
		public uint m_TypeID { get; set; }
		public uint m_Type { get; set; }
		public uint m_Index { get; set; }

		public ValueConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_ID = reader.ReadUInt32();
			m_TypeID = reader.ReadUInt32();
			m_Type = reader.ReadUInt32();
			m_Index = reader.ReadUInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_ID);
			writer.Write(m_TypeID);
			writer.Write(m_Type);
			writer.Write(m_Index);
		}
	}

	public class ValueArrayConstant : IObjInfo
	{
		public List<ValueConstant> m_ValueArray { get; set; }

		public ValueArrayConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numVals = reader.ReadInt32();
			m_ValueArray = new List<ValueConstant>(numVals);
			for (int i = 0; i < numVals; i++)
			{
				m_ValueArray.Add(new ValueConstant(stream));
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(m_ValueArray.Count);
			for (int i = 0; i < m_ValueArray.Count; i++)
			{
				m_ValueArray[i].WriteTo(stream);
			}
		}
	}

	public class Clip : IObjInfo
	{
		public StreamedClip m_StreamedClip { get; set; }
		public DenseClip m_DenseClip { get; set; }
		public ConstantClip m_ConstantClip { get; set; }
		public ValueArrayConstant m_Binding { get; set; }

		public Clip(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			m_StreamedClip = new StreamedClip(stream);
			m_DenseClip = new DenseClip(stream);
			m_ConstantClip = new ConstantClip(stream);
			m_Binding = new ValueArrayConstant(stream);
		}

		public void WriteTo(Stream stream)
		{
			m_StreamedClip.WriteTo(stream);
			m_DenseClip.WriteTo(stream);
			m_ConstantClip.WriteTo(stream);
			m_Binding.WriteTo(stream);
		}
	}

	public class ValueDelta : IObjInfo
	{
		public float m_Start { get; set; }
		public float m_Stop { get; set; }

		public ValueDelta(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Start = reader.ReadSingle();
			m_Stop = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_Start);
			writer.Write(m_Stop);
		}
	}

	public class ClipMuscleConstant : IObjInfo
	{
		public HumanPose m_DeltaPose { get; set; }
		public xform m_StartX { get; set; }
		public xform m_LeftFootStartX { get; set; }
		public xform m_RightFootStartX { get; set; }
		public xform m_MotionStartX { get; set; }
		public xform m_MotionStopX { get; set; }
		public Vector4 m_AverageSpeed { get; set; }
		public Clip m_Clip { get; set; }
		public float m_StartTime { get; set; }
		public float m_StopTime { get; set; }
		public float m_OrientationOffsetY { get; set; }
		public float m_Level { get; set; }
		public float m_CycleOffset { get; set; }
		public float m_AverageAngularSpeed { get; set; }
		public int[] m_IndexArray { get; set; }
		public List<ValueDelta> m_ValueArrayDelta { get; set; }
		public bool m_Mirror { get; set; }
		public bool m_LoopTime { get; set; }
		public bool m_LoopBlend { get; set; }
		public bool m_LoopBlendOrientation { get; set; }
		public bool m_LoopBlendPositionY { get; set; }
		public bool m_LoopBlendPositionXZ { get; set; }
		public bool m_KeepOriginalOrientation { get; set; }
		public bool m_KeepOriginalPositionY { get; set; }
		public bool m_KeepOriginalPositionXZ { get; set; }
		public bool m_HeightFromFeet { get; set; }

		public ClipMuscleConstant(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_DeltaPose = new HumanPose(stream);
			m_StartX = new xform(stream);
			m_LeftFootStartX = new xform(stream);
			m_RightFootStartX = new xform(stream);
			m_MotionStartX = new xform(stream);
			m_MotionStopX = new xform(stream);
			m_AverageSpeed = reader.ReadVector4();
			m_Clip = new Clip(stream);
			m_StartTime = reader.ReadSingle();
			m_StopTime = reader.ReadSingle();
			m_OrientationOffsetY = reader.ReadSingle();
			m_Level = reader.ReadSingle();
			m_CycleOffset = reader.ReadSingle();
			m_AverageAngularSpeed = reader.ReadSingle();

			int numIndices = reader.ReadInt32();
			m_IndexArray = reader.ReadInt32Array(numIndices);

			int numDeltas = reader.ReadInt32();
			m_ValueArrayDelta = new List<ValueDelta>(numDeltas);
			for (int i = 0; i < numDeltas; i++)
			{
				m_ValueArrayDelta.Add(new ValueDelta(stream));
			}

			m_Mirror = reader.ReadBoolean();
			m_LoopTime = reader.ReadBoolean();
			m_LoopBlend = reader.ReadBoolean();
			m_LoopBlendOrientation = reader.ReadBoolean();
			m_LoopBlendPositionY = reader.ReadBoolean();
			m_LoopBlendPositionXZ = reader.ReadBoolean();
			m_KeepOriginalOrientation = reader.ReadBoolean();
			m_KeepOriginalPositionY = reader.ReadBoolean();
			m_KeepOriginalPositionXZ = reader.ReadBoolean();
			m_HeightFromFeet = reader.ReadBoolean();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			m_DeltaPose.WriteTo(stream);
			m_StartX.WriteTo(stream);
			m_LeftFootStartX.WriteTo(stream);
			m_RightFootStartX.WriteTo(stream);
			m_MotionStartX.WriteTo(stream);
			m_MotionStopX.WriteTo(stream);
			writer.Write(m_AverageSpeed);
			m_Clip.WriteTo(stream);
			writer.Write(m_StartTime);
			writer.Write(m_StopTime);
			writer.Write(m_OrientationOffsetY);
			writer.Write(m_Level);
			writer.Write(m_CycleOffset);
			writer.Write(m_AverageAngularSpeed);

			writer.Write(m_IndexArray.Length);
			writer.Write(m_IndexArray);

			writer.Write(m_ValueArrayDelta.Count);
			for (int i = 0; i < m_ValueArrayDelta.Count; i++)
			{
				m_ValueArrayDelta[i].WriteTo(stream);
			}

			writer.Write(m_Mirror);
			writer.Write(m_LoopTime);
			writer.Write(m_LoopBlend);
			writer.Write(m_LoopBlendOrientation);
			writer.Write(m_LoopBlendPositionY);
			writer.Write(m_LoopBlendPositionXZ);
			writer.Write(m_KeepOriginalOrientation);
			writer.Write(m_KeepOriginalPositionY);
			writer.Write(m_KeepOriginalPositionXZ);
			writer.Write(m_HeightFromFeet);
			writer.Write(new byte[2]);
		}
	}

	public class PackedBitVector3 : IObjInfo
	{
		public uint m_NumItems { get; set; }
		public byte[] m_Data { get; set; }

		public PackedBitVector3(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_NumItems = reader.ReadUInt32();

			int numData = reader.ReadInt32();
			m_Data = reader.ReadBytes(numData);

			if ((numData & 3) > 0)
			{
				reader.ReadBytes(4 - (numData & 3));
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(m_NumItems);

			writer.Write(m_Data.Length);
			writer.Write(m_Data);

			if ((m_Data.Length & 3) > 0)
			{
				writer.Write(new byte[4 - (m_Data.Length & 3)]);
			}
		}
	}

	public class CompressedAnimationCurve : IObjInfo
	{
		public string m_Path { get; set; }
		public PackedBitVector2 m_Times { get; set; }
		public PackedBitVector3 m_Values { get; set; }
		public PackedBitVector m_Slopes { get; set; }
		public int m_PreInfinity { get; set; }
		public int m_PostInfinity { get; set; }

		public CompressedAnimationCurve(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Path = reader.ReadNameA4();
			m_Times = new PackedBitVector2(stream);
			m_Values = new PackedBitVector3(stream);
			m_Slopes = new PackedBitVector(stream);
			m_PreInfinity = reader.ReadInt32();
			m_PostInfinity = reader.ReadInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Path);
			m_Times.WriteTo(stream);
			m_Values.WriteTo(stream);
			m_Slopes.WriteTo(stream);
			writer.Write(m_PreInfinity);
			writer.Write(m_PostInfinity);
		}
	}

	public class GenericBinding : IObjInfo
	{
		public uint path { get; set; }
		public uint attribute { get; set; }
		public PPtr<Object> script { get; set; }
		public UInt16 classID { get; set; }
		public byte customType { get; set; }
		public byte isPPtrCurve { get; set; }

		private AssetCabinet file;

		public GenericBinding(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			path = reader.ReadUInt32();
			attribute = reader.ReadUInt32();
			script = new PPtr<Object>(stream, file);
			classID = reader.ReadUInt16();
			customType = reader.ReadByte();
			isPPtrCurve = reader.ReadByte();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(path);
			writer.Write(attribute);
			file.WritePPtr(script.asset, false, stream);
			writer.Write(classID);
			writer.Write(customType);
			writer.Write(isPPtrCurve);
		}
	}

	public class AnimationClipBindingConstant : IObjInfo
	{
		public List<GenericBinding> genericBindings { get; set; }
		public List<PPtr<Object>> pptrCurveMapping { get; set; }

		private AssetCabinet file;

		public AnimationClipBindingConstant(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			int numBindings = reader.ReadInt32();
			genericBindings = new List<GenericBinding>(numBindings);
			for (int i = 0; i < numBindings; i++)
			{
				genericBindings.Add(new GenericBinding(file, stream));
			}
			GenericBinding bind = genericBindings[0];
			Console.WriteLine(0 + " x" + bind.path.ToString("X"));

			int numMappings = reader.ReadInt32();
			pptrCurveMapping = new List<PPtr<Object>>(numMappings);
			for (int i = 0; i < numMappings; i++)
			{
				pptrCurveMapping.Add(new PPtr<Object>(stream, file));
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(genericBindings.Count);
			for (int i = 0; i < genericBindings.Count; i++)
			{
				genericBindings[i].WriteTo(stream);
			}

			writer.Write(pptrCurveMapping.Count);
			for (int i = 0; i < pptrCurveMapping.Count; i++)
			{
				file.WritePPtr(pptrCurveMapping[i].asset, false, stream);
			}
		}
	}

	public class AnimationEvent : IObjInfo
	{
		public float time { get; set; }
		public string functionName { get; set; }
		public string data { get; set; }
		public PPtr<Object> objectReferenceParameter { get; set; }
		public float floatParameter { get; set; }
		public int intParameter { get; set; }
		public int messageOptions { get; set; }

		private AssetCabinet file;

		public AnimationEvent(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			time = reader.ReadSingle();
			functionName = reader.ReadNameA4();
			data = reader.ReadNameA4();
			objectReferenceParameter = new PPtr<Object>(stream, file);
			floatParameter = reader.ReadSingle();
			intParameter = reader.ReadInt32();
			messageOptions = reader.ReadInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(time);
			writer.WriteNameA4(functionName);
			writer.WriteNameA4(data);
			file.WritePPtr(objectReferenceParameter.asset, false, stream);
			writer.Write(floatParameter);
			writer.Write(intParameter);
			writer.Write(messageOptions);
		}
	}

	public class AnimationClip : Component, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public string m_Name { get; set; }
		public int m_AnimationType { get; set; }
		public bool m_Compressed { get; set; }
		public bool m_UseHighQualityCurve { get; set; }
		public List<QuaternionCurve> m_RotationCurves { get; set; }
		public List<CompressedAnimationCurve> m_CompressedRotationCurves { get; set; }
		public List<Vector3Curve> m_PositionCurves { get; set; }
		public List<Vector3Curve> m_ScaleCurves { get; set; }
		public List<FloatCurve> m_FloatCurves { get; set; }
		public List<PPtrCurve> m_PPtrCurves { get; set; }
		public float m_SampleRate { get; set; }
		public int m_WrapMode { get; set; }
		public AABB m_Bounds { get; set; }
		public uint m_MuscleClipSize { get; set; }
		public ClipMuscleConstant m_MuscleClip { get; set; }
		public AnimationClipBindingConstant m_ClipBindingConstant { get; set; }
		public List<AnimationEvent> m_Events { get; set; }

		public AnimationClip(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
		{
			this.file = file;
			this.pathID = pathID;
			this.classID1 = classID1;
			this.classID2 = classID2;
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			m_Name = reader.ReadNameA4();
			m_AnimationType = reader.ReadInt32();
			m_Compressed = reader.ReadBoolean();
			m_UseHighQualityCurve = reader.ReadBoolean();
			reader.ReadBytes(2);

			int numRCurves = reader.ReadInt32();
			m_RotationCurves = new List<QuaternionCurve>(numRCurves);
			for (int i = 0; i < numRCurves; i++)
			{
				m_RotationCurves.Add(new QuaternionCurve(stream));
			}

			int numCRCurves = reader.ReadInt32();
			m_CompressedRotationCurves = new List<CompressedAnimationCurve>(numCRCurves);
			for (int i = 0; i < numCRCurves; i++)
			{
				m_CompressedRotationCurves.Add(new CompressedAnimationCurve(stream));
			}

			int numPCurves = reader.ReadInt32();
			m_PositionCurves = new List<Vector3Curve>(numPCurves);
			for (int i = 0; i < numPCurves; i++)
			{
				m_PositionCurves.Add(new Vector3Curve(stream));
			}

			int numSCurves = reader.ReadInt32();
			m_ScaleCurves = new List<Vector3Curve>(numSCurves);
			for (int i = 0; i < numSCurves; i++)
			{
				m_ScaleCurves.Add(new Vector3Curve(stream));
			}

			int numFCurves = reader.ReadInt32();
			m_FloatCurves = new List<FloatCurve>(numFCurves);
			for (int i = 0; i < numFCurves; i++)
			{
				m_FloatCurves.Add(new FloatCurve(file, stream));
			}

			int numPtrCurves = reader.ReadInt32();
			m_PPtrCurves = new List<PPtrCurve>(numPtrCurves);
			for (int i = 0; i < numPtrCurves; i++)
			{
				m_PPtrCurves.Add(new PPtrCurve(file, stream));
			}

			m_SampleRate = reader.ReadSingle();
			m_WrapMode = reader.ReadInt32();
			m_Bounds = new AABB(stream);
			m_MuscleClipSize = reader.ReadUInt32();
			m_MuscleClip = new ClipMuscleConstant(stream);
			m_ClipBindingConstant = new AnimationClipBindingConstant(file, stream);

			int numEvents = reader.ReadInt32();
			m_Events = new List<AnimationEvent>(numEvents);
			for (int i = 0; i < numEvents; i++)
			{
				m_Events.Add(new AnimationEvent(file, stream));
			}
		}

		public void WriteTo(Stream stream)
		{
			long beginPos = stream.Position;
			BinaryWriter writer = new BinaryWriter(stream);
			writer.WriteNameA4(m_Name);
			writer.Write(m_AnimationType);
			writer.Write(m_Compressed);
			writer.Write(m_UseHighQualityCurve);
			writer.Write(new byte[2]);

			writer.Write(m_RotationCurves.Count);
			for (int i = 0; i < m_RotationCurves.Count; i++)
			{
				m_RotationCurves[i].WriteTo(stream);
			}

			writer.Write(m_CompressedRotationCurves.Count);
			for (int i = 0; i < m_CompressedRotationCurves.Count; i++)
			{
				m_CompressedRotationCurves[i].WriteTo(stream);
			}

			writer.Write(m_PositionCurves.Count);
			for (int i = 0; i < m_PositionCurves.Count; i++)
			{
				m_PositionCurves[i].WriteTo(stream);
			}

			writer.Write(m_ScaleCurves.Count);
			for (int i = 0; i < m_ScaleCurves.Count; i++)
			{
				m_ScaleCurves[i].WriteTo(stream);
			}

			writer.Write(m_FloatCurves.Count);
			for (int i = 0; i < m_FloatCurves.Count; i++)
			{
				m_FloatCurves[i].WriteTo(stream);
			}

			writer.Write(m_PPtrCurves.Count);
			for (int i = 0; i < m_PPtrCurves.Count; i++)
			{

				m_PPtrCurves[i].WriteTo(stream);
			}

			writer.Write(m_SampleRate);
			writer.Write(m_WrapMode);
			m_Bounds.WriteTo(stream);
			writer.Write(m_MuscleClipSize);
			m_MuscleClip.WriteTo(stream);
			m_ClipBindingConstant.WriteTo(stream);

			writer.Write(m_Events.Count);
			for (int i = 0; i < m_Events.Count; i++)
			{
				m_Events[i].WriteTo(stream);
			}
		}
	}
}
