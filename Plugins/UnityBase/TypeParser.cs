using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using SB3Utility;

namespace UnityPlugin
{
	public abstract class UType
	{
		public string Name { get; set; }
		public bool NeedsAlignment { get; set; }

		public List<UType> Members = new List<UType>();

		public abstract void CopyTo(UType dest);

		public virtual void LoadFrom(Stream stream)
		{
			for (int i = 0; i < Members.Count; i++)
			{
				if (Members[i].NeedsAlignment && (stream.Position & 3) != 0)
				{
					stream.Position += 4 - (stream.Position & 3);
				}
				Members[i].LoadFrom(stream);
			}
		}

		public virtual void WriteTo(Stream stream)
		{
			for (int i = 0; i < Members.Count; i++)
			{
				if (Members[i].NeedsAlignment && (stream.Position & 3) != 0)
				{
					stream.Position += 4 - (stream.Position & 3);
				}
				Members[i].WriteTo(stream);
			}
		}
	}

	public class UClass : UType
	{
		public string ClassName;

		public UClass() { }

		public UClass(string className, string name)
		{
			ClassName = className;
			Name = name;
		}

		public UClass(UClass cls)
		{
			ClassName = cls.ClassName;
			Name = cls.Name;

			for (int i = 0; i < cls.Members.Count; i++)
			{
				Type t = cls.Members[i].GetType();
				ConstructorInfo info = t.GetConstructor(new Type[] { t });
				Members.Add((UType)info.Invoke(new object[] { cls.Members[i] }));
			}
		}

		public override void CopyTo(UType dest)
		{
			for (int i = 0; i < Members.Count; i++)
			{
				Members[i].CopyTo(((UClass)dest).Members[i]);
			}
		}

		public string GetString()
		{
			if (ClassName != "string")
			{
				return null;
			}

			Uarray arr = (Uarray)Members[0];
			UType[] Chars = arr.Value;
			byte[] str = new byte[Chars.Length];
			for (int i = 0; i < Chars.Length; i++)
			{
				str[i] = ((Uchar)Chars[i]).Value;
			}
			return UTF8Encoding.UTF8.GetString(str);
		}

		public void SetString(string str)
		{
			if (ClassName != "string")
			{
				return;
			}

			Uarray arr = (Uarray)Members[0];
			byte[] bytes = UTF8Encoding.UTF8.GetBytes(str);
			UType[] Chars;
			if (arr.Value == null || bytes.Length != arr.Value.Length)
			{
				Chars = new UType[bytes.Length];
				if (arr.Value != null)
				{
					for (int i = 0; i < Math.Min(bytes.Length, arr.Value.Length); i++)
					{
						Chars[i] = arr.Value[i];
					}
				}
				arr.Value = Chars;
			}
			else
			{
				Chars = arr.Value;
			}
			for (int i = 0; i < bytes.Length; i++)
			{
				if (Chars[i] == null)
				{
					Chars[i] = new Uchar();
				}
				((Uchar)Chars[i]).Value = bytes[i];
			}
		}
	}

	public class UPPtr : UType
	{
		public PPtr<Object> Value;
		public AssetCabinet file;
		public static Transform AnimatorRoot;

		public UPPtr()
		{
			NeedsAlignment = true;
		}

		public UPPtr(AssetCabinet file, string name) : this()
		{
			this.file = file;
			Name = name;
		}

		public UPPtr(UPPtr ptr)
		{
			Name = ptr.Name;
			this.file = ptr.file;
		}

