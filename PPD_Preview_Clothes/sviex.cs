using System;
using System.Collections.Generic;
using System.Text;

using SB3Utility;

namespace PPD_Preview_Clothes
{
	public static partial class PluginsPPD
	{
		[Plugin]
		public static sviexParser OpenSVIEX([DefaultVar]ppParser parser, string name)
		{
			for (int i = 0; i < parser.Subfiles.Count; i++)
			{
				if (parser.Subfiles[i].Name == name)
				{
					IReadFile subfile = parser.Subfiles[i] as IReadFile;
					if (subfile != null)
					{
						return new sviexParser(subfile.CreateReadStream(), subfile.Name);
					}
					if (parser.Subfiles[i] is sviexParser)
					{
						return (sviexParser)parser.Subfiles[i];
					}

					break;
				}
			}
			return null;
		}
	}
}
