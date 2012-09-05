using System;
using System.Collections.Generic;
using System.IO;

using SB3Utility;
using ODFPlugin;

namespace ODFPluginOld
{
	public static partial class Plugins
	{
		[Plugin]
		public static void ExportODFtoFbx([DefaultVar]odfParser parser, object[] meshNames, string path, string exportFormat, bool allFrames, bool skins, bool _8dot3)
		{
			List<string> meshStrList = new List<string>(Utility.Convert<string>(meshNames));
			List<odfMesh> meshes = new List<odfMesh>(meshStrList.Count);
			foreach (string meshName in meshStrList)
			{
				odfMesh mesh = odf.FindMeshListSome(meshName, parser.MeshSection);
				if (mesh != null)
				{
					meshes.Add(mesh);
				}
				else
					Report.ReportLog("Mesh " + meshName + " not found.");
			}
			Fbx.Exporter.Export(path, parser, meshes, exportFormat, allFrames, skins, _8dot3);
		}

		[Plugin]
		public static void ExportMorphFbx([DefaultVar]odfParser parser, string path, odfMesh mesh, odfMorphClip morphClip, string exportFormat)
		{
//			Fbx.Exporter.ExportMorph(path, xxparser, meshFrame, morphClip, xaparser, exportFormat);
		}

		[Plugin]
		public static Fbx.Importer ImportFbxAsODF([DefaultVar]string path)
		{
			ICanImport importer = Fbx.ImporterBase.TestFormat(path);
			if (importer == null)
			{
				Report.ReportLog("Fbx doesn't include an ODF layer!");
				return null;
			}
			importer.Import();
			return importer as Fbx.Importer;
		}
	}
}
