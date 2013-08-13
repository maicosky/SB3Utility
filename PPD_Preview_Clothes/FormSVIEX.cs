using System;
using System.Collections.Generic;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;
using System.Windows.Forms;
using SlimDX;

using SB3Utility;

namespace PPD_Preview_Clothes
{
	[Plugin]
	[PluginTool("&SviEx Normal Approximation", "Alt+N")]
	public partial class FormSVIEX : DockContent
	{
		static FormSVIEX theInstance = null;

		public FormSVIEX()
		{
			try
			{
				if (theInstance != null)
				{
					theInstance.FillMeshComboBoxes();
					theInstance.BringToFront();
					return;
				}
				theInstance = this;

				InitializeComponent();

				this.ShowHint = DockState.Document;
				this.Text = "SviEx Normals";
				Gui.Docking.ShowDockContent(this, Gui.Docking.DockEditors, ContentCategory.Others);

				FillMeshComboBoxes();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void CustomDispose()
		{
			try
			{
				if (this == theInstance)
				{
					theInstance = null;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		class ComboBoxItemXX
		{
			public List<ComboBoxItemMesh> meshes;
			public xxParser xxParser;
			public FormPP ppForm;
			public FormXX xxForm;

			public override String ToString()
			{
				return xxParser.Name + " - " + Path.GetFileName(ppForm.Editor.Parser.FilePath);
			}
		}

		class ComboBoxItemMesh
		{
			public xxFrame meshFrame;
			public string submeshes;

			public ComboBoxItemMesh(xxFrame meshFrame, string submeshes)
			{
				this.meshFrame = meshFrame;
				this.submeshes = submeshes;
			}

			public override string ToString()
			{
				return meshFrame.ToString();
			}
		}

		public void FillMeshComboBoxes()
		{
			List<DockContent> formXXList;
			if (!Gui.Docking.DockContents.TryGetValue(typeof(FormXX), out formXXList))
			{
				return;
			}
			List<DockContent> formPPList;
			if (!Gui.Docking.DockContents.TryGetValue(typeof(FormPP), out formPPList))
			{
				return;
			}

			comboBoxTargetXX.Items.Clear();
			comboBoxCorrectlyLitXX.Items.Clear();
			foreach (FormPP form in formPPList)
			{
				foreach (IWriteFile file in form.Editor.Parser.Subfiles)
				{
					if (file is xxParser)
					{
						foreach (FormXX xxForm in formXXList)
						{
							if (Gui.Scripting.Variables[xxForm.ParserVar] == file)
							{
								ComboBoxItemXX cbItem = new ComboBoxItemXX();
								cbItem.meshes = new List<ComboBoxItemMesh>(xxForm.listViewMesh.SelectedItems.Count);
								cbItem.xxParser = (xxParser)file;
								cbItem.ppForm = form;
								cbItem.xxForm = xxForm;
								foreach (ListViewItem item in xxForm.listViewMesh.SelectedItems)
								{
									ComboBoxItemMesh cbiMesh = new ComboBoxItemMesh(xxForm.Editor.Meshes[(int)item.Tag], "-1");
									cbItem.meshes.Add(cbiMesh);
								}
								if (xxForm.listViewMesh.SelectedItems.Count > 0)
								{
									comboBoxTargetXX.Items.Add(cbItem);
								}
								comboBoxCorrectlyLitXX.Items.Add(cbItem);

								break;
							}
						}
					}
				}
			}
			if (comboBoxTargetXX.Items.Count > 0)
			{
				comboBoxTargetXX.SelectedIndex = 0;
			}
		}

		private void comboBoxTargetXX_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (comboBoxTargetXX.SelectedItem == null)
				{
					return;
				}
				ComboBoxItemXX cbItem = (ComboBoxItemXX)comboBoxTargetXX.SelectedItem;

				comboBoxTargetMeshes.Items.Clear();
				foreach (ComboBoxItemMesh meshItem in cbItem.meshes)
				{
					comboBoxTargetMeshes.Items.Add(meshItem);
				}
				comboBoxTargetMeshes.SelectedIndex = 0;

				comboBoxTargetSVIEXunits.Items.Clear();
				String sviex = Path.GetFileNameWithoutExtension(cbItem.xxParser.Name) + "_";
				foreach (IWriteFile file in cbItem.ppForm.Editor.Parser.Subfiles)
				{
					if (file.Name.StartsWith(sviex, StringComparison.InvariantCultureIgnoreCase) && file.Name.EndsWith(".sviex", StringComparison.InvariantCultureIgnoreCase))
					{
						comboBoxTargetSVIEXunits.Items.Add(file);
					}
				}
				if (comboBoxTargetSVIEXunits.Items.Count > 0)
				{
					comboBoxTargetSVIEXunits.SelectedIndex = 0;
				}

				if (comboBoxCorrectlyLitXX.SelectedItem == comboBoxTargetXX.SelectedItem)
				{
					buttonApproximateNormals.Enabled = false;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void comboBoxTargetMeshes_SelectedIndexChanged(object sender, EventArgs e)
		{
			ComboBoxItemMesh cbItem = (ComboBoxItemMesh)comboBoxTargetMeshes.SelectedItem;
			textBoxTargetedSubmeshes.Text = cbItem.submeshes;
		}

		private void textBoxTargetedSubmeshes_AfterEditTextChanged(object sender, EventArgs e)
		{
			ComboBoxItemMesh cbItem = (ComboBoxItemMesh)comboBoxTargetMeshes.SelectedItem;
			cbItem.submeshes = textBoxTargetedSubmeshes.Text;
		}

		private void comboBoxCorrectlyLitXX_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (comboBoxCorrectlyLitXX.SelectedItem == null)
				{
					return;
				}
				ComboBoxItemXX cbItem = (ComboBoxItemXX)comboBoxCorrectlyLitXX.SelectedItem;

				comboBoxSourceSVIEXunits.Items.Clear();
				String sviex = Path.GetFileNameWithoutExtension(cbItem.xxParser.Name) + "_";
				foreach (IWriteFile file in cbItem.ppForm.Editor.Parser.Subfiles)
				{
					if (file.Name.StartsWith(sviex, StringComparison.InvariantCultureIgnoreCase) && file.Name.EndsWith(".sviex", StringComparison.InvariantCultureIgnoreCase))
					{
						comboBoxSourceSVIEXunits.Items.Add(file);
					}
				}
				comboBoxCorrectlyLitMeshes.Items.Clear();
				if (comboBoxSourceSVIEXunits.Items.Count > 0)
				{
					sviexParser srcParser = PluginsPPD.OpenSVIEX((ppParser)Gui.Scripting.Variables[cbItem.ppForm.ParserVar], ((IWriteFile)comboBoxSourceSVIEXunits.Items[0]).Name);
					Dictionary<string, ComboBoxItemMesh> meshFrameDic = new Dictionary<string, ComboBoxItemMesh>();
					foreach (sviexParser.SubmeshSection section in srcParser.sections)
					{
						ComboBoxItemMesh meshItem;
						if (!meshFrameDic.TryGetValue(section.Name, out meshItem))
						{
							foreach (xxFrame meshFrame in cbItem.xxForm.Editor.Meshes)
							{
								if (meshFrame.Name == section.Name)
								{
									meshItem = new ComboBoxItemMesh(meshFrame, section.submeshIdx.ToString());
									comboBoxCorrectlyLitMeshes.Items.Add(meshItem);
									meshFrameDic.Add(section.Name, meshItem);
									break;
								}
							}
						}
						else
						{
							meshItem.submeshes += ", " + section.submeshIdx;
						}
					}
					comboBoxCorrectlyLitMeshes.SelectedIndex = 0;
					buttonApproximateNormals.Enabled = comboBoxCorrectlyLitXX.SelectedItem != comboBoxTargetXX.SelectedItem;
				}
				else
				{
					comboBoxSourceSVIEXunits.Items.Add("No SVIEX for " + cbItem.meshes);
					buttonApproximateNormals.Enabled = false;
				}
				comboBoxSourceSVIEXunits.SelectedIndex = 0;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void comboBoxCorrectlyLitMeshes_SelectedIndexChanged(object sender, EventArgs e)
		{
			ComboBoxItemMesh cbItem = (ComboBoxItemMesh)comboBoxCorrectlyLitMeshes.SelectedItem;
			textBoxSourceSubmeshes.Text = cbItem.submeshes;
		}

		private void textBoxSourceSubmeshes_AfterEditTextChanged(object sender, EventArgs e)
		{
			ComboBoxItemMesh cbItem = (ComboBoxItemMesh)comboBoxCorrectlyLitMeshes.SelectedItem;
			cbItem.submeshes = textBoxSourceSubmeshes.Text;
		}

		private void buttonApproximateNormals_Click(object sender, EventArgs e)
		{
			try
			{
				ComboBoxItemXX srcItem = (ComboBoxItemXX)comboBoxCorrectlyLitXX.SelectedItem;
				string srcMeshes = String.Empty;
				string srcSubmeshes = String.Empty;
				foreach (ComboBoxItemMesh meshItem in comboBoxCorrectlyLitMeshes.Items)
				{
					srcMeshes += srcItem.xxForm.EditorVar + ".Meshes[" + srcItem.xxForm.Editor.Meshes.IndexOf(meshItem.meshFrame) + "], ";
					srcSubmeshes += meshItem.submeshes.Split(',').Length + ", " + meshItem.submeshes + ", ";
				}
				srcMeshes = srcMeshes.Substring(0, srcMeshes.Length - 2);
				srcSubmeshes = srcSubmeshes.Substring(0, srcSubmeshes.Length - 2);

				ComboBoxItemXX dstItem = (ComboBoxItemXX)comboBoxTargetXX.SelectedItem;
				string dstMeshes = String.Empty;
				string dstSubmeshes = String.Empty;
				foreach (ComboBoxItemMesh meshItem in comboBoxTargetMeshes.Items)
				{
					dstMeshes += dstItem.xxForm.EditorVar + ".Meshes[" + dstItem.xxForm.Editor.Meshes.IndexOf(meshItem.meshFrame) + "], ";
					if (meshItem.submeshes.Contains("-1"))
					{
						dstSubmeshes += "-1, ";
					}
					else
					{
						dstSubmeshes += meshItem.submeshes.Split(',').Length + ", " + meshItem.submeshes + ", ";
					}
				}
				dstMeshes = dstMeshes.Substring(0, dstMeshes.Length - 2);
				dstSubmeshes = dstSubmeshes.Substring(0, dstSubmeshes.Length - 2);

				string srcParserVar = Gui.Scripting.GetNextVariable("sviexParser");
				string dstParserVar = Gui.Scripting.GetNextVariable("sviexParser");
				string srcEditorVar = Gui.Scripting.GetNextVariable("sviexEditor");
				foreach (IWriteFile srcFile in comboBoxSourceSVIEXunits.Items)
				{
					comboBoxSourceSVIEXunits.SelectedItem = srcFile;

					string parserCommand = srcParserVar + " = OpenSVIEX(parser=" + srcItem.ppForm.ParserVar + ", name=\"" + srcFile.Name + "\")";
					sviexParser srcParser = (sviexParser)Gui.Scripting.RunScript(parserCommand);

					IWriteFile dstFile = (IWriteFile)comboBoxTargetSVIEXunits.Items[comboBoxSourceSVIEXunits.Items.IndexOf(srcFile)];
					parserCommand = dstParserVar + " = OpenSVIEX(parser=" + dstItem.ppForm.ParserVar + ", name=\"" + dstFile.Name + "\")";
					Gui.Scripting.RunScript(parserCommand);

					sviexEditor srcEditor = (sviexEditor)Gui.Scripting.RunScript(srcEditorVar + " = sviexEditor(parser=" + srcParserVar + ")");
					srcEditor.progressBar = progressBarApproximation;
					Gui.Scripting.RunScript(srcEditorVar + ".CopyNearestNormals(srcMeshes={ " + srcMeshes + " }, srcSubmeshes={ " + srcSubmeshes + " }, dstMeshes={ " + dstMeshes + " }, dstSubmeshes={ " + dstSubmeshes + " }, dstParser=" + dstParserVar + ", nearVertexThreshold=" + ((float)numericUpDownNearVertexSqDist.Value).ToFloatString() + ", nearestNormal=" + checkBoxNearestNormal.Checked + ", automatic=" + checkBoxAutomatic.Checked + ")");
					Gui.Scripting.RunScript(dstItem.ppForm.EditorVar + ".ReplaceSubfile(file=" + dstParserVar + ")");

					comboBoxTargetSVIEXunits.SelectedItem = dstFile;
				}
				Gui.Scripting.Variables.Remove(srcParserVar);
				Gui.Scripting.Variables.Remove(srcEditorVar);

				progressBarApproximation.Value = 0;
				progressBarApproximation.Maximum = 1;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void checkBoxShowTargetNormals_Click(object sender, EventArgs e)
		{
			try
			{
				ComboBoxItemXX cbItem = (ComboBoxItemXX)comboBoxTargetXX.SelectedItem;
				if (cbItem == null)
				{
					return;
				}
				if (checkBoxShowTargetNormals.Checked)
				{
					SwapNormals(cbItem, ((IWriteFile)comboBoxTargetSVIEXunits.SelectedItem).Name);
				}
				else
				{
					cbItem.xxForm.RecreateRenderObjects();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private static void SwapNormals(ComboBoxItemXX cbItem, string sviexName)
		{
			Dictionary<xxVertex, Vector3> originalNormals = new Dictionary<xxVertex, Vector3>();
			sviexParser targetParser = PluginsPPD.OpenSVIEX((ppParser)Gui.Scripting.Variables[cbItem.ppForm.ParserVar], sviexName);
			foreach (sviexParser.SubmeshSection section in targetParser.sections)
			{
				bool meshFound = false;
				foreach (ComboBoxItemMesh itemMesh in cbItem.meshes)
				{
					if (section.Name == itemMesh.meshFrame.Name)
					{
						meshFound = true;
						xxSubmesh submesh = itemMesh.meshFrame.Mesh.SubmeshList[section.submeshIdx];
						if (section.indices.Length != submesh.VertexList.Count)
						{
							Report.ReportLog("Unmatching SVIEX mesh=" + section.Name + " submeshIdx=" + section.submeshIdx + " has " + section.indices.Length + " indices.");
							break;
						}
						for (int i = 0; i < section.indices.Length; i++)
						{
							ushort vIdx = section.indices[i];
							Vector3 norm = section.normals[i];
							xxVertex vert = submesh.VertexList[vIdx];
							originalNormals.Add(vert, vert.Normal);
							vert.Normal = norm;
						}
						break;
					}
				}
				if (!meshFound)
				{
					Report.ReportLog("SVIEX Normals not copied for " + section.Name);
				}
			}

			cbItem.xxForm.RecreateRenderObjects();

			foreach (sviexParser.SubmeshSection section in targetParser.sections)
			{
				foreach (ComboBoxItemMesh itemMesh in cbItem.meshes)
				{
					if (section.Name == itemMesh.meshFrame.Name)
					{
						xxSubmesh submesh = itemMesh.meshFrame.Mesh.SubmeshList[section.submeshIdx];
						if (section.indices.Length != submesh.VertexList.Count)
						{
							break;
						}
						for (int i = 0; i < section.indices.Length; i++)
						{
							ushort vIdx = section.indices[i];
							xxVertex vert = submesh.VertexList[vIdx];
							Vector3 norm = originalNormals[vert];
							vert.Normal = norm;
						}
						break;
					}
				}
			}
		}

		private void checkBoxShowSourceNormals_Click(object sender, EventArgs e)
		{
			try
			{
				ComboBoxItemXX cbItem = (ComboBoxItemXX)comboBoxCorrectlyLitXX.SelectedItem;
				if (cbItem == null)
				{
					return;
				}
				if (checkBoxShowSourceNormals.Checked)
				{
					SwapNormals(cbItem, ((IWriteFile)comboBoxSourceSVIEXunits.SelectedItem).Name);
				}
				else
				{
					cbItem.xxForm.RecreateRenderObjects();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void checkBoxAutomatic_Click(object sender, EventArgs e)
		{
			checkBoxNearestNormal.Enabled = !checkBoxAutomatic.Checked;
		}
	}
}
