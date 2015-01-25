using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

using SB3Utility;

namespace UnityPlugin
{
	[Plugin]
	[PluginOpensFile(".unity3d")]
	public partial class FormUnity3d : DockContent, EditedContent
	{
		public string FormVariable { get; protected set; }
		public Unity3dEditor Editor { get; protected set; }
		public string EditorVar { get; protected set; }
		public string ParserVar { get; protected set; }

		List<ListView> assetListViews = new List<ListView>();

		Dictionary<string, string> ChildParserVars = new Dictionary<string, string>();
		Dictionary<string, DockContent> ChildForms = new Dictionary<string, DockContent>();

		//private Utility.SoundLib soundLib;

		private const Keys MASS_DESTRUCTION_KEY_COMBINATION = Keys.Delete | Keys.Shift;

		public FormUnity3d(string path, string variable)
		{
			List<DockContent> formUnity3dList;
			if (Gui.Docking.DockContents.TryGetValue(typeof(FormUnity3d), out formUnity3dList))
			{
				var listCopy = new List<FormUnity3d>(formUnity3dList.Count);
				for (int i = 0; i < formUnity3dList.Count; i++)
				{
					listCopy.Add((FormUnity3d)formUnity3dList[i]);
				}

				foreach (var form in listCopy)
				{
					if (form != this)
					{
						var formParser = (UnityParser)Gui.Scripting.Variables[form.ParserVar];
						if (formParser.FilePath == path)
						{
							form.Close();
							if (!form.IsDisposed)
							{
								throw new Exception("Loading " + path + " another time cancelled.");
							}
						}
					}
				}
			}

			try
			{
				InitializeComponent();
				float listViewFontSize = (float)Gui.Config["ListViewFontSize"];
				if (listViewFontSize > 0)
				{
					animatorsList.Font = new Font(animatorsList.Font.FontFamily, listViewFontSize);
					materialsList.Font = new Font(materialsList.Font.FontFamily, listViewFontSize);
					imagesList.Font = new Font(imagesList.Font.FontFamily, listViewFontSize);
					soundsList.Font = new Font(soundsList.Font.FontFamily, listViewFontSize);
					othersList.Font = new Font(othersList.Font.FontFamily, listViewFontSize);
					filteredList.Font = new Font(filteredList.Font.FontFamily, listViewFontSize);
				}

				FormVariable = variable;

				ParserVar = Gui.Scripting.GetNextVariable("unityParser");
				EditorVar = Gui.Scripting.GetNextVariable("unityEditor");

				UnityParser uParser = (UnityParser)Gui.Scripting.RunScript(ParserVar + " = OpenUnity3d(path=\"" + path + "\")");
				Editor = (Unity3dEditor)Gui.Scripting.RunScript(EditorVar + " = Unity3dEditor(parser=" + ParserVar + ")");

				Text = Path.GetFileName(uParser.FilePath);
				ToolTipText = uParser.FilePath;
				ShowHint = DockState.Document;

				saveFileDialog1.Filter = ".unity3d Files (*.unity3d)|*.unity3d|All Files (*.*)|*.*";

				assetListViews.Add(animatorsList);
				assetListViews.Add(materialsList);
				assetListViews.Add(imagesList);
				assetListViews.Add(soundsList);
				assetListViews.Add(othersList);
				if (filterIncludedAssetsToolStripMenuItem.Checked)
				{
					tabControlAssets.TabPages.Remove(tabPageFiltered);
				}
				else
				{
					assetListViews.Add(filteredList);
				}

				InitSubfileLists(true);

				this.FormClosing += new FormClosingEventHandler(FormUnity_FormClosing);
				Gui.Docking.ShowDockContent(this, Gui.Docking.DockFiles, ContentCategory.Archives);

				keepBackupToolStripMenuItem.Checked = (bool)Properties.Settings.Default["KeepBackupOfUnity3d"];
				keepBackupToolStripMenuItem.CheckedChanged += keepBackupToolStripMenuItem_CheckedChanged;
				backupExtensionToolStripEditTextBox.Text = (string)Properties.Settings.Default["BackupExtensionUnity3d"];
				backupExtensionToolStripEditTextBox.AfterEditTextChanged += backupExtensionToolStripEditTextBox_AfterEditTextChanged;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		public bool Changed
		{
			get { return Editor.Changed; }

			set
			{
				if (value)
				{
					if (!Text.EndsWith("*"))
					{
						Text += "*";
					}
				}
				else if (Text.EndsWith("*"))
				{
					Text = Path.GetFileName(ToolTipText);
				}
				Editor.Changed = value;
			}
		}

		private void FormUnity_FormClosing(object sender, FormClosingEventArgs e)
		{
			try
			{
				if (e.CloseReason == CloseReason.MdiFormClosing)
				{
					e.Cancel = true;
					return;
				}

				if (e.CloseReason != CloseReason.TaskManagerClosing && e.CloseReason != CloseReason.WindowsShutDown)
				{
					bool confirm = Changed;
					if (!Changed)
					{
						foreach (DockContent childForm in ChildForms.Values)
						{
							if (childForm is EditedContent && ((EditedContent)childForm).Changed)
							{
								confirm = true;
								break;
							}
						}
					}
					if (confirm)
					{
						BringToFront();
						if (MessageBox.Show("Confirm to close the unit3d file and lose all changes.", "Close " + Text + " ?", MessageBoxButtons.OKCancel) != DialogResult.OK)
						{
							e.Cancel = true;
							return;
						}
					}
				}

				foreach (ListViewItem item in soundsList.SelectedItems)
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

		private void InitSubfileLists(bool opening)
		{
			adjustSubfileListsEnabled(false);
			int[] selectedAnimators = new int[animatorsList.SelectedIndices.Count];
			animatorsList.SelectedIndices.CopyTo(selectedAnimators, 0);
			animatorsList.Items.Clear();
			int[] selectedMaterials = new int[materialsList.SelectedIndices.Count];
			materialsList.SelectedIndices.CopyTo(selectedMaterials, 0);
			materialsList.Items.Clear();
			int[] selectedImg = new int[imagesList.SelectedIndices.Count];
			imagesList.SelectedIndices.CopyTo(selectedImg, 0);
			imagesList.Items.Clear();
			int[] selectedSounds = new int[soundsList.SelectedIndices.Count];
			soundsList.SelectedIndices.CopyTo(selectedSounds, 0);
			soundsList.Items.Clear();
			int[] selectedOthers = new int[othersList.SelectedIndices.Count];
			othersList.SelectedIndices.CopyTo(selectedOthers, 0);
			othersList.Items.Clear();
			if (!filterIncludedAssetsToolStripMenuItem.Checked)
			{
				filteredList.Items.Clear();
			}

			List<ListViewItem> animators = new List<ListViewItem>(Editor.Parser.Cabinet.Components.Count);
			List<ListViewItem> materials = new List<ListViewItem>(Editor.Parser.Cabinet.Components.Count);
			List<ListViewItem> images = new List<ListViewItem>(Editor.Parser.Cabinet.Components.Count);
			List<ListViewItem> sounds = new List<ListViewItem>(Editor.Parser.Cabinet.Components.Count);
			List<ListViewItem> others = new List<ListViewItem>(Editor.Parser.Cabinet.Components.Count);
			List<ListViewItem> filtered = new List<ListViewItem>(Editor.Parser.Cabinet.Components.Count);
			Font bold = new Font(animatorsList.Font, FontStyle.Bold);
			string[] assetNames = Editor.GetAssetNames(filterIncludedAssetsToolStripMenuItem.Checked);
			for (int i = 0; i < Editor.Parser.Cabinet.Components.Count; i++)
			{
				Component subfile = Editor.Parser.Cabinet.Components[i];
				if (filterIncludedAssetsToolStripMenuItem.Checked)
				{
					switch (subfile.classID1)
					{
					case UnityClassID.Avatar:
					case UnityClassID.Mesh:
					case UnityClassID.SkinnedMeshRenderer:
					case UnityClassID.Transform:
					case UnityClassID.GameObject:
						continue;
					}
				}
				ListViewItem item = new ListViewItem(new string[] { assetNames[i], subfile.classID2.ToString() });
				item.Tag = subfile;
				if (!(subfile is NotLoaded))
				{
					item.Font = new Font(animatorsList.Font, /*subfile is ppSwapfile || subfile is RawFile ? FontStyle.Italic :*/ FontStyle.Bold);
				}
				int itemWidth = (int)Math.Ceiling(Graphics.FromHwnd(Handle).MeasureString(item.Text, bold).Width) + 16;

				switch (subfile.classID1)
				{
				case UnityClassID.Animator:
					animators.Add(item);
					if (itemWidth > animatorsListHeader.Width)
					{
						animatorsListHeader.Width = itemWidth;
					}
					break;
				case UnityClassID.Material:
				case UnityClassID.Shader:
					materials.Add(item);
					if (itemWidth > materialsListHeaderName.Width)
					{
						materialsListHeaderName.Width = itemWidth;
					}
					break;
				case UnityClassID.Texture2D:
					images.Add(item);
					if (itemWidth > imagesListHeaderName.Width)
					{
						imagesListHeaderName.Width = itemWidth;
					}
					break;
				case UnityClassID.AudioClip:
				case UnityClassID.AudioSource:
					sounds.Add(item);
					if (itemWidth > soundsListHeaderName.Width)
					{
						soundsListHeaderName.Width = itemWidth;
					}
					break;
				case UnityClassID.Avatar:
				case UnityClassID.Mesh:
				case UnityClassID.SkinnedMeshRenderer:
				case UnityClassID.Transform:
				case UnityClassID.GameObject:
					filtered.Add(item);
					break;
				default:
					if (subfile.classID1 != (UnityClassID)(int)-1 || subfile.classID2 != UnityClassID.MonoBehaviour)
					{
						item.BackColor = Color.LightCoral;
					}
					others.Add(item);
					if (itemWidth > othersListHeaderNamePathID.Width)
					{
						othersListHeaderNamePathID.Width = itemWidth;
					}
					break;
				}
			}
			animatorsList.Items.AddRange(animators.ToArray());
			materialsList.Items.AddRange(materials.ToArray());
			imagesList.Items.AddRange(images.ToArray());
			soundsList.Items.AddRange(sounds.ToArray());
			othersList.Items.AddRange(others.ToArray());
			if (!filterIncludedAssetsToolStripMenuItem.Checked)
			{
				filteredList.Items.AddRange(filtered.ToArray());
			}
			adjustSubfileListsEnabled(true);
			adjustSubfileLists(opening);
			ReselectItems(animatorsList, selectedAnimators);
			ReselectItems(materialsList, selectedMaterials);
			ReselectItems(imagesList, selectedImg);
			ReselectItems(soundsList, selectedSounds);
			ReselectItems(othersList, selectedOthers);

			/*if (soundsList.Items.Count > 0 && soundLib == null)
			{
				soundLib = new Utility.SoundLib();
			}*/
		}

		private void ReselectItems(ListView subfiles, int[] selectedSubfiles)
		{
			foreach (int i in selectedSubfiles)
			{
				if (i < subfiles.Items.Count)
				{
					subfiles.Items[i].Selected = true;
					subfiles.Items[i].EnsureVisible();
				}
			}
		}

		private void adjustSubfileListsEnabled(bool enabled)
		{
			if (enabled)
			{
				for (int i = 0; i < assetListViews.Count; i++)
				{
					assetListViews[i].EndUpdate();
					assetListViews[i].Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
					if (assetListViews[i].Columns.Count > 1)
					{
						assetListViews[i].Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
					}
					assetListViews[i].Sort();
				}

			}
			else
			{
				for (int i = 0; i < assetListViews.Count; i++)
				{
					assetListViews[i].BeginUpdate();
				}
			}
		}

		private void adjustSubfileLists(bool opening)
		{
			bool first = true;
			for (int i = 0; i < assetListViews.Count; i++)
			{
				TabPage tabPage = (TabPage)assetListViews[i].Parent;
				int countIdx = tabPage.Text.IndexOf('[');
				if (countIdx > 0)
				{
					tabPage.Text = tabPage.Text.Substring(0, countIdx) + "[" + assetListViews[i].Items.Count + "]";
				}
				else
				{
					tabPage.Text += " [" + assetListViews[i].Items.Count + "]";
				}
				if (opening && assetListViews[i].Items.Count > 0 && first)
				{
					tabControlAssets.SelectTabWithoutLoosingFocus(tabPage);
					first = false;
				}
			}
		}

		private void animatorsList_DoubleClick(object sender, EventArgs e)
		{
			try
			{
				OpenAnimatorsList();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void animatorsList_KeyPress(object sender, KeyPressEventArgs e)
		{
			try
			{
				if (e.KeyChar == '\r')
				{
					OpenAnimatorsList();
					e.Handled = true;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void animatorsList_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION)
			{
				return;
			}
			//removeToolStripMenuItem_Click(sender, e);
		}

		public List<FormAnimator> OpenAnimatorsList()
		{
			List<FormAnimator> list = new List<FormAnimator>(animatorsList.SelectedItems.Count);
			foreach (ListViewItem item in animatorsList.SelectedItems)
			{
				FormAnimator formAnimator = (FormAnimator)Gui.Scripting.RunScript(FormVariable + ".OpenAnimator(name=\"" + item.Text + "\")", false);
				formAnimator.Activate();
				list.Add(formAnimator);
			}
			return list;
		}

		[Plugin]
		public FormAnimator OpenAnimator(string name)
		{
			DockContent child;
			if (!ChildForms.TryGetValue(name, out child))
			{
				string childParserVar;
				bool changed = true;
				if (!ChildParserVars.TryGetValue(name, out childParserVar))
				{
					childParserVar = Gui.Scripting.GetNextVariable("animator");
					Gui.Scripting.RunScript(childParserVar + " = OpenAnimator(parser=" + ParserVar + ", name=\"" + name + "\")");
					ChildParserVars.Add(name, childParserVar);

					foreach (ListViewItem item in animatorsList.Items)
					{
						if (item.Text.Equals(name, StringComparison.InvariantCultureIgnoreCase))
						{
							item.Font = new Font(item.Font, FontStyle.Bold);
							changed = false;// item.Tag is ppSwapfile;
							item.Tag = Gui.Scripting.Variables[childParserVar];
							break;
						}
					}
					animatorsList.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
				}

				child = new FormAnimator(Editor.Parser, childParserVar);
				((FormAnimator)child).Changed = changed;
				child.FormClosing += new FormClosingEventHandler(ChildForms_FormClosing);
				child.Tag = name;
				ChildForms.Add(name, child);
			}

			return child as FormAnimator;
		}

		private void ChildForms_FormClosing(object sender, FormClosingEventArgs e)
		{
			try
			{
				DockContent form = (DockContent)sender;
				form.FormClosing -= new FormClosingEventHandler(ChildForms_FormClosing);
				ChildForms.Remove((string)form.Tag);

				string parserVar = null;
				if (form is FormAnimator)
				{
					FormAnimator formAnimator = (FormAnimator)form;
					parserVar = formAnimator.ParserVar;
				}
				/*else if (form is FormXA)
				{
					FormXA formXA = (FormXA)form;
					parserVar = formXA.ParserVar;
				}
				else if (form is FormLST)
				{
					FormLST formLST = (FormLST)form;
					parserVar = formLST.ParserVar;
				}*/

				bool dontSwap = false;
				if (form is EditedContent)
				{
					EditedContent editorForm = (EditedContent)form;
					if (!editorForm.Changed)
					{
						/*using (FileStream stream = File.OpenRead(Editor.Parser.FilePath))
						{
							List<IWriteFile> headerFromFile = Editor.Parser.Format.ppHeader.ReadHeader(stream, Editor.Parser.Format);
							foreach (ppSubfile subfile in headerFromFile)
							{
								if (subfile.Name == (string)form.Tag)
								{
									headerFromFile.Remove(subfile);
									int subfileIdx = Editor.FindSubfile(subfile.Name);
									if (Editor.Parser.Subfiles[subfileIdx] == Gui.Scripting.Variables[parserVar])
									{
										Editor.ReplaceSubfile(subfile);
									}

									ChildParserVars.Remove((string)form.Tag);
									Gui.Scripting.RunScript(parserVar + "=null");
									InitSubfileLists(false);
									dontSwap = true;
									break;
								}
							}
						}*/
					}
				}

				if (!dontSwap)
				{
					/*System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
					long privateMemMB = currentProcess.PrivateMemorySize64 / 1024 / 1024;
					if (privateMemMB >= (long)Gui.Config["PrivateMemSwapThresholdMB"])
					{
						string swapfileVar = Gui.Scripting.GetNextVariable("swapfile");
						Gui.Scripting.RunScript(swapfileVar + " = OpenSwapfile(ppParser=" + ParserVar + ", parserToSwap=" + parserVar + ")");
						Gui.Scripting.RunScript(EditorVar + ".ReplaceSubfile(file=" + swapfileVar + ")");
						ChildParserVars.Remove((string)form.Tag);
						Gui.Scripting.RunScript(swapfileVar + "=null");
						Gui.Scripting.RunScript(parserVar + "=null");
						InitSubfileLists(false);
					}*/
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void imagesList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (e.IsSelected)
				{
					Texture2D tex = Editor.Parser.GetTexture(e.Item.Text);
					using (MemoryStream mem = new MemoryStream())
					{
						tex.Export(mem);
						mem.Position = 0;
						ImportedTexture image = new ImportedTexture(mem, tex.m_Name);
						Gui.ImageControl.Image = image;
					}
				}
			}
			catch (Exception ex)
			{
				Report.ReportLog(ex.ToString());
			}
		}

		private void imagesList_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION)
			{
				return;
			}
			//removeToolStripMenuItem_Click(sender, e);
		}

		private void saveUnity3dToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				BackgroundWorker worker = (BackgroundWorker)Gui.Scripting.RunScript(EditorVar + ".SaveUnity3d(keepBackup=" + keepBackupToolStripMenuItem.Checked + ", backupExtension=\"" + (string)Properties.Settings.Default["BackupExtensionUnity3d"] + "\", background=True)");
				ShowBlockingDialog(Editor.Parser.FilePath, worker);
				ClearChanges();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void saveUnity3dAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				if (saveFileDialog1.ShowDialog() == DialogResult.OK)
				{
					BackgroundWorker worker = (BackgroundWorker)Gui.Scripting.RunScript(EditorVar + ".SaveUnity3d(path=\"" + saveFileDialog1.FileName + "\", keepBackup=" + keepBackupToolStripMenuItem.Checked + ", backupExtension=\"" + (string)Properties.Settings.Default["BackupExtensionUnity3d"] + "\", background=True)");
					ShowBlockingDialog(saveFileDialog1.FileName, worker);
					Text = Path.GetFileName(saveFileDialog1.FileName);
					ToolTipText = Editor.Parser.FilePath;
					ClearChanges();
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

		void ClearChanges()
		{
			/*using (FileStream stream = File.OpenRead(Editor.Parser.FilePath))
			{
				List<IWriteFile> headerFromFile = Editor.Parser.Format.ppHeader.ReadHeader(stream, Editor.Parser.Format);
				List<IWriteFile> swapped = new List<IWriteFile>(Editor.Parser.Subfiles.Count);
				foreach (IWriteFile unit in Editor.Parser.Subfiles)
				{
					if (unit is ppSwapfile || unit is RawFile)
					{
						swapped.Add(unit);
					}
					else if (ChildForms.ContainsKey(unit.Name))
					{
						var editorForm = ChildForms[unit.Name] as EditedContent;
						if (editorForm != null)
						{
							editorForm.Changed = false;
						}
					}
					else if (!(unit is ppSubfile))
					{
						swapped.Add(unit);
						string parserVar = null;
						if (ChildParserVars.TryGetValue(unit.Name, out parserVar))
						{
							ChildParserVars.Remove(unit.Name);
							Gui.Scripting.RunScript(parserVar + "=null");
						}
					}
				}
				if (swapped.Count > 0)
				{
					foreach (IWriteFile swapfile in swapped)
					{
						foreach (ppSubfile subfile in headerFromFile)
						{
							if (subfile.Name == swapfile.Name)
							{
								headerFromFile.Remove(subfile);
								Editor.ReplaceSubfile(subfile);
								break;
							}
						}
					}
					InitSubfileLists(false);
				}
			}*/
			Changed = false;
		}

		private void reopenToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				string opensFileVar = Gui.Scripting.GetNextVariable("opensUnity");
				Gui.Scripting.RunScript(opensFileVar + " = FormUnity3d(path=\"" + Editor.Parser.FilePath + "\", variable=\"" + opensFileVar + "\")", false);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
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

		private void keepBackupToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
		{
			Properties.Settings.Default["KeepBackupOfUnity3d"] = keepBackupToolStripMenuItem.Checked;
		}

		private void backupExtensionToolStripEditTextBox_AfterEditTextChanged(object sender, EventArgs e)
		{
			Properties.Settings.Default["BackupExtensionUnity3d"] = backupExtensionToolStripEditTextBox.Text;
		}
	}
}
