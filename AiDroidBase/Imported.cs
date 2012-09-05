using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using SB3Utility;

namespace AiDroidPlugin
{
	public class ImportedTexture
	{
		public string Name { get; set; }
		public string TextureFile { get; set; }
		public byte[] Data { get; set; }

		public ImportedTexture()
		{
		}

		public ImportedTexture(string path)
		{
			Name = TextureFile = Path.GetFileName(path);
			FileStream fs = null;
			try
			{
				fs = File.OpenRead(path);
				int fileSize = (int)fs.Length;
				using (BinaryReader reader = new BinaryReader(new BufferedStream(fs, fileSize)))
				{
					Data = reader.ReadBytes(fileSize);
				}
			}
			catch (Exception e)
			{
				if (fs != null)
					fs.Close();
				throw e;
			}
		}
	}
}
