#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.ComponentModel;
using System.Security.Cryptography;

namespace ODFPlugin
{
	#region odfFormat
	public abstract class odfFormat
	{
		private string Name { get; set; }

		public bool isEncrypted;

		protected abstract ICryptoTransform CryptoTransform();

		protected odfFormat(string name)
		{
			this.Name = name;
		}

		public List<odfFileSection> ScanFile(string odfPath, int fileSize)
		{
			FileStream fs = null;
			try
			{
				testEncryption(odfPath);
				List<odfFileSection> sectionList = new List<odfFileSection>();
				ICryptoTransform trans = this.isEncrypted ? CryptoTransform() : null;
				int addr = 12;
				int length;
				do
				{
					fs = File.OpenRead(odfPath);
					length = (int)fs.Length;
					if (trans is CryptoTransformThreeChoices)
						((CryptoTransformThreeChoices)trans).keyOffset = addr;
					fs.Seek(addr, SeekOrigin.Begin);

					odfFileSection section;
					Stream stream = this.isEncrypted ? (Stream)new CryptoStream(new BufferedStream(fs), trans, CryptoStreamMode.Read) : new BufferedStream(fs);
					using (BinaryReader reader = new BinaryReader(stream))
					{
						
						section = new odfFileSection(odfFileSection.DecryptSectionType(reader.ReadBytes(4)), odfPath);
						section.Size = reader.ReadInt32();
						section.Offset = addr + 4+4;

						addr += 4+4 + section.Size;
						if (section.Type == odfSectionType.BANM)
							addr += 264;
					}
					sectionList.Add(section);
				} while (addr < length);

				return sectionList;
			}
			catch (Exception e)
			{
				if (fs != null)
					fs.Close();
				throw e;
			}
		}

		private void testEncryption(string odfPath)
		{
			using (BinaryReader reader = new BinaryReader(File.OpenRead(odfPath)))
			{
				byte[] fileType = reader.ReadBytes(3);
				string fileTypeDecoded = Encoding.ASCII.GetString(fileType);
				this.isEncrypted = fileTypeDecoded != "ODF" && fileTypeDecoded != "ODA";
			}
		}

		public BinaryReader ReadFile(odfFileSection section, string odfPath)
		{
			int bufferSize = section.Size;
			if (bufferSize == 0)
				return null;
			FileStream fs = null;
			try
			{
				fs = File.OpenRead(odfPath);
				fs.Seek(section.Offset, SeekOrigin.Begin);
				Stream stream = null;
				if (section.Type == odfSectionType.BANM)
					bufferSize += 264;
				if (this.isEncrypted)
				{
					ICryptoTransform trans = CryptoTransform();
					if (trans is CryptoTransformThreeChoices)
						((CryptoTransformThreeChoices)trans).keyOffset = section.Offset;
					stream = new CryptoStream(new BufferedStream(fs, bufferSize), trans, CryptoStreamMode.Read);
				}
				else
					stream = new BufferedStream(fs, bufferSize);
				return new BinaryReader(stream);
			}
			catch (Exception e)
			{
				if (fs != null)
				{
					fs.Close();
				}
				throw e;
			}
		}

		public CryptoStream WriteFile(Stream stream)
		{
			return new CryptoStream(stream, CryptoTransform(), CryptoStreamMode.Write);
		}

		public override string ToString()
		{
			return Name;
		}
	}
	#endregion

	public class odfFormat_LGKLRetail : odfFormat
	{
		int fileSize;

		public odfFormat_LGKLRetail(int fileSize)
			: base("LGKL Retail")
		{
			this.fileSize = fileSize;
		}

		protected override ICryptoTransform CryptoTransform()
		{
			return new CryptoTransformThreeChoices(
				new byte[][] {
					new byte[] { 0xAA, 0x5A, 0xFF, 0x81, 0xD0 },
					new byte[] { 0xAA, 0x8F, 0xFF, 0x81, 0xD0 },
					new byte[] { 0xAA, 0x5A, 0xFF, 0xAF, 0xF0 }
				},
				fileSize % 3
			);
		}
	}

	public class imageFormat_LGKLRetail
	{
		private string Name { get; set; }
		public bool isEncrypted { get; set; }

		protected ICryptoTransform CryptoTransform()
		{
			return new CryptoTransformImage(
				new byte[] { 0xFF, 0xFF, 0xFF, 0x15, 0x50, 0x0F, 0xFF, 0xFF, 0xFF, 0xCA, 0x02, 0xFF, 0x15, 0x50, 0xFF, 0xFF }
			);
		}

		public imageFormat_LGKLRetail(bool encrypted)
		{
			Name = "LGKL Retail Image";
			isEncrypted = encrypted;
		}

