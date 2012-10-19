using System;
using System.Collections.Generic;
using System.Text;

using SB3Utility;

namespace AiDroidPlugin
{
	[Plugin]
	public class remEditor : IDisposable
	{
		public List<string> Textures { get; protected set; }

		public remParser Parser { get; protected set; }

		public remEditor(remParser parser)
		{
			Parser = parser;

			Textures = new List<string>(parser.RemFile.MATC.numMats);
			foreach (remMaterial mat in parser.RemFile.MATC.materials)
			{
				if (mat.texture != null)
					Textures.Add(mat.texture);
			}
		}

		public void Dispose()
		{
			Textures.Clear();
			Parser = null;
		}
	}
}
