using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using SlimDX;
using SlimDX.Direct3D9;

using SB3Utility;

using System.Media;

namespace AiDroidPlugin
{
	[Plugin]
	[PluginOpensFile(".rem")]
	public partial class FormREM : DockContent
	{
		private class KeyList<T>
		{
			public List<T> List { get; protected set; }
			public int Index { get; protected set; }

			public KeyList(List<T> list, int index)
			{
				List = list;
				Index = index;
			}
		}

		public remEditor Editor { get; protected set; }
		public string EditorVar { get; protected set; }
		public string ParserVar { get; protected set; }
		public string FormVar { get; protected set; }

		string exportDir;
		EditTextBox[][] matMatrixText = new EditTextBox[5][];
		ComboBox[] matTexNameCombo;
		bool SetComboboxEvent = false;

		int loadedFrame = -1;
		int[] loadedBone = null;
		int[] highlightedBone = null;
		int loadedMesh = -1;
		int loadedMaterial = -1;
		int loadedTexture = -1;

		Dictionary<int, List<KeyList<remMaterial>>> crossRefMeshMaterials = new Dictionary<int, List<KeyList<remMaterial>>>();
		Dictionary<int, List<KeyList<string>>> crossRefMeshTextures = new Dictionary<int, List<KeyList<string>>>();
		Dictionary<int, List<KeyList<remMesh>>> crossRefMaterialMeshes = new Dictionary<int, List<KeyList<remMesh>>>();
		Dictionary<int, List<KeyList<string>>> crossRefMaterialTextures = new Dictionary<int, List<KeyList<string>>>();
		Dictionary<int, List<KeyList<remMesh>>> crossRefTextureMeshes = new Dictionary<int, List<KeyList<remMesh>>>();
		Dictionary<int, List<KeyList<remMaterial>>> crossRefTextureMaterials = new Dictionary<int, List<KeyList<remMaterial>>>();
		Dictionary<int, int> crossRefMeshMaterialsCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefMeshTexturesCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefMaterialMeshesCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefMaterialTexturesCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefTextureMeshesCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefTextureMaterialsCount = new Dictionary<int, int>();

		List<RenderObjectREM> renderObjectMeshes;
		List<int> renderObjectIds;

		private bool listViewItemSyncSelectedSent = false;

