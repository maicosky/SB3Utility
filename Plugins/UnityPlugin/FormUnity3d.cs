using System;
using System.Collections;
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
	[PluginOpensFile(".assets")]
	[PluginOpensFile(".")]
	public partial class FormUnity3d : DockContent, EditedContent
	{
		public string FormVariable { get; protected set; }
		public Unity3dEditor Editor { get; protected set; }
		public string EditorVar { get; protected set; }
		public string ParserVar { get; protected set; }

		private bool propertiesChanged = false;

		List<ListView> assetListViews = new List<ListView>();
		ListViewItemComparer listViewItemComparer = new ListViewItemComparer();

		Dictionary<string, string> ChildParserVars = new Dictionary<string, string>();
		Dictionary<string, DockContent> ChildForms = new Dictionary<string, DockContent>();

		private Utility.SoundLib soundLib;

		private const Keys MASS_DESTRUCTION_KEY_COMBINATION = Keys.Delete | Keys.Shift;

		private HashSet<string> RemovedMods = new HashSet<string>();

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

				saveFileDialog1.Filter = ".unity3d Files (*.unity3d;*.assets)|*.unity3d;*.assets|All Files (*.*)|*.*";
				saveFileDialog1.InitialDirectory = Path.GetDirectoryName(uParser.FilePath);
				int dotPos = uParser.FilePath.LastIndexOf('.');
				if (dotPos > 0)
				{
					saveFileDialog1.DefaultExt = uParser.FilePath.Substring(dotPos + 1);
				}

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
				backupExtension1ToolStripEditTextBox.Text = (string)Properties.Settings.Default["BackupExtensionUnity3d"];
				backupExtension1ToolStripEditTextBox.AfterEditTextChanged += backupExtensionToolStripEditTextBox_AfterEditTextChanged;
				backupExtension2ToolStripEditTextBox.Text = (string)Properties.Settings.Default["BackupExtensionAssets"];
				backupExtension2ToolStripEditTextBox.AfterEditTextChanged += backupExtensionToolStripEditTextBox_AfterEditTextChanged;
				backupExtension3ToolStripEditTextBox.Text = (string)Properties.Settings.Default["BackupExtensionNone"];
				backupExtension3ToolStripEditTextBox.AfterEditTextChanged += backupExtensionToolStripEditTextBox_AfterEditTextChanged;

				Properties.Settings.Default.SettingChanging += Default_SettingChanging;

				if (Animator.ArgToHashExecutable == null)
				{
					Animator.ArgToHashExecutable = (string)Properties.Settings.Default["ArgToHashExecutable"];
					if (Animator.ArgToHashExecutable.StartsWith("."))
					{
						Animator.ArgToHashExecutable = System.Environment.CurrentDirectory + Animator.ArgToHashExecutable.Substring(1);
					}
					Animator.ArgToHashArgs = (string)Properties.Settings.Default["ArgToHashArguments"];
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void Default_SettingChanging(object sender, System.Configuration.SettingChangingEventArgs e)
		{
			propertiesChanged = true;
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

				if (propertiesChanged)
				{
					Properties.Settings.Default.Save();
				}

				List<DockContent> formUnity3dList;
				if (Gui.Docking.DockContents.TryGetValue(typeof(FormUnity3d), out formUnity3dList) && formUnity3dList.Count == 1)
				{
					Animator.TerminateArgToHash();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		public void InitSubfileLists(bool opening)
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
			string[] assetNames = (string[])Gui.Scripting.RunScript(EditorVar + ".GetAssetNames(filter=" + filterIncludedAssetsToolStripMenuItem.Checked + ")");
			for (int i = 0; i < Editor.Parser.Cabinet.Components.Count; i++)
			{
				Component subfile = Editor.Parser.Cabinet.Components[i];
				if (filterIncludedAssetsToolStripMenuItem.Checked)
				{
					switch (subfile.classID1)
					{
					case UnityClassID.AudioSource:
					case UnityClassID.AudioListener:
					case UnityClassID.Avatar:
					case UnityClassID.Camera:
					case UnityClassID.CapsuleCollider:
					case UnityClassID.FlareLayer:
					case UnityClassID.LinkToGameObject:
					case UnityClassID.LinkToGameObject223:
					case UnityClassID.LinkToGameObject225:
					case UnityClassID.Mesh:
					case UnityClassID.MeshCollider:
					case UnityClassID.MeshFilter:
					case UnityClassID.MeshRenderer:
					case UnityClassID.MultiLink:
					case UnityClassID.Projector:
					case UnityClassID.Rigidbody:
					case UnityClassID.SkinnedMeshRenderer:
					case UnityClassID.SphereCollider:
					case UnityClassID.SpriteRenderer:
					case UnityClassID.Transform:
					case UnityClassID.GameObject:
						continue;
					}
				}
				string text = subfile.classID2.ToString() + (subfile.classID1 != subfile.classID2 ? " " + (int)subfile.classID1 : String.Empty);
				ListViewItem item = new ListViewItem(new string[] { assetNames[i], text });
				item.Tag = subfile;
				if (!(subfile is NotLoaded))
				{
					item.Font = new Font(animatorsList.Font, FontStyle.Bold | (Editor.Marked.Contains(subfile) ? FontStyle.Underline : 0));
				}
				else
				{
					NotLoaded asset = (NotLoaded)subfile;
					item.SubItems.Add(asset.size.ToString());
					if (Editor.Marked.Contains(subfile))
					{
						item.Font = new Font(animatorsList.Font, FontStyle.Underline);
					}
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
				case UnityClassID.Cubemap:
					images.Add(item);
					if (itemWidth > imagesListHeaderName.Width)
					{
						imagesListHeaderName.Width = itemWidth;
					}
					break;
				case UnityClassID.AudioClip:
					sounds.Add(item);
					if (itemWidth > soundsListHeaderName.Width)
					{
						soundsListHeaderName.Width = itemWidth;
					}
					break;
				case UnityClassID.AudioSource:
				case UnityClassID.AudioListener:
				case UnityClassID.Avatar:
				case UnityClassID.BoxCollider:
				case UnityClassID.Camera:
				case UnityClassID.CapsuleCollider:
				case UnityClassID.FlareLayer:
				case UnityClassID.LinkToGameObject:
				case UnityClassID.LinkToGameObject223:
				case UnityClassID.LinkToGameObject225:
				case UnityClassID.Mesh:
				case UnityClassID.MeshCollider:
				case UnityClassID.MeshFilter:
				case UnityClassID.MeshRenderer:
				case UnityClassID.MultiLink:
				case UnityClassID.Projector:
				case UnityClassID.Rigidbody:
				case UnityClassID.SkinnedMeshRenderer:
				case UnityClassID.SphereCollider:
				case UnityClassID.SpriteRenderer:
				case UnityClassID.Transform:
				case UnityClassID.GameObject:
					filtered.Add(item);
					break;
				default:
					if (subfile.classID1 != UnityClassID.AnimationClip &&
						subfile.classID1 != UnityClassID.AnimatorController &&
						subfile.classID1 != UnityClassID.AssetBundle &&
						subfile.classID1 != UnityClassID.Cubemap &&
						subfile.classID1 != UnityClassID.EllipsoidParticleEmitter &&
						subfile.classID1 != UnityClassID.Light &&
						(subfile.classID2 != UnityClassID.MonoBehaviour || Editor.Parser.Cabinet.Types.Count == 0) &&
						subfile.classID1 != UnityClassID.MonoScript &&
						subfile.classID1 != UnityClassID.ParticleAnimator &&
						subfile.classID1 != UnityClassID.ParticleRenderer &&
						subfile.classID1 != UnityClassID.ParticleSystem &&
						subfile.classID1 != UnityClassID.ParticleSystemRenderer &&
						subfile.classID1 != UnityClassID.Sprite &&
						subfile.classID1 != UnityClassID.TextAsset)
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
			foreach (Animator anim in Editor.VirtualAnimators)
			{
				string name = anim.m_GameObject.asset is NotLoaded ? ((NotLoaded)anim.m_GameObject.asset).Name : anim.m_GameObject.instance.m_Name;
				FontStyle marked = Editor.Marked.Contains(anim.m_GameObject.asset) ? FontStyle.Underline : 0;
				ListViewItem item = new ListViewItem(new string[] { name, anim.classID2.ToString() });
				item.Tag = anim;
				if (!(anim.m_GameObject.asset is NotLoaded))
				{
					item.Font = new Font(animatorsList.Font, FontStyle.Bold | marked);
				}
				else
				{
					NotLoaded asset = (NotLoaded)anim.m_GameObject.asset;
					item.SubItems.Add(asset.size.ToString());
					if (marked != 0)
					{
						item.Font = new Font(animatorsList.Font, marked);
					}
				}
				item.ForeColor = Color.Purple;
				int itemWidth = (int)Math.Ceiling(Graphics.FromHwnd(Handle).MeasureString(item.Text, bold).Width) + 16;

				animators.Add(item);
				if (itemWidth > animatorsListHeader.Width)
				{
					animatorsListHeader.Width = itemWidth;
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
			filteredList.ListViewItemSorter = listViewItemComparer;
			adjustSubfileListsEnabled(true);
			adjustSubfileLists(opening);
			ReselectItems(animatorsList, selectedAnimators);
			ReselectItems(materialsList, selectedMaterials);
			ReselectItems(imagesList, selectedImg);
			ReselectItems(soundsList, selectedSounds);
			ReselectItems(othersList, selectedOthers);

			if (soundsList.Items.Count > 0 && soundLib == null)
			{
				soundLib = new Utility.SoundLib();
			}
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
					for (int j = assetListViews[i].Columns.Count - 1; j >= 0; j--)
					{
						int prevWidth = assetListViews[i].Columns[j].Width;
						assetListViews[i].Columns[j].Width = -2;
						assetListViews[i].Columns[j].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
						if (assetListViews[i].Columns[j].Width < prevWidth)
						{
							assetListViews[i].Columns[j].Width = prevWidth;
						}
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
			removeToolStripMenuItem_Click(sender, e);
		}

		public List<FormAnimator> OpenAnimatorsList()
		{
			List<FormAnimator> list = new List<FormAnimator>(animatorsList.SelectedItems.Count);
			foreach (ListViewItem item in animatorsList.SelectedItems)
			{
				Animator anim = item.Tag as Animator;
				bool vAnimator = Editor.VirtualAnimators.Contains(anim);
				int componentIdx = Editor.Parser.Cabinet.Components.IndexOf(vAnimator ? anim.m_GameObject.asset : (Component)item.Tag);
				FormAnimator formAnimator = (FormAnimator)Gui.Scripting.RunScript(FormVariable + ".OpenAnimator(componentIndex=" + componentIdx + ", virtualAnimator=" + vAnimator + ")", false);
				formAnimator.Activate();
				list.Add(formAnimator);
			}
			InitSubfileLists(false);
			return list;
		}

		[Plugin]
		public FormAnimator OpenAnimator(int componentIndex, bool virtualAnimator)
		{
			Component asset = Editor.Parser.Cabinet.Components[componentIndex];
			string name = asset is NotLoaded ? ((NotLoaded)asset).Name : AssetCabinet.ToString(asset);
			if (!virtualAnimator)
			{
				name += asset.pathID;
			}
			DockContent child;
			if (!ChildForms.TryGetValue(name, out child))
			{
				string childParserVar;
				bool changed = true;
				if (!ChildParserVars.TryGetValue(name, out childParserVar))
				{
					if (!virtualAnimator)
					{
						childParserVar = Gui.Scripting.GetNextVariable("animator");
						Gui.Scripting.RunScript(childParserVar + " = " + EditorVar + ".OpenAnimator(componentIndex=" + componentIndex + ")");
					}
					else
					{
						childParserVar = Gui.Scripting.GetNextVariable("virtualAnimator");
						Gui.Scripting.RunScript(childParserVar + " = " + EditorVar + ".OpenVirtualAnimator(componentIndex=" + componentIndex + ")");
					}
					ChildParserVars.Add(name, childParserVar);

					changed = false;
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
						/*Component comp = (Component)Gui.Scripting.Variables[parserVar];
						Editor.Parser.Cabinet.UnloadSubfile(comp);

						ChildParserVars.Remove((string)form.Tag);
						Gui.Scripting.RunScript(parserVar + "=null");
						InitSubfileLists(false);*/
						dontSwap = true;
					}
					else
					{
						Changed = true;
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

		private void anyListView_DoubleClick(object sender, EventArgs e)
		{
			using (Stream stream = File.OpenRead(Editor.Parser.FilePath))
			{
				bool format = false;
				foreach (ListViewItem item in ((ListView)sender).SelectedItems)
				{
					if (item.Tag is NotLoaded)
					{
						item.Tag = Editor.Parser.Cabinet.LoadComponent(stream, (NotLoaded)item.Tag);
						format = true;
					}
				}
				if (format)
				{
					InitSubfileLists(false);
				}
			}
		}

		private void materialsList_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION)
			{
				return;
			}
			removeToolStripMenuItem_Click(sender, e);
		}

		private void imagesList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (e.IsSelected)
				{
					Component asset = (Component)e.Item.Tag;
					Texture2D tex;
					if (asset is NotLoaded)
					{
						if (((NotLoaded)asset).replacement != null)
						{
							tex = (Texture2D)((NotLoaded)asset).replacement;
						}
						else
						{
							int texIdx = Editor.Parser.Textures.IndexOf(asset);
							tex = Editor.Parser.GetTexture(texIdx);
						}
						e.Item.Tag = tex;
					}
					else
					{
						tex = (Texture2D)asset;
					}
					using (MemoryStream mem = new MemoryStream())
					{
						tex.Export(mem);
						mem.Position = 0;
						ImportedTexture image = new ImportedTexture(mem, tex.m_Name);
						Gui.ImageControl.Image = image;
					}
					if (!e.Item.Font.Bold)
					{
						e.Item.Font = new Font(imagesList.Font, FontStyle.Bold);
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void imagesList_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION)
			{
				return;
			}
			removeToolStripMenuItem_Click(sender, e);
		}

		private void soundSubfilesList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (!soundLib.isLoaded())
					return;
				if (e.IsSelected)
				{
					Component subfile = (Component)e.Item.Tag;
					AudioClip audioClip = Editor.Parser.LoadAsset(subfile.pathID);
					soundLib.Play(e.Item.Text, audioClip.m_AudioData);
					if (!e.Item.Font.Bold)
					{
						e.Item.Font = new Font(imagesList.Font, FontStyle.Bold);
					}
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

		private void soundSubfilesList_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION)
			{
				return;
			}
			removeToolStripMenuItem_Click(sender, e);
		}

		private void saveUnity3dToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				CloseEditors();
				string backupExt = Path.GetExtension(Editor.Parser.FilePath);
				backupExt = backupExt == String.Empty ? backupExt = "None" : backupExt.Substring(1);
				backupExt = (string)Properties.Settings.Default["BackupExtension" + backupExt];
				BackgroundWorker worker = (BackgroundWorker)Gui.Scripting.RunScript(EditorVar + ".SaveUnity3d(keepBackup=" + keepBackupToolStripMenuItem.Checked + ", backupExtension=\"" + backupExt + "\", background=True)");
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
					CloseEditors();
					string backupExt = Path.GetExtension(Editor.Parser.FilePath);
					backupExt = backupExt == String.Empty ? backupExt = "None" : backupExt.Substring(1);
					backupExt = (string)Properties.Settings.Default["BackupExtension" + backupExt];
					BackgroundWorker worker = (BackgroundWorker)Gui.Scripting.RunScript(EditorVar + ".SaveUnity3d(path=\"" + saveFileDialog1.FileName + "\", keepBackup=" + keepBackupToolStripMenuItem.Checked + ", backupExtension=\"" + backupExt + "\", background=True)");
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

		void CloseEditors()
		{
			if (RemovedMods.Count > 0)
			{
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
			foreach (string originalsParserVar in RemovedMods)
			{
				UnityParser originalsParser = (UnityParser)Gui.Scripting.Variables[originalsParserVar];
				List<DockContent> formUnity3dList;
				if (Gui.Docking.DockContents.TryGetValue(typeof(FormUnity3d), out formUnity3dList))
				{
					foreach (var form in formUnity3dList)
					{
						var formParser = (UnityParser)Gui.Scripting.Variables[((FormUnity3d)form).ParserVar];
						if (formParser == originalsParser)
						{
							form.Close();
							break;
						}
					}
				}
			}
			RemovedMods.Clear();

			foreach (DockContent child in ChildForms.Values)
			{
				var editorForm = child as EditedContent;
				if (editorForm != null)
				{
					editorForm.Changed = false;
				}
			}
			InitSubfileLists(false);

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

		private void exportAssetsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				folderBrowserDialog1.SelectedPath = Path.GetDirectoryName(this.Editor.Parser.FilePath);
				folderBrowserDialog1.RootFolder = Environment.SpecialFolder.MyComputer;

				ListView subfilesList = null;
				if (tabControlAssets.SelectedTab == tabPageImages)
				{
					subfilesList = imagesList;
				}
				else if (tabControlAssets.SelectedTab == tabPageSounds)
				{
					subfilesList = soundsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageMaterials)
				{
					subfilesList = materialsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageOthers)
				{
					subfilesList = othersList;
				}
				if (subfilesList == null || subfilesList.SelectedItems.Count == 0)
				{
					Report.ReportLog("Nothing is selected for export.");
					return;
				}
				folderBrowserDialog1.Description = subfilesList.SelectedItems.Count + " subfiles will be exported.";
				if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
				{
					foreach (ListViewItem item in subfilesList.SelectedItems)
					{
						Component subfile = (Component)item.Tag;
						if (subfile is NotLoaded && ((NotLoaded)subfile).replacement != null)
						{
							subfile = ((NotLoaded)subfile).replacement;
						}
						Gui.Scripting.RunScript(EditorVar + ".Export" + subfile.classID2 + "(asset=" + ParserVar + ".Cabinet.Components[" + Editor.Parser.Cabinet.Components.IndexOf(subfile) + "], path=\"" + folderBrowserDialog1.SelectedPath + "\")");
					}
					for (int i = 0; i < assetListViews.Count; i++)
					{
						foreach (ListViewItem item in subfilesList.Items)
						{
							Component subfile = (Component)item.Tag;
							if (subfile is NotLoaded && ((NotLoaded)subfile).replacement != null)
							{
								item.Tag = ((NotLoaded)subfile).replacement;
								item.Font = new Font(subfilesList.Font, FontStyle.Bold);
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

		private void replaceFilesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				if (openFileDialog1.ShowDialog() == DialogResult.OK)
				{
					List<string> editors = new List<string>(openFileDialog1.FileNames.Length);
					foreach (string path in openFileDialog1.FileNames)
					{
						DockContent content;
						if (ChildForms.TryGetValue(Path.GetFileName(path), out content))
						{
							EditedContent editor = content as EditedContent;
							if (editor != null)
							{
								editors.Add(Path.GetFileName(path));
							}
						}
					}
					if (!CloseEditors(assetsToolStripMenuItem.Text + "/" + replaceFilesToolStripMenuItem.Text, editors))
					{
						return;
					}
					foreach (string path in openFileDialog1.FileNames)
					{
						string extension = Path.GetExtension(path).Substring(1);
						string function = null;
						switch (extension.ToLower())
						{
						case "bmp":
						case "dds":
						case "jpg":
						case "png":
						case "tga":
							function = "MergeTexture";
							break;
						case "ogg":
							function = "ReplaceAudioClip";
							break;
						default:
							UnityClassID classID = (UnityClassID)Enum.Parse(typeof(UnityClassID), extension, true);
							function = "Replace" + classID;
							break;
						}
						Gui.Scripting.RunScript(EditorVar + "." + function + "(path=\"" + path + "\")");
						Changed = Changed;
					}

					InitSubfileLists(false);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		bool CloseEditors(string title, List<string> editors)
		{
			if (editors.Count > 0)
			{
				FormPPSubfileChange dialog = new FormPPSubfileChange(title, editors.ToArray(), ChildForms);
				if (dialog.ShowDialog() == DialogResult.Cancel)
				{
					return false;
				}
				foreach (string editorName in editors)
				{
					DockContent content;
					if (ChildForms.TryGetValue(editorName, out content))
					{
						EditedContent editor = content as EditedContent;
						editor.Changed = false;
						content.Close();
					}
				}
			}
			return true;
		}

		private void createModToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				saveFileDialog1.FileName = Path.GetFileNameWithoutExtension(Editor.Parser.FilePath) + "-mod1" + Path.GetExtension(Editor.Parser.FilePath);
				if (saveFileDialog1.ShowDialog() == DialogResult.OK)
				{
					var animators = OpenAnimatorsList();
					string animEditors = String.Empty;
					foreach (FormAnimator form in animators)
					{
						animEditors += (animEditors.Length == 0 ? "{" : ", ") + form.EditorVar;
					}
					if (animEditors.Length > 0)
					{
						animEditors += "}";
					}
					else
					{
						animEditors = "null";
					}

					string singlesArg = String.Empty;
					foreach (ListViewItem i in imagesList.Items)
					{
						if (i.Selected)
						{
							Component asset = (Component)i.Tag;
							singlesArg += (singlesArg.Length == 0 ? "{" : ", ") + asset.pathID;
						}
					}
					foreach (ListViewItem i in soundsList.Items)
					{
						if (i.Selected)
						{
							Component asset = (Component)i.Tag;
							singlesArg += (singlesArg.Length == 0 ? "{" : ", ") + asset.pathID;
						}
					}
					foreach (ListViewItem i in othersList.Items)
					{
						if (i.Selected && !(i.Tag is AssetBundle) && !(i.Tag is MonoScript))
						{
							Component asset = (Component)i.Tag;
							singlesArg += (singlesArg.Length == 0 ? "{" : ", ") + asset.pathID;
						}
					}
					if (singlesArg.Length > 0)
					{
						singlesArg += "}";
					}
					else
					{
						singlesArg = "null";
					}

					string myExt = Path.GetExtension(Editor.Parser.FilePath).ToLower();
					string bakExt;
					switch (myExt)
					{
					case ".unity3d":
						bakExt = (string)Properties.Settings.Default["BackupExtensionUnity3d"];
						break;
					case ".assets":
						bakExt = (string)Properties.Settings.Default["BackupExtensionAssets"];
						break;
					default:
						bakExt = (string)Properties.Settings.Default["BackupExtensionNone"];
						break;
					}
					string orgParserVar = null;
					string modParserVar = null;
					try
					{
						string orgFilename = Path.GetDirectoryName(Editor.Parser.FilePath) + @"\" + Path.GetFileNameWithoutExtension(Editor.Parser.FilePath) + ".bak0" + bakExt;
						if (File.Exists(orgFilename))
						{
							orgParserVar = Gui.Scripting.GetNextVariable("unityParser");
							Gui.Scripting.RunScript(orgParserVar + " = OpenUnity3d(path=\"" + orgFilename + "\")");
						}
						else
						{
							foreach (var pair in Gui.Scripting.Variables)
							{
								if (pair.Value is FormUnity3d && (FormUnity3d)pair.Value != this
									&& Path.GetFileName(Editor.Parser.FilePath) == Path.GetFileName(((FormUnity3d)pair.Value).Editor.Parser.FilePath))
								{
									FormUnity3d orgForm = (FormUnity3d)pair.Value;
									orgParserVar = orgForm.ParserVar;
									break;
								}
							}
							if (orgParserVar == null)
							{
								Report.ReportLog("Original unmodded archive not found");
								return;
							}
							bakExt = null;
						}

						modParserVar = Gui.Scripting.GetNextVariable("modParser");
						Gui.Scripting.RunScript(modParserVar + " = DeployCollect(parser=" + ParserVar + ", animatorEditors=" + animEditors + ", singleAssets=" + singlesArg + ")");
						BackgroundWorker worker = (BackgroundWorker)Gui.Scripting.RunScript("SaveMod(modParser=" + modParserVar + ", path=AddFirstNewPathID(originalParser=" + orgParserVar + ", modParser=" + modParserVar + ", path=\"" + saveFileDialog1.FileName + "\"), background=true)");
						ShowBlockingDialog(Editor.Parser.FilePath, worker);
					}
					finally
					{
						if (modParserVar != null)
						{
							Gui.Scripting.Variables.Remove(modParserVar);
						}
						if (bakExt != null && orgParserVar != null)
						{
							Gui.Scripting.Variables.Remove(orgParserVar);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void selectAllLoadedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				foreach (ListViewItem i in animatorsList.Items)
				{
					if (i.Tag is Animator && ((Animator)i.Tag).m_GameObject.instance != null)
					{
						i.Selected = true;
					}
				}
				foreach (ListViewItem i in imagesList.Items)
				{
					if (!(i.Tag is NotLoaded))
					{
						i.Selected = true;
					}
				}
				foreach (ListViewItem i in soundsList.Items)
				{
					if (!(i.Tag is NotLoaded))
					{
						i.Selected = true;
					}
				}
				foreach (ListViewItem i in othersList.Items)
				{
					if (!(i.Tag is NotLoaded) && !(i.Tag is AssetBundle) && !(i.Tag is MonoScript))
					{
						i.Selected = true;
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void unselectAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				animatorsList.SelectedItems.Clear();
				imagesList.SelectedItems.Clear();
				soundsList.SelectedItems.Clear();
				othersList.SelectedItems.Clear();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void applyModsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				HashSet<AssetCabinet> alreadyPatched = new HashSet<AssetCabinet>();
				foreach (Component comp in Editor.Parser.Cabinet.Components)
				{
					alreadyPatched.Add(comp.file);
				}
				foreach (var pair in Gui.Scripting.Variables)
				{
					if (pair.Value is UnityParser && !alreadyPatched.Contains(((UnityParser)pair.Value).Cabinet))
					{
						if ((bool)Gui.Scripting.RunScript("ApplyMod(parser=" + ParserVar + ", modParser=" + pair.Key + ", saveOriginals=true)"))
						{
							Report.ReportLog(Path.GetFileName(((UnityParser)pair.Value).FilePath) + " applied.");
							Changed = true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void removeModsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				List<KeyValuePair<string, object>> modParsers = new List<KeyValuePair<string, object>>();
				foreach (var pair in Gui.Scripting.Variables)
				{
					if (pair.Value is UnityParser && (UnityParser)pair.Value != Editor.Parser && ((UnityParser)pair.Value).FilePath.ToLower().Contains("-org-"))
					{
						modParsers.Add(pair);
					}
				}
				if (modParsers.Count == 0)
				{
					Report.ReportLog("Nothing to remove.");
					return;
				}
				modParsers.Sort
				(
					delegate(KeyValuePair<string, object> p1, KeyValuePair<string, object> p2)
					{
						UnityParser parser1 = (UnityParser)p1.Value;
						UnityParser parser2 = (UnityParser)p2.Value;
						return File.GetLastWriteTime(parser2.FilePath).CompareTo(File.GetLastWriteTime(parser1.FilePath));
					}
				);
				foreach (var pair in modParsers)
				{
					if ((bool)Gui.Scripting.RunScript("RemoveMod(parser=" + ParserVar + ", originalsParser=" + pair.Key + ", deleteOriginals=true)"))
					{
						RemovedMods.Add(pair.Key);
						Report.ReportLog(Path.GetFileName(((UnityParser)pair.Value).FilePath) + " removed. File queued for automatic deletion.");
						Changed = true;
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void markForCopyingtoolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ListView subfilesList = null;
				if (tabControlAssets.SelectedTab == tabPageAnimators)
				{
					subfilesList = animatorsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageImages)
				{
					subfilesList = imagesList;
				}
				else if (tabControlAssets.SelectedTab == tabPageSounds)
				{
					subfilesList = soundsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageMaterials)
				{
					subfilesList = materialsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageOthers)
				{
					subfilesList = othersList;
				}
				else if (tabControlAssets.SelectedTab == tabPageFiltered)
				{
					subfilesList = filteredList;
				}
				foreach (ListViewItem item in subfilesList.Items)
				{
					Component asset = (Component)item.Tag;
					Component markedAsset = asset;
					int compIdx = Editor.Parser.Cabinet.Components.IndexOf(asset);
					if (compIdx < 0)
					{
						Animator vAnim = (Animator)asset;
						markedAsset = vAnim.m_GameObject.asset;
						compIdx = Editor.Parser.Cabinet.Components.IndexOf(markedAsset);
					}
					if (item.Selected)
					{
						if (!Editor.Marked.Contains(markedAsset))
						{
							Gui.Scripting.RunScript(EditorVar + ".MarkAsset(componentIdx=" + compIdx + ")");
						}
					}
					else
					{
						if (Editor.Marked.Contains(markedAsset))
						{
							Gui.Scripting.RunScript(EditorVar + ".UnmarkAsset(componentIdx=" + compIdx + ")");
						}
					}
				}

				InitSubfileLists(false);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void pasteAllMarkedtoolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				Gui.Scripting.RunScript(EditorVar + ".PasteAllMarked()");
				Changed = Changed;

				InitSubfileLists(false);
				List<DockContent> formUnity3dList;
				Gui.Docking.DockContents.TryGetValue(typeof(FormUnity3d), out formUnity3dList);
				foreach (FormUnity3d form in formUnity3dList)
				{
					if (form != this && form.Editor.Marked.Count > 0)
					{
						form.InitSubfileLists(false);
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void removeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ListView subfilesList = null;
				if (tabControlAssets.SelectedTab == tabPageAnimators)
				{
					Report.ReportLog("Removing Animators is not implemented yet.");
					return;
				}
				else if (tabControlAssets.SelectedTab == tabPageImages)
				{
					subfilesList = imagesList;
				}
				else if (tabControlAssets.SelectedTab == tabPageSounds)
				{
					subfilesList = soundsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageMaterials)
				{
					subfilesList = materialsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageOthers)
				{
					subfilesList = othersList;
				}
				foreach (ListViewItem item in subfilesList.SelectedItems)
				{
					Component asset = (Component)item.Tag;
					Gui.Scripting.RunScript("RemoveAsset(parser=" + ParserVar + ", pathID=" + asset.pathID + ")");
				}
				Changed = Changed;

				InitSubfileLists(false);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void renameToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ListView subfilesList = null;
				if (tabControlAssets.SelectedTab == tabPageAnimators)
				{
					subfilesList = animatorsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageImages)
				{
					subfilesList = imagesList;
				}
				else if (tabControlAssets.SelectedTab == tabPageSounds)
				{
					subfilesList = soundsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageMaterials)
				{
					subfilesList = materialsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageOthers)
				{
					subfilesList = othersList;
				}
				if (subfilesList.SelectedItems.Count != 1)
				{
					return;
				}
				ListViewItem item = subfilesList.SelectedItems[0];
				using (FormPPRename renameForm = new FormPPRename(item))
				{
					if (renameForm.ShowDialog() == DialogResult.OK)
					{
						Animator anim = item.Tag as Animator;
						bool vAnimator = Editor.VirtualAnimators.Contains(anim);
						int componentIdx = Editor.Parser.Cabinet.Components.IndexOf(vAnimator ? anim.m_GameObject.asset : (Component)item.Tag);

						string oldName = item.Text;
						if (!vAnimator)
						{
							oldName += Editor.Parser.Cabinet.Components[componentIdx].pathID;
						}
						if ((bool)Gui.Scripting.RunScript(EditorVar + ".SetAssetName(componentIndex=" + componentIdx + ", name=\"" + renameForm.NewName + "\")"))
						{
							if (tabControlAssets.SelectedTab == tabPageAnimators)
							{
								string newName = renameForm.NewName;
								if (!vAnimator)
								{
									newName += Editor.Parser.Cabinet.Components[componentIdx].pathID;
								}
								if (ChildParserVars.ContainsKey(oldName))
								{
									string value = ChildParserVars[oldName];
									ChildParserVars.Remove(oldName);
									ChildParserVars.Add(newName, value);
								}

								if (ChildForms.ContainsKey(oldName))
								{
									DockContent value = ChildForms[oldName];
									ChildForms.Remove(oldName);
									ChildForms.Add(newName, value);
									value.Tag = newName;
									value.Text = renameForm.NewName;
									value.ToolTipText = Editor.Parser.FilePath + @"\" + renameForm.NewName;
								}
							}
							Changed = Changed;

							InitSubfileLists(false);
						}
						else
						{
							Report.ReportLog(((Component)item.Tag).classID1 + " asset could not be renamed.");
						}
					}
				}
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
			ToolStripEditTextBox backupExtTextBox = (ToolStripEditTextBox)sender;
			string backupExt;
			if (backupExtTextBox == backupExtension1ToolStripEditTextBox)
			{
				backupExt = "Unity3d";
			}
			else if (backupExtTextBox == backupExtension2ToolStripEditTextBox)
			{
				backupExt = "Assets";
			}
			else
			{
				backupExt = "None";
			}
			Properties.Settings.Default["BackupExtension" + backupExt] = backupExtTextBox.Text;
		}

		private void filterIncludedAssetsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
		{
			try
			{
				if (filterIncludedAssetsToolStripMenuItem.Checked)
				{
					tabControlAssets.TabPages.Remove(tabPageFiltered);
					assetListViews.Remove(filteredList);
				}
				else
				{
					tabControlAssets.TabPages.Add(tabPageFiltered);
					assetListViews.Add(filteredList);
				}

				InitSubfileLists(false);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dumpAssetBundleToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				Editor.Parser.Cabinet.Bundle.Dump();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dumpTypeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ListView subfilesList = null;
				if (tabControlAssets.SelectedTab == tabPageImages)
				{
					subfilesList = imagesList;
				}
				else if (tabControlAssets.SelectedTab == tabPageSounds)
				{
					subfilesList = soundsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageMaterials)
				{
					subfilesList = materialsList;
				}
				else if (tabControlAssets.SelectedTab == tabPageOthers)
				{
					subfilesList = othersList;
				}
				else if (tabControlAssets.SelectedTab == tabPageFiltered)
				{
					subfilesList = filteredList;
				}
				HashSet<UnityClassID> selectedClasses = new HashSet<UnityClassID>();
				foreach (ListViewItem item in subfilesList.SelectedItems)
				{
					Component asset = (Component)item.Tag;
					selectedClasses.Add(asset.classID1);
				}
				foreach (UnityClassID cls in selectedClasses)
				{
					Editor.Parser.Cabinet.DumpType(cls);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void viewDataToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ListView subfilesList = null;
				if (tabControlAssets.SelectedTab == tabPageOthers)
				{
					subfilesList = othersList;
				}
				if (subfilesList != null)
				{
					foreach (ListViewItem item in subfilesList.SelectedItems)
					{
						Component asset = (Component)item.Tag;
						switch (asset.classID2)
						{
						case UnityClassID.MonoBehaviour:
							try
							{
								Editor.Parser.Cabinet.BeginLoadingSkippedComponents();
								Editor.Parser.Cabinet.SourceStream.Position = ((NotLoaded)asset).offset;
								PPtr<MonoScript> scriptRef = MonoBehaviour.LoadMonoScriptRef(Editor.Parser.Cabinet.SourceStream);
								Report.ReportLog(asset.classID2 + " " + asset.classID1 + ": MonoScript FileID: " + scriptRef.m_FileID + ", PathID: " + scriptRef.m_PathID);
							}
							finally
							{
								Editor.Parser.Cabinet.EndLoadingSkippedComponents();
							}
							break;
						case UnityClassID.MonoScript:
							if (asset is NotLoaded)
							{
								asset = Editor.Parser.Cabinet.LoadComponent(asset.pathID);
								item.Tag = asset;
							}
							MonoScript script = (MonoScript)asset;
							Report.ReportLog
							(
								asset.classID1 + " PathID: " + asset.pathID
								+ "\r\n\tClassName: " + script.m_ClassName
								+ "\r\n\tNamespace: " + script.m_Namespace
								+ "\r\n\tAssemply: " + script.m_AssemblyName
							);
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void othersList_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION)
			{
				return;
			}
			removeToolStripMenuItem_Click(sender, e);
		}

		private void filteredList_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			filteredList.BeginUpdate();
			bool newColumn = listViewItemComparer.col != e.Column;
			listViewItemComparer.col = e.Column;
			if (filteredList.Sorting != SortOrder.Ascending || newColumn)
			{
				filteredList.ListViewItemSorter = listViewItemComparer;
				listViewItemComparer.asc = true;
				filteredList.Sorting = SortOrder.Ascending;
			}
			else
			{
				filteredList.ListViewItemSorter = listViewItemComparer;
				listViewItemComparer.asc = false;
				filteredList.Sorting = SortOrder.Descending;
			}
			filteredList.Sort();
			filteredList.EndUpdate();
		}

		class ListViewItemComparer : IComparer
		{
			public int col;
			public bool asc;

			public ListViewItemComparer()
			{
				col = 0;
				asc = true;
			}
			public ListViewItemComparer(int column)
			{
				col = column;
				asc = true;
			}
			public int Compare(object x, object y)
			{
				int cmp = String.Compare(((ListViewItem)x).SubItems[col].Text, ((ListViewItem)y).SubItems[col].Text);
				return asc ? cmp : -cmp;
			}
		}

		private void filteredList_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION)
			{
				return;
			}
			removeToolStripMenuItem_Click(sender, e);
		}
	}
}