		public override void CopyTo(UType dest)
		{
			Component asset = null;
			if (Value.asset != null)
			{
				string name = AssetCabinet.ToString(Value.asset);
				asset = ((UPPtr)dest).file.Bundle.FindComponent(name, Value.asset.classID2);
				if (asset == null)
				{
					switch (Value.asset.classID2)
					{
					case UnityClassID.GameObject:
						if (((UPPtr)dest).Value == null)
						{
							Report.ReportLog("Warning! Unset MonoBehaviour to GameObject " + name);
							break;
						}
						return;
					case UnityClassID.MonoBehaviour:
						if (((MonoBehaviour)Value.asset).m_GameObject.instance != null)
						{
							Transform trans = Operations.FindFrame(name, AnimatorRoot);
							if (trans != null)
							{
								AssetCabinet.TypeDefinition srcDef = this.file.Types.Find
								(
									delegate(AssetCabinet.TypeDefinition def)
									{
										return def.typeId == (int)((MonoBehaviour)Value.asset).classID1;
									}
								);
								bool found = false;
								var m_Component = trans.m_GameObject.instance.m_Component;
								for (int i = 0; i < m_Component.Count; i++)
								{
									if (m_Component[i].Value.asset != null && m_Component[i].Value.asset.classID2 == UnityClassID.MonoBehaviour)
									{
										AssetCabinet.TypeDefinition destDef = ((UPPtr)dest).file.Types.Find
										(
											delegate(AssetCabinet.TypeDefinition def)
											{
												return def.typeId == (int)((MonoBehaviour)m_Component[i].Value.asset).classID1;
											}
										);
										if (AssetCabinet.CompareTypes(destDef, srcDef))
										{
											asset = m_Component[i].Value.asset;
											found = true;
											break;
										}
									}
								}
								if (!found)
								{
									asset = ((MonoBehaviour)Value.asset).Clone(((UPPtr)dest).file);
									trans.m_GameObject.instance.AddLinkedComponent((LinkedByGameObject)asset);
								}
							}
							else
							{
								Report.ReportLog("Error! MonoBehaviour reference to " + name + " lost. Member " + Name);
							}
						}
						else
						{
							asset = ((MonoBehaviour)Value.asset).Clone(((UPPtr)dest).file);
						}
						break;
					case UnityClassID.MonoScript:
						asset = ((MonoScript)Value.asset).Clone(((UPPtr)dest).file);
						break;
					case UnityClassID.Transform:
						asset = Operations.FindFrame(name, AnimatorRoot);
						if (asset == null)
						{
							Report.ReportLog("Warning! Transform reference to " + name + " lost. Member " + Name);
						}
						break;
					case UnityClassID.Texture2D:
						asset = ((Texture2D)Value.asset).Clone(((UPPtr)dest).file);
						break;
					default:
						Report.ReportLog("Warning! MonoBehaviour reference to " + Value.asset.classID2 + " " + name + " unhandled. Member " + Name);
						break;
					}
				}
			}
			((UPPtr)dest).Value = new PPtr<Object>(asset);
		}

		public override void LoadFrom(Stream stream)
		{
			Value = new PPtr<Object>(stream, file);
		}

		public override void WriteTo(Stream stream)
		{
			Value.WriteTo(stream);
		}
	}

	public class Uchar : UType
	{
		public byte Value;

		public Uchar()
		{
			NeedsAlignment = false;
		}

		public Uchar(string name) : this()
		{
			Name = name;
		}

		public Uchar(Uchar c)
		{
			Name = c.Name;
		}

		public override void CopyTo(UType dest)
		{
			((Uchar)dest).Value = Value;
		}