		public FormREM(string path, string variable)
		{
			try
			{
				InitializeComponent();
				saveFileDialog1.Filter = ".xx Files (*.xx)|*.xx|All Files (*.*)|*.*";

				this.menuStrip1.Enabled = false;

				this.ShowHint = DockState.Document;
				this.Text = Path.GetFileName(path);
				this.ToolTipText = path;
				this.exportDir = Path.GetDirectoryName(path) + @"\" + Path.GetFileNameWithoutExtension(path);

				Init();
				ParserVar = Gui.Scripting.GetNextVariable("remParser");
				EditorVar = Gui.Scripting.GetNextVariable("remEditor");
				FormVar = variable;
				ReopenREM();

				List<DockContent> formREMList;
				if (Gui.Docking.DockContents.TryGetValue(typeof(FormREM), out formREMList))
				{
					var listCopy = new List<FormREM>(formREMList.Count);
					for (int i = 0; i < formREMList.Count; i++)
					{
						listCopy.Add((FormREM)formREMList[i]);
					}

					foreach (var form in listCopy)
					{
						if (form != this)
						{
							if (form.ToolTipText == this.ToolTipText)
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

		private void ReopenREM()
		{
			string path = this.ToolTipText;
			string parserCommand = ParserVar + " = OpenREM(path=\"" + path + "\")";
			remParser parser = (remParser)Gui.Scripting.RunScript(parserCommand);

			string editorCommand = EditorVar + " = remEditor(parser=" + ParserVar + ")";
			Editor = (remEditor)Gui.Scripting.RunScript(editorCommand);

			LoadREM();
		}

		public FormREM(fpkParser fpkParser, string remParserVar)
		{
			try
			{
				InitializeComponent();
				this.Controls.Remove(this.menuStrip1);

				remParser parser = (remParser)Gui.Scripting.Variables[remParserVar];

				this.ShowHint = DockState.Document;
				this.Text = parser.Name;
				this.ToolTipText = fpkParser.FilePath + @"\" + parser.Name;
				this.exportDir = Path.GetDirectoryName(fpkParser.FilePath) + @"\" + Path.GetFileNameWithoutExtension(fpkParser.FilePath) + @"\" + Path.GetFileNameWithoutExtension(parser.Name);

				ParserVar = remParserVar;

				EditorVar = Gui.Scripting.GetNextVariable("remEditor");
				Editor = (remEditor)Gui.Scripting.RunScript(EditorVar + " = remEditor(parser=" + ParserVar + ")");

				Init();
				LoadREM();
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
				if (FormVar != null)
				{
					Gui.Scripting.Variables.Remove(ParserVar);
					Gui.Scripting.Variables.Remove(FormVar);
				}
				Gui.Scripting.Variables.Remove(EditorVar);

				UnloadREM();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void UnloadREM()
		{
			Editor.Dispose();
			Editor = null;
			DisposeRenderObjects();
			CrossRefsClear();
			ClearKeyList<remMaterial>(crossRefMeshMaterials);
			ClearKeyList<string>(crossRefMeshTextures);
			ClearKeyList<remMesh>(crossRefMaterialMeshes);
			ClearKeyList<string>(crossRefMaterialTextures);
			ClearKeyList<remMesh>(crossRefTextureMeshes);
			ClearKeyList<remMaterial>(crossRefTextureMaterials);
		}

		void DisposeRenderObjects()
		{
			foreach (ListViewItem item in listViewMesh.SelectedItems)
			{
				Gui.Renderer.RemoveRenderObject(renderObjectIds[(int)item.Tag]);
			}

			if (renderObjectMeshes != null)
			{
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

		void ClearKeyList<T>(Dictionary<int, List<KeyList<T>>> dic)
		{
			foreach (var pair in dic)
			{
				pair.Value.Clear();
			}
		}

		void RecreateRenderObjects()
		{
			DisposeRenderObjects();

			renderObjectMeshes = new List<RenderObjectREM>(new RenderObjectREM[Editor.Parser.RemFile.MESC.numMeshes]);
			renderObjectIds = new List<int>(new int[Editor.Parser.RemFile.MESC.numMeshes]);

			foreach (ListViewItem item in listViewMesh.SelectedItems)
			{
				int id = (int)item.Tag;
				remMesh mesh = Editor.Parser.RemFile.MESC[id];
				renderObjectMeshes[id] = new RenderObjectREM(Editor.Parser, mesh);

				RenderObjectREM renderObj = renderObjectMeshes[id];
				renderObjectIds[id] = Gui.Renderer.AddRenderObject(renderObj);
			}

/*			HighlightSubmeshes();
			if (highlightedBone != null)
				HighlightBone(highlightedBone, true);*/
		}

		void Init()
		{
			panelTexturePic.Resize += new EventHandler(panelTexturePic_Resize);
//			splitContainer1.Panel2MinSize = tabControlViews.Width;

			matTexNameCombo = new ComboBox[4] { comboBoxMatTex1, comboBoxMatTex2, comboBoxMatTex3, comboBoxMatTex4 };

			matMatrixText[0] = new EditTextBox[4] { textBoxMatDiffuseR, textBoxMatDiffuseG, textBoxMatDiffuseB, textBoxMatDiffuseA };
			matMatrixText[1] = new EditTextBox[4] { textBoxMatAmbientR, textBoxMatAmbientG, textBoxMatAmbientB, textBoxMatAmbientA };
			matMatrixText[2] = new EditTextBox[4] { textBoxMatSpecularR, textBoxMatSpecularG, textBoxMatSpecularB, textBoxMatSpecularA };
			matMatrixText[3] = new EditTextBox[4] { textBoxMatEmissiveR, textBoxMatEmissiveG, textBoxMatEmissiveB, textBoxMatEmissiveA };
			matMatrixText[4] = new EditTextBox[1] { textBoxMatSpecularPower };

			InitDataGridViewSRT(dataGridViewFrameSRT, dataGridViewFrameMatrix);
			InitDataGridViewMatrix(dataGridViewFrameMatrix, dataGridViewFrameSRT);
			InitDataGridViewSRT(dataGridViewBoneSRT, dataGridViewBoneMatrix);
			InitDataGridViewMatrix(dataGridViewBoneMatrix, dataGridViewBoneSRT);

/*			textBoxFrameName.AfterEditTextChanged += new EventHandler(textBoxFrameName_AfterEditTextChanged);
			textBoxBoneName.AfterEditTextChanged += new EventHandler(textBoxBoneName_AfterEditTextChanged);*/
			textBoxMatName.AfterEditTextChanged += new EventHandler(textBoxMatName_AfterEditTextChanged);
			textBoxTexName.AfterEditTextChanged += new EventHandler(textBoxTexName_AfterEditTextChanged);

			ColumnSubmeshMaterial.DisplayMember = "Item1";
			ColumnSubmeshMaterial.ValueMember = "Item2";
			ColumnSubmeshMaterial.DefaultCellStyle.NullValue = "(invalid)";

			for (int i = 0; i < matMatrixText.Length; i++)
			{
				for (int j = 0; j < matMatrixText[i].Length; j++)
				{
					matMatrixText[i][j].AfterEditTextChanged += new EventHandler(matMatrixText_AfterEditTextChanged);
				}
			}

			for (int i = 0; i < matTexNameCombo.Length; i++)
			{
				matTexNameCombo[i].Tag = i;
				matTexNameCombo[i].SelectedIndexChanged += new EventHandler(matTexNameCombo_SelectedIndexChanged);
				matTexNameCombo[i].TextChanged += new EventHandler(matTexNameCombo_TextChanged);
			}

/*			MeshExportFormat[] values = Enum.GetValues(typeof(MeshExportFormat)) as MeshExportFormat[];
			string[] descriptions = new string[values.Length];
			for (int i = 0; i < descriptions.Length; i++)
			{
				descriptions[i] = values[i].GetDescription();
			}
			comboBoxMeshExportFormat.Items.AddRange(descriptions);
			comboBoxMeshExportFormat.SelectedIndex = 4;*/

			Gui.Docking.ShowDockContent(this, Gui.Docking.DockEditors);
		}

		void InitDataGridViewSRT(DataGridViewEditor viewSRT, DataGridViewEditor viewMatrix)
		{
			DataTable tableSRT = new DataTable();
			tableSRT.Columns.Add(" ", typeof(string));
			tableSRT.Columns[0].ReadOnly = true;
			tableSRT.Columns.Add("X", typeof(float));
			tableSRT.Columns.Add("Y", typeof(float));
			tableSRT.Columns.Add("Z", typeof(float));
			tableSRT.Rows.Add(new object[] { "Translate", 0f, 0f, 0f });
			tableSRT.Rows.Add(new object[] { "Rotate", 0f, 0f, 0f });
			tableSRT.Rows.Add(new object[] { "Scale", 1f, 1f, 1f });
			viewSRT.Initialize(tableSRT, new DataGridViewEditor.ValidateCellDelegate(ValidateCellSRT), 3);
			viewSRT.Scroll += new ScrollEventHandler(dataGridViewEditor_Scroll);

			viewSRT.Columns[0].DefaultCellStyle = viewSRT.ColumnHeadersDefaultCellStyle;
			for (int i = 0; i < viewSRT.Columns.Count; i++)
			{
				viewSRT.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
			}

			viewSRT.Tag = viewMatrix;
		}

		private void dataGridViewSRT_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			DataGridView viewSRT = (DataGridView)sender;
			Vector3[] srt = GetSRT(viewSRT);
			Matrix mat = FbxUtility.SRTToMatrix(srt[0], srt[1], srt[2]);
			LoadMatrix(mat, null, (DataGridView)viewSRT.Tag);
		}

		private void dataGridViewMatrix_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			DataGridView viewMatrix = (DataGridView)sender;
			Matrix mat = GetMatrix(viewMatrix);
			LoadMatrix(mat, (DataGridView)viewMatrix.Tag, null);
		}

		void LoadMatrix(Matrix matrix, DataGridView viewSRT, DataGridView viewMatrix)
		{
			if (viewSRT != null)
			{
				Vector3[] srt = FbxUtility.MatrixToSRT(matrix);
				DataTable tableSRT = (DataTable)viewSRT.DataSource;
				for (int i = 0; i < 3; i++)
				{
					tableSRT.Rows[0][i + 1] = srt[2][i];
					tableSRT.Rows[1][i + 1] = srt[1][i];
					tableSRT.Rows[2][i + 1] = srt[0][i];
				}
			}

			if (viewMatrix != null)
			{
				DataTable tableMatrix = (DataTable)viewMatrix.DataSource;
				for (int i = 0; i < 4; i++)
				{
					for (int j = 0; j < 4; j++)
					{
						tableMatrix.Rows[i][j] = matrix[i, j];
					}
				}
			}
		}

		Matrix GetMatrix(DataGridView viewMatrix)
		{
			Matrix m = new Matrix();
			DataTable table = (DataTable)viewMatrix.DataSource;
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					m[i, j] = (float)table.Rows[i][j];
				}
			}
			return m;
		}

		Vector3[] GetSRT(DataGridView viewSRT)
		{
			DataTable table = (DataTable)viewSRT.DataSource;
			Vector3[] srt = new Vector3[3];
			for (int i = 0; i < 3; i++)
			{
				srt[0][i] = (float)table.Rows[2][i + 1];
				srt[1][i] = (float)table.Rows[1][i + 1];
				srt[2][i] = (float)table.Rows[0][i + 1];
			}
			return srt;
		}

		void InitDataGridViewMatrix(DataGridViewEditor viewMatrix, DataGridViewEditor viewSRT)
		{
			DataTable tableMatrix = new DataTable();
			tableMatrix.Columns.Add("1", typeof(float));
			tableMatrix.Columns.Add("2", typeof(float));
			tableMatrix.Columns.Add("3", typeof(float));
			tableMatrix.Columns.Add("4", typeof(float));
			tableMatrix.Rows.Add(new object[] { 1f, 0f, 0f, 0f });
			tableMatrix.Rows.Add(new object[] { 0f, 1f, 0f, 0f });
			tableMatrix.Rows.Add(new object[] { 0f, 0f, 1f, 0f });
			tableMatrix.Rows.Add(new object[] { 0f, 0f, 0f, 1f });
			viewMatrix.Initialize(tableMatrix, new DataGridViewEditor.ValidateCellDelegate(ValidateCellSingle), 4);
			viewMatrix.Scroll += new ScrollEventHandler(dataGridViewEditor_Scroll);

			for (int i = 0; i < viewMatrix.Columns.Count; i++)
			{
				viewMatrix.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
			}

			viewMatrix.Tag = viewSRT;
		}

		void dataGridViewEditor_Scroll(object sender, ScrollEventArgs e)
		{
			try
			{
				e.NewValue = e.OldValue;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		bool ValidateCellSRT(string s, int row, int col)
		{
			if (col == 0)
			{
				return true;
			}
			else
			{
				return ValidateCellSingle(s, row, col);
			}
		}

		bool ValidateCellSingle(string s, int row, int col)
		{
			float f;
			if (Single.TryParse(s, out f))
			{
				return true;
			}
			return false;
		}

		void panelTexturePic_Resize(object sender, EventArgs e)
		{
			try
			{
				ResizeImage();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void ResizeImage()
		{
			if (pictureBoxTexture.Image != null)
			{
				Decimal x = (Decimal)panelTexturePic.Width / pictureBoxTexture.Image.Width;
				Decimal y = (Decimal)panelTexturePic.Height / pictureBoxTexture.Image.Height;
				if (x > y)
				{
					pictureBoxTexture.Width = Decimal.ToInt32(pictureBoxTexture.Image.Width * y);
					pictureBoxTexture.Height = Decimal.ToInt32(pictureBoxTexture.Image.Height * y);
				}
				else
				{
					pictureBoxTexture.Width = Decimal.ToInt32(pictureBoxTexture.Image.Width * x);
					pictureBoxTexture.Height = Decimal.ToInt32(pictureBoxTexture.Image.Height * x);
				}
			}
		}

		void LoadREM()
		{
			renderObjectMeshes = new List<RenderObjectREM>(new RenderObjectREM[Editor.Parser.RemFile.MESC.numMeshes]);
			renderObjectIds = new List<int>(new int[Editor.Parser.RemFile.MESC.numMeshes]);

			InitFrames();
			InitMeshes();
			InitMaterials();
			InitTextures();

			RecreateCrossRefs();
		}

		void InitFrames()
		{
			TreeNode objRootNode = CreateFrameTree(Editor.Parser.RemFile.BONC.rootFrame, null);

			if (treeViewObjectTree.Nodes.Count > 0)
			{
				treeViewObjectTree.Nodes.RemoveAt(0);
			}
			treeViewObjectTree.Nodes.Insert(0, objRootNode);
		}

		private TreeNode CreateFrameTree(remBone frame, TreeNode parentNode)
		{
			TreeNode newNode = new TreeNode(frame.name);
			newNode.Tag = new DragSource(EditorVar, typeof(remBone), Editor.Parser.RemFile.BONC.frames.IndexOf(frame));

			remMesh mesh = rem.FindMesh(frame, Editor.Parser.RemFile.MESC);
			if (mesh != null)
			{
				int meshId = Editor.Parser.RemFile.MESC.meshes.IndexOf(mesh);
				TreeNode meshNode = new TreeNode("Mesh");
				meshNode.Tag = new DragSource(EditorVar, typeof(remMesh), meshId);
				newNode.Nodes.Add(meshNode);

				remSkin skin = rem.FindSkin(mesh.name, Editor.Parser.RemFile.SKIC);
				if (skin != null)
				{
					TreeNode boneListNode = new TreeNode("Bones");
					meshNode.Nodes.Add(boneListNode);
					for (int i = 0; i < skin.numWeights; i++)
					{
						remBoneWeights boneWeights = skin[i];
						TreeNode boneNode = new TreeNode(boneWeights.bone);
						boneNode.Tag = new DragSource(EditorVar, typeof(remBoneWeights), new int[] { meshId, i });
						boneListNode.Nodes.Add(boneNode);
					}
				}
			}

			if (parentNode != null)
			{
				parentNode.Nodes.Add(newNode);
			}
			for (int i = 0; i < frame.numChilds; i++)
			{
				CreateFrameTree(frame[i], newNode);
			}

			return newNode;
		}

		void InitMeshes()
		{
			ListViewItem[] meshItems = new ListViewItem[Editor.Parser.RemFile.MESC.numMeshes];
			for (int i = 0; i < Editor.Parser.RemFile.MESC.numMeshes; i++)
			{
				remMesh mesh = Editor.Parser.RemFile.MESC[i];
				meshItems[i] = new ListViewItem(mesh.name);
				meshItems[i].Tag = i;
			}
			listViewMesh.Items.Clear();
			listViewMesh.Items.AddRange(meshItems);
			meshlistHeader.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
		}

		void InitMaterials()
		{
			List<Tuple<string, int>> columnMaterials = new List<Tuple<string, int>>(Editor.Parser.RemFile.MATC.numMats);
			ListViewItem[] materialItems = new ListViewItem[Editor.Parser.RemFile.MATC.numMats];
			for (int i = 0; i < Editor.Parser.RemFile.MATC.numMats; i++)
			{
				remMaterial mat = Editor.Parser.RemFile.MATC[i];
				materialItems[i] = new ListViewItem(mat.name);
				materialItems[i].Tag = i;

				columnMaterials.Add(new Tuple<string, int>(mat.name, i));
			}
			listViewMaterial.Items.Clear();
			listViewMaterial.Items.AddRange(materialItems);
			materiallistHeader.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);

			ColumnSubmeshMaterial.DataSource = columnMaterials;
			SetComboboxEvent = false;

			TreeNode materialsNode = new TreeNode("Materials");
			for (int i = 0; i < Editor.Parser.RemFile.MATC.numMats; i++)
			{
				TreeNode matNode = new TreeNode(Editor.Parser.RemFile.MATC[i].name);
				matNode.Tag = new DragSource(EditorVar, typeof(remMaterial), i);
				materialsNode.Nodes.Add(matNode);
			}

			if (treeViewObjectTree.Nodes.Count > 1)
			{
				treeViewObjectTree.Nodes.RemoveAt(1);
			}
			treeViewObjectTree.Nodes.Insert(1, materialsNode);
		}

		void InitTextures()
		{
			for (int i = 0; i < matTexNameCombo.Length; i++)
			{
				matTexNameCombo[i].Items.Clear();
				matTexNameCombo[i].Items.Add("(none)");
			}

			ListViewItem[] textureItems = new ListViewItem[Editor.Textures.Count];
			for (int i = 0; i < Editor.Textures.Count; i++)
			{
				string tex = Editor.Textures[i];
				textureItems[i] = new ListViewItem(tex);
				textureItems[i].Tag = i;
				for (int j = 0; j < matTexNameCombo.Length; j++)
				{
					matTexNameCombo[j].Items.Add(tex);
				}
			}
			listViewTexture.Items.Clear();
			listViewTexture.Items.AddRange(textureItems);
			texturelistHeader.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);

			TreeNode texturesNode = new TreeNode("Textures");
			TreeNode currentTexture = null;
			for (int i = 0; i < Editor.Textures.Count; i++)
			{
				TreeNode texNode = new TreeNode(Editor.Textures[i]);
				texNode.Tag = new DragSource(EditorVar, typeof(string), i);
				texturesNode.Nodes.Add(texNode);
				if (loadedTexture == i)
					currentTexture = texNode;
			}

			if (treeViewObjectTree.Nodes.Count > 2)
			{
				treeViewObjectTree.Nodes.RemoveAt(2);
			}
			treeViewObjectTree.Nodes.Insert(2, texturesNode);
			if (currentTexture != null)
				currentTexture.EnsureVisible();
		}

		void RenameListViewItems<T>(List<T> list, ListView listView, T obj, string name)
		{
			foreach (ListViewItem item in listView.Items)
			{
				if (list[(int)item.Tag].Equals(obj))
				{
					item.Text = name;
					break;
				}
			}
			listView.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
		}

		TreeNode FindFrameNode(string name, TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				var source = node.Tag as DragSource?;
				if ((source == null) || (source.Value.Type != typeof(remBone)))
				{
					return null;
				}

				if (Editor.Parser.RemFile.BONC.frames[(int)source.Value.Id].name == name)
				{
					return node;
				}

				TreeNode found = FindFrameNode(name, node.Nodes);
				if (found != null)
				{
					return found;
				}
			}

			return null;
		}

		TreeNode FindFrameNode(remBone frame, TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				var source = node.Tag as DragSource?;
				if ((source == null) || (source.Value.Type != typeof(remBone)))
				{
					return null;
				}

				if (Editor.Parser.RemFile.BONC.frames[(int)source.Value.Id].Equals(frame))
				{
					return node;
				}

				TreeNode found = FindFrameNode(frame, node.Nodes);
				if (found != null)
				{
					return found;
				}
			}

			return null;
		}

		TreeNode FindBoneNode(remBoneWeights bone, TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				var source = node.Tag as DragSource?;

				var tuple = node.Tag as Tuple<remBoneWeights, int[]>;
				if ((source != null) && (source.Value.Type == typeof(remBoneWeights)))
				{
					var id = (int[])source.Value.Id;
					remMesh mesh = Editor.Parser.RemFile.MESC[id[0]];
					remSkin skin = rem.FindSkin(mesh.name, Editor.Parser.RemFile.SKIC);
					remBoneWeights boneWeights = skin[id[1]];
					if (boneWeights.Equals(bone))
					{
						return node;
					}
				}

				TreeNode found = FindBoneNode(bone, node.Nodes);
				if (found != null)
				{
					return found;
				}
			}

			return null;
		}

		void LoadTexture(int id)
		{
			if (id < 0)
			{
				textBoxTexName.Text = String.Empty;
				textBoxTexSize.Text = String.Empty;
				pictureBoxTexture.Image = null;
			}
			else
			{
				string tex = Editor.Textures[id];
				textBoxTexName.Text = tex;

				ImportedTexture importedTex = rem.ImportedTexture(new remId(tex), Path.GetDirectoryName(this.ToolTipText), true);
				Texture renderTexture = Texture.FromMemory(Gui.Renderer.Device, importedTex.Data);
				Bitmap bitmap = new Bitmap(Texture.ToStream(renderTexture, ImageFileFormat.Bmp));
				renderTexture.Dispose();
				pictureBoxTexture.Image = bitmap;
				textBoxTexSize.Text = bitmap.Width + "x" + bitmap.Height;

				ResizeImage();
			}
			loadedTexture = id;
		}

		#region CrossRefs

		private void RecreateCrossRefs()
		{
			CrossRefsClear();

			crossRefMeshMaterials.Clear();
			crossRefMeshTextures.Clear();
			crossRefMaterialMeshes.Clear();
			crossRefMaterialTextures.Clear();
			crossRefTextureMeshes.Clear();
			crossRefTextureMaterials.Clear();
			crossRefMeshMaterialsCount.Clear();
			crossRefMeshTexturesCount.Clear();
			crossRefMaterialMeshesCount.Clear();
			crossRefMaterialTexturesCount.Clear();
			crossRefTextureMeshesCount.Clear();
			crossRefTextureMaterialsCount.Clear();

			var meshes = Editor.Parser.RemFile.MESC.meshes;
			var materials = Editor.Parser.RemFile.MATC.materials;
			var textures = Editor.Textures;

			for (int i = 0; i < meshes.Count; i++)
			{
				crossRefMeshMaterials.Add(i, new List<KeyList<remMaterial>>(materials.Count));
				crossRefMeshTextures.Add(i, new List<KeyList<string>>(textures.Count));
				crossRefMaterialMeshesCount.Add(i, 0);
				crossRefTextureMeshesCount.Add(i, 0);
			}

			for (int i = 0; i < materials.Count; i++)
			{
				crossRefMaterialMeshes.Add(i, new List<KeyList<remMesh>>(meshes.Count));
				crossRefMaterialTextures.Add(i, new List<KeyList<string>>(textures.Count));
				crossRefMeshMaterialsCount.Add(i, 0);
				crossRefTextureMaterialsCount.Add(i, 0);
			}

			for (int i = 0; i < textures.Count; i++)
			{
				crossRefTextureMeshes.Add(i, new List<KeyList<remMesh>>(meshes.Count));
				crossRefTextureMaterials.Add(i, new List<KeyList<remMaterial>>(materials.Count));
				crossRefMeshTexturesCount.Add(i, 0);
				crossRefMaterialTexturesCount.Add(i, 0);
			}

			for (int i = 0; i < materials.Count; i++)
			{
				remMaterial mat = materials[i];
				int texIdx = textures.IndexOf(mat.texture);
				crossRefMaterialTextures[i].Add(new KeyList<string>(textures, i));
				crossRefTextureMaterials[texIdx].Add(new KeyList<remMaterial>(materials, i));
			}

			for (int i = 0; i < meshes.Count; i++)
			{
				remMesh mesh = meshes[i];
				for (int j = 0; j < mesh.materials.Count; j++)
				{
					remMaterial mat = rem.FindMaterial(mesh.materials[j], Editor.Parser.RemFile.MATC);
					int matIdx = Editor.Parser.RemFile.MATC.materials.IndexOf(mat);
					crossRefMeshMaterials[i].Add(new KeyList<remMaterial>(materials, matIdx));
					crossRefMaterialMeshes[matIdx].Add(new KeyList<remMesh>(meshes, i));
					int texIdx = textures.IndexOf(mat.texture);
					crossRefMeshTextures[i].Add(new KeyList<string>(textures, texIdx));
					crossRefTextureMeshes[texIdx].Add(new KeyList<remMesh>(meshes, i));
				}
			}

			CrossRefsSet();
		}

		private void CrossRefsSet()
		{
			listViewItemSyncSelectedSent = true;

			listViewMeshMaterial.BeginUpdate();
			listViewMeshTexture.BeginUpdate();
			for (int i = 0; i < listViewMesh.SelectedItems.Count; i++)
			{
				int mesh = (int)listViewMesh.SelectedItems[i].Tag;
				CrossRefAddItem(crossRefMeshMaterials[mesh], crossRefMeshMaterialsCount, listViewMeshMaterial, listViewMaterial);
				CrossRefAddItem(crossRefMeshTextures[mesh], crossRefMeshTexturesCount, listViewMeshTexture, listViewTexture);
			}
			listViewMeshMaterial.EndUpdate();
			listViewMeshTexture.EndUpdate();

			listViewMaterialMesh.BeginUpdate();
			listViewMaterialTexture.BeginUpdate();
			for (int i = 0; i < listViewMaterial.SelectedItems.Count; i++)
			{
				int mat = (int)listViewMaterial.SelectedItems[i].Tag;
				CrossRefAddItem(crossRefMaterialMeshes[mat], crossRefMaterialMeshesCount, listViewMaterialMesh, listViewMesh);
				CrossRefAddItem(crossRefMaterialTextures[mat], crossRefMaterialTexturesCount, listViewMaterialTexture, listViewTexture);
			}
			listViewMaterialMesh.EndUpdate();
			listViewMaterialTexture.EndUpdate();

			listViewTextureMesh.BeginUpdate();
			listViewTextureMaterial.BeginUpdate();
			for (int i = 0; i < listViewTexture.SelectedItems.Count; i++)
			{
				int tex = (int)listViewTexture.SelectedItems[i].Tag;
				CrossRefAddItem(crossRefTextureMeshes[tex], crossRefTextureMeshesCount, listViewTextureMesh, listViewMesh);
				CrossRefAddItem(crossRefTextureMaterials[tex], crossRefTextureMaterialsCount, listViewTextureMaterial, listViewMaterial);
			}
			listViewTextureMesh.EndUpdate();
			listViewTextureMaterial.EndUpdate();

			listViewItemSyncSelectedSent = false;
		}

		private void CrossRefsClear()
		{
			listViewItemSyncSelectedSent = true;

			listViewMeshMaterial.BeginUpdate();
			listViewMeshTexture.BeginUpdate();
			foreach (var pair in crossRefMeshMaterials)
			{
				int mesh = pair.Key;
				CrossRefRemoveItem(pair.Value, crossRefMeshMaterialsCount, listViewMeshMaterial);
				CrossRefRemoveItem(crossRefMeshTextures[mesh], crossRefMeshTexturesCount, listViewMeshTexture);
			}
			listViewMeshMaterial.EndUpdate();
			listViewMeshTexture.EndUpdate();

			listViewMaterialMesh.BeginUpdate();
			listViewMaterialTexture.BeginUpdate();
			foreach (var pair in crossRefMaterialMeshes)
			{
				int mat = pair.Key;
				CrossRefRemoveItem(pair.Value, crossRefMaterialMeshesCount, listViewMaterialMesh);
				CrossRefRemoveItem(crossRefMaterialTextures[mat], crossRefMaterialTexturesCount, listViewMaterialTexture);
			}
			listViewMaterialMesh.EndUpdate();
			listViewMaterialTexture.EndUpdate();

			listViewTextureMesh.BeginUpdate();
			listViewTextureMaterial.BeginUpdate();
			foreach (var pair in crossRefTextureMeshes)
			{
				int tex = pair.Key;
				CrossRefRemoveItem(pair.Value, crossRefTextureMeshesCount, listViewTextureMesh);
				CrossRefRemoveItem(crossRefTextureMaterials[tex], crossRefTextureMaterialsCount, listViewTextureMaterial);
			}
			listViewTextureMesh.EndUpdate();
			listViewTextureMaterial.EndUpdate();

			listViewItemSyncSelectedSent = false;
		}

		private void CrossRefAddItem<T>(List<KeyList<T>> list, Dictionary<int, int> dic, ListView listView, ListView mainView)
		{
			bool added = false;
			for (int i = 0; i < list.Count; i++)
			{
				int count = dic[list[i].Index] + 1;
				dic[list[i].Index] = count;
				if (count == 1)
				{
					var keylist = list[i];
					ListViewItem item = new ListViewItem(keylist.List[keylist.Index].ToString());
					item.Tag = keylist.Index;

					foreach (ListViewItem mainItem in mainView.Items)
					{
						if ((int)mainItem.Tag == keylist.Index)
						{
							item.Selected = mainItem.Selected;
							break;
						}
					}

					listView.Items.Add(item);
					added = true;
				}
			}

			if (added)
			{
				listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
			}
		}

		private void CrossRefRemoveItem<T>(List<KeyList<T>> list, Dictionary<int, int> dic, ListView listView)
		{
			bool removed = false;
			for (int i = 0; i < list.Count; i++)
			{
				int count = dic[list[i].Index] - 1;
				dic[list[i].Index] = count;
				if (count == 0)
				{
					var tuple = list[i];
					for (int j = 0; j < listView.Items.Count; j++)
					{
						if ((int)listView.Items[j].Tag == tuple.Index)
						{
							listView.Items.RemoveAt(j);
							removed = true;
							break;
						}
					}
				}
			}

			if (removed)
			{
				listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
			}
		}

		private void CrossRefSetSelected(bool selected, ListView view, int tag)
		{
			foreach (ListViewItem item in view.Items)
			{
				if ((int)item.Tag == tag)
				{
					item.Selected = selected;
					break;
				}
			}
		}

		#endregion CrossRefs

		#region MeshView

		private void listViewMesh_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (listViewItemSyncSelectedSent == false)
				{
					listViewItemSyncSelectedSent = true;
					listViewMeshMaterial.BeginUpdate();
					listViewMeshTexture.BeginUpdate();

					int meshIdx = (int)e.Item.Tag;
					if (e.IsSelected)
					{
						if (!Gui.Docking.DockRenderer.IsHidden)
						{
							Gui.Docking.DockRenderer.Activate();
						}
						tabControlViews.SelectedTab = tabPageMeshView;
//						LoadMesh(meshIdx);
						CrossRefAddItem(crossRefMeshMaterials[meshIdx], crossRefMeshMaterialsCount, listViewMeshMaterial, listViewMaterial);
						CrossRefAddItem(crossRefMeshTextures[meshIdx], crossRefMeshTexturesCount, listViewMeshTexture, listViewTexture);

						if (renderObjectMeshes[meshIdx] == null)
						{
							remMesh mesh = Editor.Parser.RemFile.MESC[meshIdx];
							renderObjectMeshes[meshIdx] = new RenderObjectREM(Editor.Parser, mesh);
						}
						RenderObjectREM renderObj = renderObjectMeshes[meshIdx];
						renderObjectIds[meshIdx] = Gui.Renderer.AddRenderObject(renderObj);
					}
					else
					{
						if (meshIdx == loadedMesh)
						{
//							LoadMesh(-1);
						}
						CrossRefRemoveItem(crossRefMeshMaterials[meshIdx], crossRefMeshMaterialsCount, listViewMeshMaterial);
						CrossRefRemoveItem(crossRefMeshTextures[meshIdx], crossRefMeshTexturesCount, listViewMeshTexture);

						Gui.Renderer.RemoveRenderObject(renderObjectIds[meshIdx]);
					}

					CrossRefSetSelected(e.IsSelected, listViewMesh, meshIdx);
					CrossRefSetSelected(e.IsSelected, listViewMaterialMesh, meshIdx);
					CrossRefSetSelected(e.IsSelected, listViewTextureMesh, meshIdx);

					listViewMeshMaterial.EndUpdate();
					listViewMeshTexture.EndUpdate();
					listViewItemSyncSelectedSent = false;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void listViewMeshMaterial_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewMaterial_ItemSelectionChanged(sender, e);
		}

		private void listViewMeshTexture_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewTexture_ItemSelectionChanged(sender, e);
		}

		#endregion MeshView

		#region MaterialView

		private void listViewMaterial_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (listViewItemSyncSelectedSent == false)
				{
					listViewItemSyncSelectedSent = true;
					listViewMaterialMesh.BeginUpdate();
					listViewMaterialTexture.BeginUpdate();

					int id = (int)e.Item.Tag;
					if (e.IsSelected)
					{
						tabControlViews.SelectedTab = tabPageMaterialView;
//						LoadMaterial(id);
						CrossRefAddItem(crossRefMaterialMeshes[id], crossRefMaterialMeshesCount, listViewMaterialMesh, listViewMesh);
						CrossRefAddItem(crossRefMaterialTextures[id], crossRefMaterialTexturesCount, listViewMaterialTexture, listViewTexture);
					}
					else
					{
						if (id == loadedMaterial)
						{
//							LoadMaterial(-1);
						}
						CrossRefRemoveItem(crossRefMaterialMeshes[id], crossRefMaterialMeshesCount, listViewMaterialMesh);
						CrossRefRemoveItem(crossRefMaterialTextures[id], crossRefMaterialTexturesCount, listViewMaterialTexture);
					}

					CrossRefSetSelected(e.IsSelected, listViewMaterial, id);
					CrossRefSetSelected(e.IsSelected, listViewMeshMaterial, id);
					CrossRefSetSelected(e.IsSelected, listViewTextureMaterial, id);

					listViewMaterialMesh.EndUpdate();
					listViewMaterialTexture.EndUpdate();
					listViewItemSyncSelectedSent = false;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void listViewMaterialMesh_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewMesh_ItemSelectionChanged(sender, e);
		}

		private void listViewMaterialTexture_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewTexture_ItemSelectionChanged(sender, e);
		}

		#endregion MaterialView

		#region TextureView

		private void listViewTexture_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (listViewItemSyncSelectedSent == false)
				{
					listViewItemSyncSelectedSent = true;
					listViewTextureMesh.BeginUpdate();
					listViewTextureMaterial.BeginUpdate();

					int id = (int)e.Item.Tag;
					if (e.IsSelected)
					{
						tabControlViews.SelectedTab = tabPageTextureView;
						LoadTexture(id);
						CrossRefAddItem(crossRefTextureMeshes[id], crossRefTextureMeshesCount, listViewTextureMesh, listViewMesh);
						CrossRefAddItem(crossRefTextureMaterials[id], crossRefTextureMaterialsCount, listViewTextureMaterial, listViewMaterial);
					}
					else
					{
						if (id == loadedTexture)
						{
							LoadTexture(-1);
						}
						CrossRefRemoveItem(crossRefTextureMeshes[id], crossRefTextureMeshesCount, listViewTextureMesh);
						CrossRefRemoveItem(crossRefTextureMaterials[id], crossRefTextureMaterialsCount, listViewTextureMaterial);
					}

					CrossRefSetSelected(e.IsSelected, listViewTexture, id);
					CrossRefSetSelected(e.IsSelected, listViewMeshTexture, id);
					CrossRefSetSelected(e.IsSelected, listViewMaterialTexture, id);

					listViewTextureMesh.EndUpdate();
					listViewTextureMaterial.EndUpdate();
					listViewItemSyncSelectedSent = false;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void listViewTextureMesh_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewMesh_ItemSelectionChanged(sender, e);
		}

		private void listViewTextureMaterial_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewMaterial_ItemSelectionChanged(sender, e);
		}

		#endregion TextureView

		#region Frame

		void textBoxFrameName_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}

/*				Gui.Scripting.RunScript(EditorVar + ".SetFrameName(id=" + loadedFrame + ", name=\"" + textBoxFrameName.Text + "\")");

				RecreateRenderObjects();

				xxFrame frame = Editor.Frames[loadedFrame];
				TreeNode node = FindFrameNode(frame, treeViewObjectTree.Nodes);
				node.Text = frame.Name;

				RenameListViewItems(Editor.Meshes, listViewMesh, frame, frame.Name);
				RenameListViewItems(Editor.Meshes, listViewMaterialMesh, frame, frame.Name);
				RenameListViewItems(Editor.Meshes, listViewTextureMesh, frame, frame.Name);*/
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		#endregion Frame

