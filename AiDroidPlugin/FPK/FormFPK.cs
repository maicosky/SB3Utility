using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.IO;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

using SB3Utility;

namespace AiDroidPlugin
{
	[Plugin]
	[PluginOpensFile(".fpk")]
	public partial class FormFPK : DockContent
	{
		public string FormVariable { get; protected set; }
		public fpkEditor Editor { get; protected set; }
		public string EditorVar { get; protected set; }
		public string ParserVar { get; protected set; }

		List<ListView> subfileListViews = new List<ListView>();

		Dictionary<string, string> ChildParserVars = new Dictionary<string, string>();
		Dictionary<string, DockContent> ChildForms = new Dictionary<string, DockContent>();

		private Utility.SoundLib soundLib;

		public FormFPK(string path, string variable)
		{
			try
			{
				InitializeComponent();

				FormVariable = variable;

				ParserVar = Gui.Scripting.GetNextVariable("fpkParser");
				fpkParser fpkParser = (fpkParser)Gui.Scripting.RunScript(ParserVar + " = OpenFPK(path=\"" + path + "\")");

				EditorVar = Gui.Scripting.GetNextVariable("fpkEditor");
				Editor = (fpkEditor)Gui.Scripting.RunScript(EditorVar + " = fpkEditor(parser=" + ParserVar + ")");

				Text = Path.GetFileName(fpkParser.FilePath);
				ToolTipText = fpkParser.FilePath;
				ShowHint = DockState.Document;

				saveFileDialog1.Filter = ".fpk Files (*.fpk)|*.fpk|All Files (*.*)|*.*";

				subfileListViews.Add(remSubfilesList);
				subfileListViews.Add(reaSubfilesList);
				subfileListViews.Add(imageSubfilesList);
				subfileListViews.Add(soundSubfilesList);
				subfileListViews.Add(otherSubfilesList);

				InitSubfileLists();

				Gui.Docking.ShowDockContent(this, Gui.Docking.DockFiles);
				this.FormClosing += new FormClosingEventHandler(FormFPK_FormClosing);

				List<DockContent> formFPKList;
				if (Gui.Docking.DockContents.TryGetValue(typeof(FormFPK), out formFPKList))
				{
					var listCopy = new List<FormFPK>(formFPKList.Count);
					for (int i = 0; i < formFPKList.Count; i++)
					{
						listCopy.Add((FormFPK)formFPKList[i]);
					}

					foreach (var form in listCopy)
					{
						if (form != this)
						{
							var formParser = (fpkParser)Gui.Scripting.Variables[form.ParserVar];
							if (formParser.FilePath == path)
							{
								form.Close();
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void FormFPK_FormClosing(object sender, FormClosingEventArgs e)
		{
			try
			{
				foreach (ListViewItem item in soundSubfilesList.SelectedItems)
				{
					item.Selected = false;
				}

				foreach (var pair in ChildForms)
				{
					if (pair.Value.IsHidden)
					{
						pair.Value.Show();
					}

					pair.Value.FormClosing -= new FormClosingEventHandler(ChildForms_FormClosing);
					pair.Value.Close();
				}
				ChildForms.Clear();
				foreach (var parserVar in ChildParserVars.Values)
				{
					Gui.Scripting.Variables.Remove(parserVar);
				}
				ChildParserVars.Clear();
				Gui.Scripting.Variables.Remove(FormVariable);
				Gui.Scripting.Variables.Remove(EditorVar);
				Gui.Scripting.Variables.Remove(ParserVar);
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
				ChildForms.Remove((string)form.Tag);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void InitSubfileLists()
		{
			remSubfilesList.Items.Clear();
			reaSubfilesList.Items.Clear();
			imageSubfilesList.Items.Clear();
			soundSubfilesList.Items.Clear();
			otherSubfilesList.Items.Clear();

			adjustSubfileListsEnabled(false);
			List<ListViewItem> remFiles = new List<ListViewItem>(Editor.Parser.Subfiles.Count);
			List<ListViewItem> reaFiles = new List<ListViewItem>(Editor.Parser.Subfiles.Count);
			List<ListViewItem> imageFiles = new List<ListViewItem>(Editor.Parser.Subfiles.Count);
			List<ListViewItem> soundFiles = new List<ListViewItem>(Editor.Parser.Subfiles.Count);
			List<ListViewItem> otherFiles = new List<ListViewItem>(Editor.Parser.Subfiles.Count);
			for (int i = 0; i < Editor.Parser.Subfiles.Count; i++)
			{
				IWriteFile subfile = Editor.Parser.Subfiles[i];
				ListViewItem item = new ListViewItem(subfile.Name);
				item.Tag = subfile;

				string ext = Path.GetExtension(subfile.Name).ToUpper();
				if (ext.Equals(".REM"))
				{
					remFiles.Add(item);
				}
				else if (ext.Equals(".REA"))
				{
					reaFiles.Add(item);
				}
				else if (Utility.ImageSupported(ext))
				{
					imageFiles.Add(item);
				}
				else if (ext.Equals(".WAV") || ext.Equals(".OGG"))
				{
					soundFiles.Add(item);
				}
				else
				{
					otherFiles.Add(item);
				}
			}
			remSubfilesList.Items.AddRange(remFiles.ToArray());
			reaSubfilesList.Items.AddRange(reaFiles.ToArray());
			OptionalTab(imageSubfilesList, imageFiles, tabPageImageSubfiles, 2);
			OptionalTab(soundSubfilesList, soundFiles, tabPageSoundSubfiles, 3);
			otherSubfilesList.Items.AddRange(otherFiles.ToArray());
			adjustSubfileLists();
			adjustSubfileListsEnabled(true);

			if (soundSubfilesList.Items.Count > 0 && soundLib == null)
			{
				soundLib = new Utility.SoundLib();
			}
		}

		private void OptionalTab(ListView view, List<ListViewItem> newFiles, TabPage tab, int position)
		{
			if (newFiles.Count > 0)
			{
				view.Items.AddRange(newFiles.ToArray());
				if (!tabControlSubfiles.Controls.Contains(tab))
					tabControlSubfiles.Controls.Add(tab);
				tabControlSubfiles.Controls.SetChildIndex(tab, position);
			}
			else
			{
				if (tabControlSubfiles.Controls.Contains(tab))
					tabControlSubfiles.Controls.Remove(tab);
			}
		}

		private void adjustSubfileListsEnabled(bool enabled)
		{
			if (enabled)
			{
				for (int i = 0; i < subfileListViews.Count; i++)
				{
					subfileListViews[i].EndUpdate();
				}
			}
			else
			{
				for (int i = 0; i < subfileListViews.Count; i++)
				{
					subfileListViews[i].BeginUpdate();
				}
			}
		}

		private void adjustSubfileLists()
		{
			for (int i = 0; i < subfileListViews.Count; i++)
			{
				subfileListViews[i].BeginUpdate();
				subfileListViews[i].Sort();
				subfileListViews[i].AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
				subfileListViews[i].EndUpdate();

				TabPage tabPage = (TabPage)subfileListViews[i].Parent;
				int countIdx = tabPage.Text.IndexOf('[');
				if (countIdx > 0)
				{
					tabPage.Text = tabPage.Text.Substring(0, countIdx) + "[" + subfileListViews[i].Items.Count + "]";
				}
				else
				{
					tabPage.Text += " [" + subfileListViews[i].Items.Count + "]";
				}
			}
		}

		private void exportFPKToolStripMenuItem_Click(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		private void reopenToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				string opensFileVar = Gui.Scripting.GetNextVariable("opensFPK");
				Gui.Scripting.RunScript(opensFileVar + " = FormFPK(path=\"" + Editor.Parser.FilePath + "\", variable=\"" + opensFileVar + "\")", false);

				List<DockContent> formFPKList;
				if (Gui.Docking.DockContents.TryGetValue(typeof(FormFPK), out formFPKList))
				{
					var listCopy = new List<FormFPK>(formFPKList.Count);
					for (int i = 0; i < formFPKList.Count; i++)
					{
						listCopy.Add((FormFPK)formFPKList[i]);
					}

					foreach (var form in listCopy)
					{
						if (form.FormVariable == FormVariable)
						{
							form.Close();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void savefpkToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				BackgroundWorker worker = (BackgroundWorker)Gui.Scripting.RunScript(EditorVar + ".SaveFPK(keepBackup=" + keepBackupToolStripMenuItem.Checked + ", background=True)");
				ShowBlockingDialog(Editor.Parser.FilePath, worker);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void savefpkAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				if (saveFileDialog1.ShowDialog() == DialogResult.OK)
				{
					BackgroundWorker worker = (BackgroundWorker)Gui.Scripting.RunScript(EditorVar + ".SaveFPK(path=\"" + saveFileDialog1.FileName + "\", keepBackup=" + keepBackupToolStripMenuItem.Checked + ", background=True)");
					ShowBlockingDialog(saveFileDialog1.FileName, worker);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void ShowBlockingDialog(string path, BackgroundWorker worker)
		{
			using (FormPPSave blockingForm = new FormPPSave(worker))
			{
				blockingForm.Text = "Saving " + Path.GetFileName(path) + "...";
				if (blockingForm.ShowDialog() == DialogResult.OK)
				{
					Report.ReportLog("Finished saving to " + saveFileDialog1.FileName);
				}
			}
		}

		private void closeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				Close();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void exportSubfilesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		private void addFilesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			throw new NotImplementedException();
			try
			{
				if (openFileDialog1.ShowDialog() == DialogResult.OK)
				{
					foreach (string path in openFileDialog1.FileNames)
					{
//						Gui.Scripting.RunScript(EditorVar + ".AddSubfile(path=\"" + path + "\", replace=True)");
					}

					InitSubfileLists();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void removeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		private void renameToolStripMenuItem_Click(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		private void remSubfilesList_DoubleClick(object sender, EventArgs e)
		{
			try
			{
				OpenREMSubfilesList();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void remSubfilesList_KeyPress(object sender, KeyPressEventArgs e)
		{
			try
			{
				if (e.KeyChar == '\r')
				{
					OpenREMSubfilesList();
					e.Handled = true;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		public List<FormREM> OpenREMSubfilesList()
		{
			List<FormREM> list = new List<FormREM>(remSubfilesList.SelectedItems.Count);
			foreach (ListViewItem item in remSubfilesList.SelectedItems)
			{
				IWriteFile writeFile = (IWriteFile)item.Tag;
				FormREM formREM = (FormREM)Gui.Scripting.RunScript(FormVariable + ".OpenREMSubfile(name=\"" + writeFile.Name + "\")", false);
				formREM.Activate();
				list.Add(formREM);
			}
			return list;
		}

		[Plugin]
		public FormREM OpenREMSubfile(string name)
		{
			DockContent child;
			if (!ChildForms.TryGetValue(name, out child))
			{
				string childParserVar;
				if (!ChildParserVars.TryGetValue(name, out childParserVar))
				{
					childParserVar = Gui.Scripting.GetNextVariable("remParser");
					Gui.Scripting.RunScript(childParserVar + " = OpenREM(parser=" + ParserVar + ", name=\"" + name + "\")");
					Gui.Scripting.RunScript(EditorVar + ".ReplaceSubfile(file=" + childParserVar + ")");
					ChildParserVars.Add(name, childParserVar);

					foreach (ListViewItem item in remSubfilesList.Items)
					{
						if (((IWriteFile)item.Tag).Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
						{
							item.Font = new Font(item.Font, FontStyle.Bold);
							remSubfilesList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
							break;
						}
					}
				}

				child = new FormREM(Editor.Parser, childParserVar);
				child.FormClosing += new FormClosingEventHandler(ChildForms_FormClosing);
				child.Tag = name;
				ChildForms.Add(name, child);
			}

			return child as FormREM;
		}

		private void imageSubfilesList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (e.IsSelected)
				{
					IReadFile subfile = (IReadFile)e.Item.Tag;
					ImportedTexture image;
					string stream = EditorVar + ".ReadSubfile(name=\"" + subfile.Name + "\")";

					image = (ImportedTexture)Gui.Scripting.RunScript(Gui.ImageControl.ImageScriptVariable + " = ImportTexture(stream=" + stream + ", name=\"" + subfile.Name + "\")");
					Gui.ImageControl.Image = image;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void soundSubfilesList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (!soundLib.isLoaded())
					return;
				if (e.IsSelected)
				{
					IReadFile subfile = (IReadFile)e.Item.Tag;
					Stream stream = (Stream)Gui.Scripting.RunScript(EditorVar + ".ReadSubfile(name=\"" + subfile.Name + "\")", false);
					byte[] soundBuf;
					using (BinaryReader reader = new BinaryReader(stream))
					{
						soundBuf = reader.ReadToEnd();
					}
					soundLib.Play(e.Item.Text, soundBuf);
				}
				else
				{
					soundLib.Stop(e.Item.Text);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}
	}
}