		public override void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			Value = reader.ReadByte();
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(Value);
		}
	}

	public class Uint8 : UType
	{
		public byte Value;

		public Uint8()
		{
			NeedsAlignment = false;
		}

		public Uint8(string name) : this()
		{
			Name = name;
		}

		public Uint8(Uint8 i)
		{
			Name = i.Name;
		}

		public override void CopyTo(UType dest)
		{
			((Uint8)dest).Value = Value;
		}

		public override void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			Value = reader.ReadByte();
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(Value);
		}
	}

	public class Uint32 : UType
	{
		public int Value;

		public Uint32()
		{
			NeedsAlignment = true;
		}

		public Uint32(string name) : this()
		{
			Name = name;
		}

		public Uint32(Uint32 i)
		{
			Name = i.Name;
		}

		public override void CopyTo(UType dest)
		{
			((Uint32)dest).Value = Value;
		}

		public override void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			Value = reader.ReadInt32();
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(Value);
		}
	}

	public class Uuint32 : UType
	{
		public uint Value;

		public Uuint32()
		{
			NeedsAlignment = true;
		}

		public Uuint32(string name)
			: this()
		{
			Name = name;
		}

		public Uuint32(Uint32 i)
		{
			Name = i.Name;
		}

		public override void CopyTo(UType dest)
		{
			((Uuint32)dest).Value = Value;
		}

		public override void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			Value = reader.ReadUInt32();
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(Value);
		}
	}

	public class Ufloat : UType
	{
		public float Value;

		public Ufloat()
		{
			NeedsAlignment = true;
		}

		public Ufloat(string name) : this()
		{
			Name = name;
		}

		public Ufloat(Ufloat f)
		{
			Name = f.Name;
		}

		public override void CopyTo(UType dest)
		{
			((Ufloat)dest).Value = Value;
		}

		public override void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			Value = reader.ReadSingle();
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(Value);
		}
	}

	public class Uarray : UType
	{
		public UType[] Value;

		public Uarray() { }

		public Uarray(string name)
		{
			Name = name;
		}

		public Uarray(Uarray a)
		{
			Name = a.Name;

			for (int i = 0; i < a.Members.Count; i++)
			{
				Type t = a.Members[i].GetType();
				ConstructorInfo info = t.GetConstructor(new Type[] { t });
				Members.Add((UType)info.Invoke(new object[] { a.Members[i] }));
			}
		}

		public override void CopyTo(UType dest)
		{
			((Uarray)dest).Value = new UType[Value.Length];
			Type t = Members[1].GetType();
			ConstructorInfo info = t.GetConstructor(new Type[] { t });
			for (int i = 0; i < Value.Length; i++)
			{
				((Uarray)dest).Value[i] = (UType)info.Invoke(new object[] { dest.Members[1] });
				Value[i].CopyTo(((Uarray)dest).Value[i]);
			}
		}

		public override void LoadFrom(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);
			if ((stream.Position & 3) != 0)
			{
				stream.Position += 4 - (stream.Position & 3);
			}
			int size = reader.ReadInt32();
			Value = new UType[size];
			Type t = Members[1].GetType();
			ConstructorInfo info = t.GetConstructor(new Type[] { t });
			for (int i = 0; i < size; i++)
			{
				Value[i] = (UType)info.Invoke(new object[] { Members[1] });
				Value[i].LoadFrom(stream);
				if (Members[1].NeedsAlignment && (stream.Position & 3) != 0)
				{
					stream.Position += 4 - (stream.Position & 3);
				}
			}
		}

		public override void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			if ((stream.Position & 3) != 0)
			{
				stream.Position += 4 - (stream.Position & 3);
			}
			writer.Write(Value.Length);
			for (int i = 0; i < Value.Length; i++)
			{
				Value[i].WriteTo(stream);
				if (Members[1].NeedsAlignment && (stream.Position & 3) != 0)
				{
					stream.Position += 4 - (stream.Position & 3);
				}
			}
		}
	}

	public class TypeParser
	{
		public UClass type;
		AssetCabinet file;

		public TypeParser(AssetCabinet file, AssetCabinet.TypeDefinition typeDef)
		{
			this.file = file;
			type = GenerateType(typeDef);
		}

		UClass GenerateType(AssetCabinet.TypeDefinition typeDef)
		{
			UClass cls = new UClass(typeDef.definitions.type, typeDef.definitions.identifier);
			for (int i = 0; i < typeDef.definitions.children.Length; i++)
			{
				AssetCabinet.TypeDefinitionString memberDef = typeDef.definitions.children[i];
				UType member = GetMember(memberDef);
				cls.Members.Add(member);
			}
			return cls;
		}

		UType GetMember(AssetCabinet.TypeDefinitionString member)
		{
			switch (member.type)
			{
			case "char":
				return new Uchar(member.identifier);
			case "bool":
			case "UInt8":
				return new Uint8(member.identifier);
			case "int":
				return new Uint32(member.identifier);
			case "unsigned int":
				return new Uuint32(member.identifier);
			case "float":
				return new Ufloat(member.identifier);
			}

			UType cls;
			if (member.type.StartsWith("PPtr<") && member.type.EndsWith(">"))
			{
				cls = new UPPtr(file, member.identifier);
			}
			else if (member.type == "Array")
			{
				cls = new Uarray();
			}
			else
			{
				cls = new UClass(member.type, member.identifier);
			}
			for (int i = 0; i < member.children.Length; i++)
			{
				AssetCabinet.TypeDefinitionString memberDef = member.children[i];
				UType submember = GetMember(memberDef);
				cls.Members.Add(submember);
			}
			return cls;
		}
	}
}