		#region Mesh

		// http://connect.microsoft.com/VisualStudio/feedback/details/151567/datagridviewcomboboxcell-needs-selectedindexchanged-event
		private void dataGridViewMesh_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
		{
			try
			{
				if (!SetComboboxEvent)
				{
					if (e.Control.GetType() == typeof(DataGridViewComboBoxEditingControl))
					{
						ComboBox comboBoxCell = (ComboBox)e.Control;
						if (comboBoxCell != null)
						{
							//Remove an existing event-handler, if present, to avoid
							//adding multiple handlers when the editing control is reused.
							comboBoxCell.SelectedIndexChanged -= new EventHandler(comboBoxCell_SelectedIndexChanged);

							//Add the event handler.
							comboBoxCell.SelectedIndexChanged += new EventHandler(comboBoxCell_SelectedIndexChanged);
							SetComboboxEvent = true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void comboBoxCell_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				ComboBox combo = (ComboBox)sender;

				combo.SelectedIndexChanged -= new EventHandler(comboBoxCell_SelectedIndexChanged);
				SetComboboxEvent = false;

				Tuple<string, int> comboValue = (Tuple<string, int>)combo.SelectedItem;
				if (comboValue == null)
				{
					return;
				}

				int currentCellValueBeforeEndEdit = (int)dataGridViewMesh.CurrentCell.Value;

				dataGridViewMesh.EndEdit();

				int matIdValue = comboValue.Item2;
				if (matIdValue != currentCellValueBeforeEndEdit)
				{
					int rowIdx = dataGridViewMesh.CurrentCell.RowIndex;
//					odfSubmesh submesh = Editor.Parser.MeshSection[loadedMesh][rowIdx];

//					ObjectID newId = new ObjectID(BitConverter.GetBytes(matIdValue));
					Gui.Scripting.RunScript(EditorVar + ".SetSubmeshMaterial(meshIdx=" + loadedMesh + ", submeshIdx=" + rowIdx + ", matId=\"" + matIdValue + "\")");

/*					RecreateRenderObjects();
					RecreateCrossRefs();*/

					dataGridViewMesh.CurrentCell.Value = matIdValue;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		#endregion Mesh

		#region Material

		void textBoxMatName_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedMaterial < 0)
				{
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".SetMaterialName(id=" + loadedMaterial + ", name=\"" + textBoxMatName.Text + "\")");

/*				xxMaterial mat = Editor.Parser.MaterialList[loadedMaterial];
				RenameListViewItems(Editor.Parser.MaterialList, listViewMaterial, mat, mat.Name);
				RenameListViewItems(Editor.Parser.MaterialList, listViewMeshMaterial, mat, mat.Name);
				RenameListViewItems(Editor.Parser.MaterialList, listViewTextureMaterial, mat, mat.Name);

				InitMaterials();
				LoadMaterial(loadedMaterial);*/
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void matTexNameCombo_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedMaterial < 0)
				{
					return;
				}

/*				ComboBox combo = (ComboBox)sender;
				int matTexIdx = (int)combo.Tag;
				string name = (combo.SelectedIndex == 0) ? String.Empty : (string)combo.Items[combo.SelectedIndex];

				Gui.Scripting.RunScript(EditorVar + ".SetMaterialTexture(id=" + loadedMaterial + ", index=" + matTexIdx + ", name=\"" + name + "\")");

				RecreateRenderObjects();
				RecreateCrossRefs();
				LoadMaterial(loadedMaterial);*/
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void matTexNameCombo_TextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedMaterial < 0)
				{
					return;
				}

/*				ComboBox combo = (ComboBox)sender;
				int matTexIdx = (int)combo.Tag;
				string name = combo.Text;

				Gui.Scripting.RunScript(EditorVar + ".SetMaterialTexture(id=" + loadedMaterial + ", index=" + matTexIdx + ", name=\"" + name + "\")");

				RecreateRenderObjects();
				RecreateCrossRefs();
				LoadMaterial(loadedMaterial);*/
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void matMatrixText_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedMaterial < 0)
				{
					return;
				}

/*				xxMaterial mat = Editor.Parser.MaterialList[loadedMaterial];
				Gui.Scripting.RunScript(EditorVar + ".SetMaterialPhong(id=" + loadedMaterial +
					", diffuse=" + MatMatrixColorScript(matMatrixText[0]) +
					", ambient=" + MatMatrixColorScript(matMatrixText[1]) +
					", specular=" + MatMatrixColorScript(matMatrixText[2]) +
					", emissive=" + MatMatrixColorScript(matMatrixText[3]) +
					", shininess=" + Single.Parse(matMatrixText[4][0].Text).ToFloatString() + ")");

				RecreateRenderObjects();
				RecreateCrossRefs();
				LoadMaterial(loadedMaterial);*/
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		string MatMatrixColorScript(EditTextBox[] textBoxes)
		{
			return "{ " +
				Single.Parse(textBoxes[0].Text).ToFloatString() + ", " +
				Single.Parse(textBoxes[1].Text).ToFloatString() + ", " +
				Single.Parse(textBoxes[2].Text).ToFloatString() + ", " +
				Single.Parse(textBoxes[3].Text).ToFloatString() + " }";
		}

		#endregion Material

		#region Texture

		void textBoxTexName_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedTexture < 0)
				{
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".SetTextureName(id=" + loadedTexture + ", name=\"" + textBoxTexName.Text + "\")");

/*				xxTexture tex = Editor.Parser.TextureList[loadedTexture];
				RenameListViewItems(Editor.Parser.TextureList, listViewTexture, tex, tex.Name);
				RenameListViewItems(Editor.Parser.TextureList, listViewMeshTexture, tex, tex.Name);
				RenameListViewItems(Editor.Parser.TextureList, listViewMaterialTexture, tex, tex.Name);

				InitTextures();
				LoadMaterial(loadedMaterial);*/
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		#endregion Texture
	}
}