		public BinaryReader ReadFile(String imagePath, ref int fileSize)
		{
			FileStream fs = null;
			try
			{
				fs = File.OpenRead(imagePath);
				fileSize = (int)fs.Length;
				Stream stream = null;
				if (isEncrypted)
				{
					ICryptoTransform trans = CryptoTransform();
					stream = new CryptoStream(new BufferedStream(fs, fileSize), trans, CryptoStreamMode.Read);
				}
				else
					stream = new BufferedStream(fs, fileSize);
				return new BinaryReader(stream);
			}
			catch (Exception e)
			{
				if (fs != null)
					fs.Close();
				throw e;
			}
		}

		public static bool testEncryption(String imagePath)
		{
			using (BinaryReader reader = new BinaryReader(File.OpenRead(imagePath)))
			{
				if (imagePath.ToLower().EndsWith(".bmp"))
				{
					byte[] buf = reader.ReadBytes(2);
					if ((buf[0] == 'B') && (buf[1] == 'M'))
					{
						return false;
					}
				}
				else if (imagePath.ToLower().EndsWith(".tga"))
				{
					byte[] buf = reader.ReadBytes(8);
					int bufSum = 0;
					for (int i = 0; i < buf.Length; i++)
					{
						bufSum += buf[i];
					}

					if ((buf[2] == 0x02 || buf[2] == 0x0A) && (bufSum == 0x02 || bufSum == 0x0A || bufSum == 0x0F || bufSum == 0x17))
					{
						return false;
					}
				}
				else
					return false;
			}
			return true;
		}

		public CryptoStream WriteFile(Stream stream)
		{
			return new CryptoStream(stream, CryptoTransform(), CryptoStreamMode.Write);
		}

		public override string ToString()
		{
			return Name;
		}
	}

	#region CryptoTransform
	public class CryptoTransformThreeChoices : ICryptoTransform
	{
		#region ICryptoTransform Members
		public bool CanReuseTransform
		{
			get { return true; }
		}

		public bool CanTransformMultipleBlocks
		{
			get { return true; }
		}

		public int InputBlockSize
		{
			get { return 1; }
		}

		public int OutputBlockSize
		{
			get { return 1; }
		}

		public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			for (int i = 0; i < inputCount; i++)
			{
				int keyAddress = outputOffset + i + keyOffset;
				outputBuffer[outputOffset + i] = (byte)(inputBuffer[inputOffset + i] ^ ((key[keyAddress % 0x05]) + keyAddress) & 0xFF);
			}
			keyOffset += inputCount;
			return inputCount;
		}

		public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
		{
			byte[] outputBuffer = new byte[inputCount];
			for (int i = 0; i < inputCount; i++)
			{
				int keyAddress = inputOffset + i + keyOffset;
				outputBuffer[i] = (byte)(inputBuffer[inputOffset + i] ^ ((key[keyAddress % 0x05]) + keyAddress) & 0xFF);
			}
			return outputBuffer;
		}
		#endregion

		#region IDisposable Members
		public void Dispose()
		{
		}
		#endregion

		private byte[] key = null;
		public int keyOffset = 0;

		public CryptoTransformThreeChoices(byte[][] codeArr, int codeIdx)
		{
			this.key = codeArr[codeIdx];
		}
	}

	public class CryptoTransformImage : ICryptoTransform
	{
		#region ICryptoTransform Members
		public bool CanReuseTransform
		{
			get { return true; }
		}

		public bool CanTransformMultipleBlocks
		{
			get { return true; }
		}

		public int InputBlockSize
		{
			get { return 1; }
		}

		public int OutputBlockSize
		{
			get { return 1; }
		}

		public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			for (int i = 0; i < inputCount; i++)
			{
				int keyAddress = outputOffset + i + keyOffset;
				outputBuffer[outputOffset + i] = (byte)(inputBuffer[inputOffset + i] ^ key[keyAddress % 0xF]);
			}
			keyOffset += inputCount;
			return inputCount;
		}

		public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
		{
			byte[] outputBuffer = new byte[inputCount];
			for (int i = 0; i < inputCount; i++)
			{
				int keyAddress = inputOffset + i + keyOffset;
				outputBuffer[i] = (byte)(inputBuffer[inputOffset + i] ^ key[keyAddress % 0xF]);
			}
			return outputBuffer;
		}
		#endregion

		#region IDisposable Members
		public void Dispose()
		{
		}
		#endregion

		private byte[] key = null;
		public int keyOffset = 0;

		public CryptoTransformImage(byte[] codeArr)
		{
			this.key = codeArr;
		}
	}
	#endregion
}