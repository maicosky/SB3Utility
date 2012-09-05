using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SB3Utility;

namespace ODFPlugin
{
	[Plugin]
	[PluginOpensFile(".oda")]
	class FormAnimView : FormMeshView
	{
		public FormAnimView(string path, string variable)
			: base(path, variable)
		{
			tabControlLists.Controls.Remove(tabPageMesh);
			tabControlLists.Controls.Remove(tabPageMaterial);
			tabControlLists.Controls.Remove(tabPageTexture);
			tabControlLists.Controls.Remove(tabPageMorph);
			tabControlLists.SelectedTab = tabPageAnimation;
		}
	}
}
