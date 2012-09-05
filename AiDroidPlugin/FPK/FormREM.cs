using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

using SB3Utility;

using System.Media;

namespace AiDroidPlugin
{
	[Plugin]
	[PluginOpensFile(".rem")]
	public partial class FormREM : DockContent
	{
		public string EditorVar;
		public string ParserVar;

		List<RenderObjectREM> renderObjectMeshes;
		List<int> renderObjectIds;

		private TreeNode[] prevMorphProfileNodes = null;

		private bool listViewItemSyncSelectedSent = false;

		public FormREM(string path, string variable)
		{
			InitializeComponent();

			this.ShowHint = DockState.Document;
			this.Text = Path.GetFileName(path);
			this.ToolTipText = path;

			ParserVar = Gui.Scripting.GetNextVariable("remParser");
			string parserCommand = ParserVar + " = OpenREM(path=\"" + path + "\")";
			remParser parser = (remParser)Gui.Scripting.RunScript(parserCommand);

/*			DockPanel panel = Gui.Docking.DockFiles.PanelPane.DockPanel;
			Gui.Docking.DockFiles.Hide();
			Gui.Docking.DockEditors.Show(panel, DockState.Document);*/
			Gui.Docking.ShowDockContent(this, Gui.Docking.DockEditors);

			renderObjectMeshes = new List<RenderObjectREM>(new RenderObjectREM[parser.RemFile.MESC.numMeshes]);
			renderObjectIds = new List<int>(new int[parser.RemFile.MESC.numMeshes]);

			for (int meshIdx = 0; meshIdx < parser.RemFile.MESC.numMeshes; meshIdx++)
			{
				if (renderObjectMeshes[meshIdx] == null)
				{
					remMesh mesh = parser.RemFile.MESC.meshes[meshIdx];
					renderObjectMeshes[meshIdx] = new RenderObjectREM(parser, mesh);
				}
				RenderObjectREM renderObj = renderObjectMeshes[meshIdx];
				renderObjectIds[meshIdx] = Gui.Renderer.AddRenderObject(renderObj);
			}

/*			SoundPlayer simpleSound = new SoundPlayer(@"c:\Windows\Media\chimes.wav");
			simpleSound.Play();*/
			// .wav in Yuusha : mo_00_00_15 & mo_00_00_09
			// .wav & .ogg in LG: lg_05_00_00
		}

		void CustomDispose()
		{
			try
			{
				DisposeRenderObjects();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void DisposeRenderObjects()
		{
//			foreach (ListViewItem item in listViewMesh.SelectedItems)
			for (int meshIdx = 0; meshIdx < renderObjectIds.Count; meshIdx++)
			{
				Gui.Renderer.RemoveRenderObject(renderObjectIds[meshIdx]);
			}

			for (int i = 0; i < renderObjectMeshes.Count; i++)
			{
				if (renderObjectMeshes[i] != null)
				{
					renderObjectMeshes[i].Dispose();
					renderObjectMeshes[i] = null;
				}
			}
		}
	}
}
