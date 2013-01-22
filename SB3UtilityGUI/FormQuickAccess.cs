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

			this.FormClosing += new FormClosingEventHandler(FormOpenedFiles_FormClosing);
		}

		private void FormOpenedFiles_FormClosing(object sender, FormClosingEventArgs e)
		{
			try
			{
				foreach (var pair in ChildForms)
				{
					pair.Key.FormClosing -= new FormClosingEventHandler(ChildForms_FormClosing);
				}
				ChildForms.Clear();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void ChildForms_FormClosing(object sender, FormClosingEventArgs e)
		{
			try
			{
				DockContent form = (DockContent)sender;
				form.FormClosing -= new FormClosingEventHandler(ChildForms_FormClosing);

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
				content.FormClosing += new FormClosingEventHandler(ChildForms_FormClosing);

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
