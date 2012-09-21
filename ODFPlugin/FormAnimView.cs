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
			tabControlLists.TabPages.Remove(tabPageMesh);
			tabControlLists.TabPages.Remove(tabPageMaterial);
			tabControlLists.TabPages.Remove(tabPageTexture);
			tabControlLists.TabPages.Remove(tabPageMorph);
			tabControlLists.SelectedTab = tabPageAnimation;

			tabControlViews.TabPages.Remove(tabPageBoneView);
			tabControlViews.TabPages.Remove(tabPageMeshView);
			tabControlViews.TabPages.Remove(tabPageMaterialView);
			tabControlViews.TabPages.Remove(tabPageTextureView);
		}
	}
}
