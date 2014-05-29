using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

using SB3Utility;

namespace SB3Utility
{
	public partial class FormQuickAccess : DockContent
	{
		Dictionary<DockContent, ListViewItem> ChildForms = new Dictionary<DockContent, ListViewItem>();

		public FormQuickAccess()
		{
			InitializeComponent();
			float listViewFontSize = (float)Gui.Config["ListViewFontSize"];
			if (listViewFontSize > 0)
			{
				archiveList.Font = new System.Drawing.Font(archiveList.Font.FontFamily, listViewFontSize);
				meshList.Font = new System.Drawing.Font(meshList.Font.FontFamily, listViewFontSize);
				animationList.Font = new System.Drawing.Font(animationList.Font.FontFamily, listViewFontSize);
				otherList.Font = new System.Drawing.Font(otherList.Font.FontFamily, listViewFontSize);
			}

			this.FormClosing += new FormClosingEventHandler(FormOpenedFiles_FormClosing);
		}

		private void FormOpenedFiles_FormClosing(object sender, FormClosingEventArgs e)
		{
			try
			{
				if (e.CloseReason == CloseReason.MdiFormClosing)
				{
					e.Cancel = true;
					return;
				}

				foreach (var pair in ChildForms)
				{
					pair.Key.FormClosed -= new FormClosedEventHandler(ChildForms_FormClosed);
				}
				ChildForms.Clear();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void ChildForms_FormClosed(object sender, FormClosedEventArgs e)
		{
			try
			{
				DockContent form = (DockContent)sender;
				form.FormClosed -= new FormClosedEventHandler(ChildForms_FormClosed);

				ListViewItem item;
				if (ChildForms.TryGetValue(form, out item))
				{
					item.Remove();
				}
				ChildForms.Remove(form);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		public void RegisterOpenFile(DockContent content, ContentCategory category)
		{
			if (!IsHidden && !ChildForms.ContainsKey(content))
			{
				content.FormClosed += new FormClosedEventHandler(ChildForms_FormClosed);

				ListViewItem item = new ListViewItem(content.Text);
				item.ToolTipText = content.ToolTipText;
				item.Tag = content;

				ColumnHeader hdr = null;
				switch (category)
				{
				case ContentCategory.Archives:
					hdr = archiveListHeader;
					break;
				case ContentCategory.Meshes:
					hdr = meshListHeader;
					break;
				case ContentCategory.Animations:
					hdr = animationListHeader;
					break;
				case ContentCategory.Others:
					hdr = otherListHeader;
					break;
				}
				hdr.ListView.Items.Add(item);
				hdr.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
				tabControlQuickAccess.SelectTabWithoutLoosingFocus((TabPage)hdr.ListView.Parent);
				ChildForms.Add(content, item);
			}
		}

		private void quickAccessList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			if (e.IsSelected)
			{
				((DockContent)e.Item.Tag).BringToFront();
				e.Item.Selected = false;
			}
		}
	}
}
