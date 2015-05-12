using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public class MinMaxCurve : IObjInfo
	{
		public float scalar { get; set; }
		public AnimationCurve<float> maxCurve { get; set; }
		public AnimationCurve<float> minCurve { get; set; }
		public Int16 minMaxState { get; set; }

		public MinMaxCurve(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			scalar = reader.ReadSingle();
			maxCurve = new AnimationCurve<float>(reader, reader.ReadSingle);
			minCurve = new AnimationCurve<float>(reader, reader.ReadSingle);
			minMaxState = reader.ReadInt16();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(scalar);
			maxCurve.WriteTo(writer, writer.Write);
			minCurve.WriteTo(writer, writer.Write);
			writer.Write(minMaxState);
			writer.Write(new byte[2]);
		}
	}

	public class GradientNEW : IObjInfo
	{
		public Color4 key0 { get; set; }
		public Color4 key1 { get; set; }
		public Color4 key2 { get; set; }
		public Color4 key3 { get; set; }
		public Color4 key4 { get; set; }
		public Color4 key5 { get; set; }
		public Color4 key6 { get; set; }
		public Color4 key7 { get; set; }
		public UInt16 ctime0 { get; set; }
		public UInt16 ctime1 { get; set; }
		public UInt16 ctime2 { get; set; }
		public UInt16 ctime3 { get; set; }
		public UInt16 ctime4 { get; set; }
		public UInt16 ctime5 { get; set; }
		public UInt16 ctime6 { get; set; }
		public UInt16 ctime7 { get; set; }
		public UInt16 atime0 { get; set; }
		public UInt16 atime1 { get; set; }
		public UInt16 atime2 { get; set; }
		public UInt16 atime3 { get; set; }
		public UInt16 atime4 { get; set; }
		public UInt16 atime5 { get; set; }
		public UInt16 atime6 { get; set; }
		public UInt16 atime7 { get; set; }
		public byte m_NumColorKeys { get; set; }
		public byte m_NumAlphaKeys { get; set; }

		public GradientNEW(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			key0 = new Color4(reader.ReadInt32());
			key1 = new Color4(reader.ReadInt32());
			key2 = new Color4(reader.ReadInt32());
			key3 = new Color4(reader.ReadInt32());
			key4 = new Color4(reader.ReadInt32());
			key5 = new Color4(reader.ReadInt32());
			key6 = new Color4(reader.ReadInt32());
			key7 = new Color4(reader.ReadInt32());
			ctime0 = reader.ReadUInt16();
			ctime1 = reader.ReadUInt16();
			ctime2 = reader.ReadUInt16();
			ctime3 = reader.ReadUInt16();
			ctime4 = reader.ReadUInt16();
			ctime5 = reader.ReadUInt16();
			ctime6 = reader.ReadUInt16();
			ctime7 = reader.ReadUInt16();
			atime0 = reader.ReadUInt16();
			atime1 = reader.ReadUInt16();
			atime2 = reader.ReadUInt16();
			atime3 = reader.ReadUInt16();
			atime4 = reader.ReadUInt16();
			atime5 = reader.ReadUInt16();
			atime6 = reader.ReadUInt16();
			atime7 = reader.ReadUInt16();
			m_NumColorKeys = reader.ReadByte();
			m_NumAlphaKeys = reader.ReadByte();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(key0.ToArgb());
			writer.Write(key1.ToArgb());
			writer.Write(key2.ToArgb());
			writer.Write(key3.ToArgb());
			writer.Write(key4.ToArgb());
			writer.Write(key5.ToArgb());
			writer.Write(key6.ToArgb());
			writer.Write(key7.ToArgb());
			writer.Write(ctime0);
			writer.Write(ctime1);
			writer.Write(ctime2);
			writer.Write(ctime3);
			writer.Write(ctime4);
			writer.Write(ctime5);
			writer.Write(ctime6);
			writer.Write(ctime7);
			writer.Write(atime0);
			writer.Write(atime1);
			writer.Write(atime2);
			writer.Write(atime3);
			writer.Write(atime4);
			writer.Write(atime5);
			writer.Write(atime6);
			writer.Write(atime7);
			writer.Write(m_NumColorKeys);
			writer.Write(m_NumAlphaKeys);
			writer.Write(new byte[2]);
		}
	}

	public class MinMaxGradient : IObjInfo
	{
		public GradientNEW maxGradient { get; set; }
		public GradientNEW minGradient { get; set; }
		public Color4 minColor { get; set; }
		public Color4 maxColor { get; set; }
		public Int16 minMaxState { get; set; }

		public MinMaxGradient(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			maxGradient = new GradientNEW(stream);
			minGradient = new GradientNEW(stream);
			minColor = new Color4(reader.ReadInt32());
			maxColor = new Color4(reader.ReadInt32());
			minMaxState = reader.ReadInt16();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			maxGradient.WriteTo(stream);
			minGradient.WriteTo(stream);
			writer.Write(minColor.ToArgb());
			writer.Write(maxColor.ToArgb());
			writer.Write(minMaxState);
			writer.Write(new byte[2]);
		}
	}

	public class InitialModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve startLifetime { get; set; }
		public MinMaxCurve startSpeed { get; set; }
		public MinMaxGradient startColor { get; set; }
		public MinMaxCurve startSize { get; set; }
		public MinMaxCurve startRotation { get; set; }
		public float gravityModifier { get; set; }
		public float inheritVelocity { get; set; }
		public int maxNumParticles { get; set; }

		public InitialModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			startLifetime = new MinMaxCurve(stream);
			startSpeed = new MinMaxCurve(stream);
			startColor = new MinMaxGradient(stream);
			startSize = new MinMaxCurve(stream);
			startRotation = new MinMaxCurve(stream);
			gravityModifier = reader.ReadSingle();
			inheritVelocity = reader.ReadSingle();
			maxNumParticles = reader.ReadInt32();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			startLifetime.WriteTo(stream);
			startSpeed.WriteTo(stream);
			startColor.WriteTo(stream);
			startSize.WriteTo(stream);
			startRotation.WriteTo(stream);
			writer.Write(gravityModifier);
			writer.Write(inheritVelocity);
			writer.Write(maxNumParticles);
		}
	}

	public class ShapeModule : IObjInfo
	{
		public bool enabled { get; set; }
		public int type { get; set; }
		public float radius { get; set; }
		public float angle { get; set; }
		public float length { get; set; }
		public float boxX { get; set; }
		public float boxY { get; set; }
		public float boxZ { get; set; }
		public int placementMode { get; set; }
		public PPtr<Mesh> m_Mesh { get; set; }
		public bool randomDirection { get; set; }

		private AssetCabinet file;

		public ShapeModule(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			type = reader.ReadInt32();
			radius = reader.ReadSingle();
			angle = reader.ReadSingle();
			length = reader.ReadSingle();
			boxX = reader.ReadSingle();
			boxY = reader.ReadSingle();
			boxZ = reader.ReadSingle();
			placementMode = reader.ReadInt32();
			m_Mesh = new PPtr<Mesh>(stream, file);
			randomDirection = reader.ReadBoolean();
			reader.ReadBytes(3);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			writer.Write(type);
			writer.Write(radius);
			writer.Write(angle);
			writer.Write(length);
			writer.Write(boxX);
			writer.Write(boxY);
			writer.Write(boxZ);
			writer.Write(placementMode);
			m_Mesh.WriteTo(stream);
			writer.Write(randomDirection);
			writer.Write(new byte[3]);
		}
	}

	public class EmissionModule : IObjInfo
	{
		public bool enabled { get; set; }
		public int m_Type { get; set; }
		public MinMaxCurve rate { get; set; }
		public UInt16 cnt0 { get; set; }
		public UInt16 cnt1 { get; set; }
		public UInt16 cnt2 { get; set; }
		public UInt16 cnt3 { get; set; }
		public float time0 { get; set; }
		public float time1 { get; set; }
		public float time2 { get; set; }
		public float time3 { get; set; }
		public byte m_BurstCount { get; set; }

		public EmissionModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			m_Type = reader.ReadInt32();
			rate = new MinMaxCurve(stream);
			cnt0 = reader.ReadUInt16();
			cnt1 = reader.ReadUInt16();
			cnt2 = reader.ReadUInt16();
			cnt3 = reader.ReadUInt16();
			time0 = reader.ReadSingle();
			time1 = reader.ReadSingle();
			time2 = reader.ReadSingle();
			time3 = reader.ReadSingle();
			m_BurstCount = reader.ReadByte();
			reader.ReadBytes(3);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			writer.Write(m_Type);
			rate.WriteTo(stream);
			writer.Write(cnt0);
			writer.Write(cnt1);
			writer.Write(cnt2);
			writer.Write(cnt3);
			writer.Write(time0);
			writer.Write(time1);
			writer.Write(time2);
			writer.Write(time3);
			writer.Write(m_BurstCount);
			writer.Write(new byte[3]);
		}
	}

	public class SizeModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve curve { get; set; }

		public SizeModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			curve = new MinMaxCurve(stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			curve.WriteTo(stream);
		}
	}

	public class RotationModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve curve { get; set; }

		public RotationModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			curve = new MinMaxCurve(stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			curve.WriteTo(stream);
		}
	}

	public class ColorModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxGradient gradient { get; set; }

		public ColorModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			gradient = new MinMaxGradient(stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			gradient.WriteTo(stream);
		}
	}

	public class UVModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve frameOverTime { get; set; }
		public int tilesX { get; set; }
		public int tilesY { get; set; }
		public int animationType { get; set; }
		public int rowIndex { get; set; }
		public float cycles { get; set; }
		public bool randomRow { get; set; }

		public UVModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			frameOverTime = new MinMaxCurve(stream);
			tilesX = reader.ReadInt32();
			tilesY = reader.ReadInt32();
			animationType = reader.ReadInt32();
			rowIndex = reader.ReadInt32();
			cycles = reader.ReadSingle();
			randomRow = reader.ReadBoolean();
			reader.ReadBytes(3);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			frameOverTime.WriteTo(stream);
			writer.Write(tilesX);
			writer.Write(tilesY);
			writer.Write(animationType);
			writer.Write(rowIndex);
			writer.Write(cycles);
			writer.Write(randomRow);
			writer.Write(new byte[3]);
		}
	}

	public class VelocityModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve x { get; set; }
		public MinMaxCurve y { get; set; }
		public MinMaxCurve z { get; set; }
		public bool inWorldSpace { get; set; }

		public VelocityModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			x = new MinMaxCurve(stream);
			y = new MinMaxCurve(stream);
			z = new MinMaxCurve(stream);
			inWorldSpace = reader.ReadBoolean();
			reader.ReadBytes(3);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			x.WriteTo(stream);
			y.WriteTo(stream);
			z.WriteTo(stream);
			writer.Write(inWorldSpace);
			writer.Write(new byte[3]);
		}
	}

	public class ForceModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve x { get; set; }
		public MinMaxCurve y { get; set; }
		public MinMaxCurve z { get; set; }
		public bool inWorldSpace { get; set; }
		public bool randomizePerFrame { get; set; }

		public ForceModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			x = new MinMaxCurve(stream);
			y = new MinMaxCurve(stream);
			z = new MinMaxCurve(stream);
			inWorldSpace = reader.ReadBoolean();
			randomizePerFrame = reader.ReadBoolean();
			reader.ReadBytes(2);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			x.WriteTo(stream);
			y.WriteTo(stream);
			z.WriteTo(stream);
			writer.Write(inWorldSpace);
			writer.Write(randomizePerFrame);
			writer.Write(new byte[2]);
		}
	}

	public class ExternalForcesModule : IObjInfo
	{
		public bool enabled { get; set; }
		public float multiplier { get; set; }

		public ExternalForcesModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			multiplier = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			writer.Write(multiplier);
		}
	}

	public class ClampVelocityModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve x { get; set; }
		public MinMaxCurve y { get; set; }
		public MinMaxCurve z { get; set; }
		public MinMaxCurve magnitude { get; set; }
		public bool separateAxis { get; set; }
		public bool inWorldSpace { get; set; }
		public float dampen { get; set; }

		public ClampVelocityModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			x = new MinMaxCurve(stream);
			y = new MinMaxCurve(stream);
			z = new MinMaxCurve(stream);
			magnitude = new MinMaxCurve(stream);
			separateAxis = reader.ReadBoolean();
			inWorldSpace = reader.ReadBoolean();
			reader.ReadBytes(2);
			dampen = reader.ReadSingle();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			x.WriteTo(stream);
			y.WriteTo(stream);
			z.WriteTo(stream);
			magnitude.WriteTo(stream);
			writer.Write(separateAxis);
			writer.Write(inWorldSpace);
			writer.Write(new byte[2]);
			writer.Write(dampen);
		}
	}

	public class SizeBySpeedModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve curve { get; set; }
		public Vector2 range { get; set; }

		public SizeBySpeedModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			curve = new MinMaxCurve(stream);
			range = reader.ReadVector2();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			curve.WriteTo(stream);
			writer.Write(range);
		}
	}

	public class RotationBySpeedModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxCurve curve { get; set; }
		public Vector2 range { get; set; }

		public RotationBySpeedModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			curve = new MinMaxCurve(stream);
			range = reader.ReadVector2();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			curve.WriteTo(stream);
			writer.Write(range);
		}
	}

	public class ColorBySpeedModule : IObjInfo
	{
		public bool enabled { get; set; }
		public MinMaxGradient gradient { get; set; }
		public Vector2 range { get; set; }
	
		public ColorBySpeedModule(Stream stream)
		{
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			gradient = new MinMaxGradient(stream);
			range = reader.ReadVector2();
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			gradient.WriteTo(stream);
			writer.Write(range);
		}
	}

	public class CollisionModule : IObjInfo
	{
		public bool enabled { get; set; }
		public int type { get; set; }
		public PPtr<Transform> plane0 { get; set; }
		public PPtr<Transform> plane1 { get; set; }
		public PPtr<Transform> plane2 { get; set; }
		public PPtr<Transform> plane3 { get; set; }
		public PPtr<Transform> plane4 { get; set; }
		public PPtr<Transform> plane5 { get; set; }
		public float dampen { get; set; }
		public float bounce { get; set; }
		public float energyLossOnCollision { get; set; }
		public float minKillSpeed { get; set; }
		public float particleRadius { get; set; }
		public BitField collidesWith { get; set; }
		public int quality { get; set; }
		public float voxelSize { get; set; }
		public bool collisionMessages { get; set; }

		AssetCabinet file;

		public CollisionModule(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			type = reader.ReadInt32();
			plane0 = new PPtr<Transform>(stream, file);
			plane1 = new PPtr<Transform>(stream, file);
			plane2 = new PPtr<Transform>(stream, file);
			plane3 = new PPtr<Transform>(stream, file);
			plane4 = new PPtr<Transform>(stream, file);
			plane5 = new PPtr<Transform>(stream, file);
			dampen = reader.ReadSingle();
			bounce = reader.ReadSingle();
			energyLossOnCollision = reader.ReadSingle();
			minKillSpeed = reader.ReadSingle();
			particleRadius = reader.ReadSingle();
			collidesWith = new BitField(stream);
			quality = reader.ReadInt32();
			voxelSize = reader.ReadSingle();
			collisionMessages = reader.ReadBoolean();
			reader.ReadBytes(3);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			writer.Write(type);
			plane0.WriteTo(stream);
			plane1.WriteTo(stream);
			plane2.WriteTo(stream);
			plane3.WriteTo(stream);
			plane4.WriteTo(stream);
			plane5.WriteTo(stream);
			writer.Write(dampen);
			writer.Write(bounce);
			writer.Write(energyLossOnCollision);
			writer.Write(minKillSpeed);
			writer.Write(particleRadius);
			collidesWith.WriteTo(stream);
			writer.Write(quality);
			writer.Write(voxelSize);
			writer.Write(collisionMessages);
			writer.Write(new byte[3]);
		}
	}

	public class SubModule : IObjInfo
	{
		public bool enabled { get; set; }
		public PPtr<ParticleSystem> subEmitterBirth { get; set; }
		public PPtr<ParticleSystem> subEmitterBirth1 { get; set; }
		public PPtr<ParticleSystem> subEmitterCollision { get; set; }
		public PPtr<ParticleSystem> subEmitterCollision1 { get; set; }
		public PPtr<ParticleSystem> subEmitterDeath { get; set; }
		public PPtr<ParticleSystem> subEmitterDeath1 { get; set; }

		AssetCabinet file;

		public SubModule(AssetCabinet file, Stream stream)
		{
			this.file = file;
			LoadFrom(stream);
		}

		public void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			enabled = reader.ReadBoolean();
			reader.ReadBytes(3);
			subEmitterBirth = new PPtr<ParticleSystem>(stream, file);
			subEmitterBirth1 = new PPtr<ParticleSystem>(stream, file);
			subEmitterCollision = new PPtr<ParticleSystem>(stream, file);
			subEmitterCollision1 = new PPtr<ParticleSystem>(stream, file);
			subEmitterDeath = new PPtr<ParticleSystem>(stream, file);
			subEmitterDeath1 = new PPtr<ParticleSystem>(stream, file);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(enabled);
			writer.Write(new byte[3]);
			subEmitterBirth.WriteTo(stream);
			subEmitterBirth1.WriteTo(stream);
			subEmitterCollision.WriteTo(stream);
			subEmitterCollision1.WriteTo(stream);
			subEmitterDeath.WriteTo(stream);
			subEmitterDeath1.WriteTo(stream);
		}
	}

	public class ParticleSystem : Component, LinkedByGameObject, StoresReferences
	{
		public AssetCabinet file { get; set; }
		public int pathID { get; set; }
		public UnityClassID classID1 { get; set; }
		public UnityClassID classID2 { get; set; }

		public PPtr<GameObject> m_GameObject { get; set; }
		public float lengthInSec { get; set; }
		public float startDelay { get; set; }
		public float speed { get; set; }
		public uint randomSeed { get; set; }
		public bool looping { get; set; }
		public bool prewarm { get; set; }
		public bool playOnAwake { get; set; }
		public bool moveWithTransform { get; set; }
		public InitialModule InitialModule { get; set; }
		public ShapeModule ShapeModule { get; set; }
		public EmissionModule EmissionModule { get; set; }
		public SizeModule SizeModule { get; set; }
		public RotationModule RotationModule { get; set; }
		public ColorModule ColorModule { get; set; }
		public UVModule UVModule { get; set; }
		public VelocityModule VelocityModule { get; set; }
		public ForceModule ForceModule { get; set; }
		public ExternalForcesModule ExternalForcesModule { get; set; }
		public ClampVelocityModule ClampVelocityModule { get; set; }
		public SizeBySpeedModule SizeBySpeedModule { get; set; }
		public RotationBySpeedModule RotationBySpeedModule { get; set; }
		public ColorBySpeedModule ColorBySpeedModule { get; set; }
		public CollisionModule CollisionModule { get; set; }
		public SubModule SubModule { get; set; }

		public ParticleSystem(AssetCabinet file, int pathID, UnityClassID classID1, UnityClassID classID2)
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
			lengthInSec = reader.ReadSingle();
			startDelay = reader.ReadSingle();
			speed = reader.ReadSingle();
			randomSeed = reader.ReadUInt32();
			looping = reader.ReadBoolean();
			prewarm = reader.ReadBoolean();
			playOnAwake = reader.ReadBoolean();
			moveWithTransform = reader.ReadBoolean();
			InitialModule = new InitialModule(stream);
			ShapeModule = new ShapeModule(file, stream);
			EmissionModule = new EmissionModule(stream);
			SizeModule = new SizeModule(stream);
			RotationModule = new RotationModule(stream);
			ColorModule = new ColorModule(stream);
			UVModule = new UVModule(stream);
			VelocityModule = new VelocityModule(stream);
			ForceModule = new ForceModule(stream);
			ExternalForcesModule = new ExternalForcesModule(stream);
			ClampVelocityModule = new ClampVelocityModule(stream);
			SizeBySpeedModule = new SizeBySpeedModule(stream);
			RotationBySpeedModule = new RotationBySpeedModule(stream);
			ColorBySpeedModule = new ColorBySpeedModule(stream);
			CollisionModule = new CollisionModule(file, stream);
			SubModule = new SubModule(file, stream);
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			m_GameObject.WriteTo(stream);
			writer.Write(lengthInSec);
			writer.Write(startDelay);
			writer.Write(speed);
			writer.Write(randomSeed);
			writer.Write(looping);
			writer.Write(prewarm);
			writer.Write(playOnAwake);
			writer.Write(moveWithTransform);
			InitialModule.WriteTo(stream);
			ShapeModule.WriteTo(stream);
			EmissionModule.WriteTo(stream);
			SizeModule.WriteTo(stream);
			RotationModule.WriteTo(stream);
			ColorModule.WriteTo(stream);
			UVModule.WriteTo(stream);
			VelocityModule.WriteTo(stream);
			ForceModule.WriteTo(stream);
			ExternalForcesModule.WriteTo(stream);
			ClampVelocityModule.WriteTo(stream);
			SizeBySpeedModule.WriteTo(stream);
			RotationBySpeedModule.WriteTo(stream);
			ColorBySpeedModule.WriteTo(stream);
			CollisionModule.WriteTo(stream);
			SubModule.WriteTo(stream);
		}
	}
}
