using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SB3Utility;

namespace AiDroidPlugin
{
	public static partial class Plugins
	{
		[Plugin]
		public static remParser OpenREM([DefaultVar]string path)
		{
			return new remParser(path);
		}

		[Plugin]
		public static void WriteREM([DefaultVar]remParser parser)
		{
//			parser.WriteArchive(parser.REMPath, true);
		}
	}
}
