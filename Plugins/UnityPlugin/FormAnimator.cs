using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	[Plugin]
	public partial class FormAnimator : DockContent, EditorForm, EditedContent
	{
		private enum MeshExportFormat
		{
			[Description("Metasequoia")]
			Mqo,
			[Description("DirectX (SDK)")]
			DirectXSDK,
			[Description("Collada")]
			Collada,
			[Description("Collada (FBX 2015.1)")]
			ColladaFbx,
			[Description("FBX 2015.1")]
			Fbx,
			[Description("AutoCAD DXF")]
			Dxf,
			[Description("Alias OBJ")]
			Obj,
			[Description("FBX 2006")]
			Fbx_2006
		}

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

		public AnimatorEditor Editor { get; protected set; }
		public string EditorVar { get; protected set; }
		public string ParserVar { get; protected set; }
		public string FormVar { get; protected set; }

		string exportDir;
		FormAnimatorDragDrop dragOptions;

		public string EditorFormVar { get; protected set; }
		TreeNode draggedNode = null;

		[Plugin]
		public TreeNode GetDraggedNode()
		{
			return draggedNode;
		}

		[Plugin]
		public void SetDraggedNode(object[] nodeAddress)
		{
			double[] addr = Utility.Convert<double>(nodeAddress);
			TreeNode node = treeViewObjectTree.Nodes[(int)addr[0]];
			for (int i = 1; i < addr.Length; i++)
			{
				node = node.Nodes[(int)addr[i]];
			}
			draggedNode = node;
		}

		private const Keys MASS_DESTRUCTION_KEY_COMBINATION = Keys.Delete | Keys.Shift;
		private readonly Color MARK_BACKGROUND_COLOR = Color.SteelBlue;

		public FormAnimator(UnityParser uParser, string animatorParserVar)
		{
			try
			{
				InitializeComponent();

				Animator parser = (Animator)Gui.Scripting.Variables[animatorParserVar];

				this.ShowHint = DockState.Document;
				this.Text = parser.m_GameObject.instance.m_Name;
				this.ToolTipText = uParser.FilePath + @"\" + parser.m_GameObject.instance.m_Name;
				this.exportDir = Path.GetDirectoryName(uParser.FilePath) + @"\" + Path.GetFileNameWithoutExtension(uParser.FilePath) + @"\" + Path.GetFileNameWithoutExtension(parser.m_GameObject.instance.m_Name);

				ParserVar = animatorParserVar;
				Gui.Docking.ShowDockContent(this, Gui.Docking.DockEditors, ContentCategory.Meshes);

				EditorVar = Gui.Scripting.GetNextVariable("animatorEditor");
				Editor = (AnimatorEditor)Gui.Scripting.RunScript(EditorVar + " = AnimatorEditor(parser=" + ParserVar + ")");

				Init();
				LoadAnimator();
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

		void CustomDispose()
		{
			if (Text == String.Empty)
			{
				return;
			}

			try
			{
				if (FormVar != null)
				{
					Gui.Scripting.Variables.Remove(ParserVar);
					Gui.Scripting.Variables.Remove(FormVar);
				}
				Gui.Scripting.Variables.Remove(EditorVar);
				if (EditorFormVar != null)
				{
					Gui.Scripting.Variables.Remove(EditorFormVar);
				}

				UnloadAnimator();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void UnloadAnimator()
		{
			dragOptions.Dispose();
			Editor.Dispose();
			Editor = null;
			/*DisposeRenderObjects();
			CrossRefsClear();
			ClearKeyList<xxMaterial>(crossRefMeshMaterials);
			ClearKeyList<xxTexture>(crossRefMeshTextures);
			ClearKeyList<xxFrame>(crossRefMaterialMeshes);
			ClearKeyList<xxTexture>(crossRefMaterialTextures);
			ClearKeyList<xxFrame>(crossRefTextureMeshes);
			ClearKeyList<xxMaterial>(crossRefTextureMaterials);*/
		}

		void DisposeRenderObjects()
		{
			/*foreach (ListViewItem item in listViewMesh.SelectedItems)
			{
				Gui.Renderer.RemoveRenderObject(renderObjectIds[(int)item.Tag]);
				renderObjectIds[(int)item.Tag] = -1;
			}

			for (int i = 0; i < renderObjectMeshes.Count; i++)
			{
				if (renderObjectMeshes[i] != null)
				{
					renderObjectMeshes[i].Dispose();
					renderObjectMeshes[i] = null;
				}
			}*/
		}

		void ClearKeyList<T>(Dictionary<int, List<KeyList<T>>> dic)
		{
			foreach (var pair in dic)
			{
				pair.Value.Clear();
			}
		}

		void Init()
		{
			float treeViewFontSize = (float)Gui.Config["TreeViewFontSize"];
			if (treeViewFontSize > 0)
			{
				treeViewObjectTree.Font = new System.Drawing.Font(treeViewObjectTree.Font.Name, treeViewFontSize);
			}
			float listViewFontSize = (float)Gui.Config["ListViewFontSize"];
			if (listViewFontSize > 0)
			{
				listViewMesh.Font = new System.Drawing.Font(listViewMesh.Font.Name, listViewFontSize);
				listViewMeshMaterial.Font = new System.Drawing.Font(listViewMeshMaterial.Font.Name, listViewFontSize);
				listViewMeshTexture.Font = new System.Drawing.Font(listViewMeshMaterial.Font.Name, listViewFontSize);
				listViewMaterial.Font = new System.Drawing.Font(listViewMaterial.Font.Name, listViewFontSize);
				listViewMaterialMesh.Font = new System.Drawing.Font(listViewMaterialMesh.Font.Name, listViewFontSize);
				listViewMaterialTexture.Font = new System.Drawing.Font(listViewMaterialTexture.Font.Name, listViewFontSize);
				listViewTexture.Font = new System.Drawing.Font(listViewTexture.Font.Name, listViewFontSize);
				listViewTextureMesh.Font = new System.Drawing.Font(listViewTextureMesh.Font.Name, listViewFontSize);
				listViewTextureMaterial.Font = new System.Drawing.Font(listViewTextureMaterial.Font.Name, listViewFontSize);
			}

			//panelTexturePic.Resize += new EventHandler(panelTexturePic_Resize);
			splitContainer1.Panel2MinSize = tabControlViews.Width;

			/*matTexNameCombo = new ComboBox[4] { comboBoxMatTex1, comboBoxMatTex2, comboBoxMatTex3, comboBoxMatTex4 };

			matMatrixText[0] = new EditTextBox[4] { textBoxMatDiffuseR, textBoxMatDiffuseG, textBoxMatDiffuseB, textBoxMatDiffuseA };
			matMatrixText[1] = new EditTextBox[4] { textBoxMatAmbientR, textBoxMatAmbientG, textBoxMatAmbientB, textBoxMatAmbientA };
			matMatrixText[2] = new EditTextBox[4] { textBoxMatSpecularR, textBoxMatSpecularG, textBoxMatSpecularB, textBoxMatSpecularA };
			matMatrixText[3] = new EditTextBox[4] { textBoxMatEmissiveR, textBoxMatEmissiveG, textBoxMatEmissiveB, textBoxMatEmissiveA };
			matMatrixText[4] = new EditTextBox[1] { textBoxMatSpecularPower };*/

			DataGridViewEditor.InitDataGridViewSRT(dataGridViewFrameSRT, dataGridViewFrameMatrix);
			DataGridViewEditor.InitDataGridViewMatrix(dataGridViewFrameMatrix, dataGridViewFrameSRT);
			DataGridViewEditor.InitDataGridViewSRT(dataGridViewBoneSRT, dataGridViewBoneMatrix);
			DataGridViewEditor.InitDataGridViewMatrix(dataGridViewBoneMatrix, dataGridViewBoneSRT);

			/*textBoxFrameName.AfterEditTextChanged += new EventHandler(textBoxFrameName_AfterEditTextChanged);
			textBoxFrameName2.AfterEditTextChanged += new EventHandler(textBoxFrameName2_AfterEditTextChanged);
			textBoxBoneName.AfterEditTextChanged += new EventHandler(textBoxBoneName_AfterEditTextChanged);
			textBoxMatName.AfterEditTextChanged += new EventHandler(textBoxMatName_AfterEditTextChanged);
			textBoxTexName.AfterEditTextChanged += new EventHandler(textBoxTexName_AfterEditTextChanged);*/

			ColumnSubmeshMaterial.DisplayMember = "Item1";
			ColumnSubmeshMaterial.ValueMember = "Item2";
			ColumnSubmeshMaterial.DefaultCellStyle.NullValue = "(invalid)";

			/*for (int i = 0; i < matMatrixText.Length; i++)
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
			}*/

			MeshExportFormat[] values = Enum.GetValues(typeof(MeshExportFormat)) as MeshExportFormat[];
			string[] descriptions = new string[values.Length];
			for (int i = 0; i < descriptions.Length; i++)
			{
				descriptions[i] = values[i].GetDescription();
			}
			comboBoxMeshExportFormat.Items.AddRange(descriptions);
			comboBoxMeshExportFormat.SelectedItem = Gui.Config["MeshExportFormat"];

			/*checkBoxMeshExportMqoSortMeshes.Checked = (bool)Gui.Config["ExportMqoSortMeshes"];
			checkBoxMeshExportMqoSortMeshes.CheckedChanged += checkBoxMeshExportMqoSortMeshes_CheckedChanged;*/
		}

		void LoadAnimator()
		{
			/*renderObjectMeshes = new List<RenderObjectAnimator>(new RenderObjectAnimator[Editor.Meshes.Count]);
			renderObjectIds = new List<int>(Editor.Meshes.Count);
			for (int i = 0; i < Editor.Meshes.Count; i++)
			{
				renderObjectIds.Add(-1);
			}*/

			InitFrames();
			InitMeshes();
			InitMaterials();
			InitTextures();

			//RecreateCrossRefs();

			dragOptions = new FormAnimatorDragDrop(Editor);
		}

		void InitFrames()
		{
			TreeNode objRootNode = CreateFrameTree(Editor.Parser.RootTransform, null);

			string selectedNodeText = null;
			Type selectedNodeType = null;
			if (treeViewObjectTree.SelectedNode != null)
			{
				selectedNodeText = treeViewObjectTree.SelectedNode.Text;
				if (treeViewObjectTree.SelectedNode.Tag != null)
				{
					selectedNodeType = ((DragSource)treeViewObjectTree.SelectedNode.Tag).Type;
				}
			}
			HashSet<string> expandedNodes = ExpandedNodes(treeViewObjectTree);

			if (treeViewObjectTree.Nodes.Count > 0)
			{
				treeViewObjectTree.Nodes.RemoveAt(0);
			}
			treeViewObjectTree.Nodes.Insert(0, objRootNode);
			if (dragOptions != null)
			{
				dragOptions.numericFrameId.Maximum = Editor.Frames.Count - 1;
				dragOptions.numericMeshId.Maximum = Editor.Frames.Count - 1;
			}

			ExpandNodes(treeViewObjectTree, expandedNodes);
			if (selectedNodeText != null)
			{
				TreeNode newNode = FindFrameNode(selectedNodeText, treeViewObjectTree.Nodes);
				if (newNode != null)
				{
					Type newType = null;
					if (newNode.Tag != null)
					{
						newType = ((DragSource)newNode.Tag).Type;
					}
					if (selectedNodeType == newType)
					{
						newNode.EnsureVisible();
						treeViewObjectTree.SelectedNode = newNode;
					}
				}
			}
		}

		private TreeNode CreateFrameTree(Transform frame, TreeNode parentNode)
		{
			TreeNode newNode = new TreeNode(frame.m_GameObject.instance.m_Name);
			newNode.Tag = new DragSource(EditorVar, typeof(Transform), Editor.Frames.IndexOf(frame));

			SkinnedMeshRenderer frameMesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
			if (frameMesh != null)
			{
				int meshId = Editor.Meshes.IndexOf(frameMesh);
				TreeNode meshNode = new TreeNode("SkinnedMeshRenderer");
				meshNode.Tag = new DragSource(EditorVar, typeof(SkinnedMeshRenderer), meshId);
				newNode.Nodes.Add(meshNode);

				if (frameMesh.m_Bones.Count > 0)
				{
					TreeNode boneListNode = new TreeNode(frameMesh.m_Bones.Count + " Bones");
					meshNode.Nodes.Add(boneListNode);
					for (int i = 0; i < frameMesh.m_Bones.Count; i++)
					{
						Transform bone = frameMesh.m_Bones[i].instance;
						TreeNode boneNode;
						if (bone != null)
						{
							boneNode = new TreeNode(bone.m_GameObject.instance.m_Name);
						}
						else
						{
							boneNode = new TreeNode("invalid bone");
							boneNode.ForeColor = Color.OrangeRed;
						}
						boneNode.Tag = new DragSource(EditorVar, typeof(Matrix), new int[] { meshId, i });
						boneListNode.Nodes.Add(boneNode);
					}
				}
			}

			if (parentNode != null)
			{
				parentNode.Nodes.Add(newNode);
			}
			for (int i = 0; i < frame.Count; i++)
			{
				CreateFrameTree(frame[i], newNode);
			}

			return newNode;
		}

		private HashSet<string> ExpandedNodes(TreeView tree)
		{
			HashSet<string> nodes = new HashSet<string>();
			TreeNode root = new TreeNode();
			while (tree.Nodes.Count > 0)
			{
				TreeNode node = tree.Nodes[0];
				node.Remove();
				root.Nodes.Add(node);
			}
			FindExpandedNodes(root, nodes);
			while (root.Nodes.Count > 0)
			{
				TreeNode node = root.Nodes[0];
				node.Remove();
				tree.Nodes.Add(node);
			}
			return nodes;
		}

		private void FindExpandedNodes(TreeNode parent, HashSet<string> result)
		{
			foreach (TreeNode node in parent.Nodes)
			{
				if (node.IsExpanded)
				{
					string parentString = parent.Text == "SkinnedMeshRenderer" ? parent.Parent.Text : parent.Text;
					result.Add(parentString + "/" + node.Text);
				}
				FindExpandedNodes(node, result);
			}
		}

		private void ExpandNodes(TreeView tree, HashSet<string> nodes)
		{
			TreeNode root = new TreeNode();
			while (tree.Nodes.Count > 0)
			{
				TreeNode node = tree.Nodes[0];
				node.Remove();
				root.Nodes.Add(node);
			}
			FindNodesToExpand(root, nodes);
			while (root.Nodes.Count > 0)
			{
				TreeNode node = root.Nodes[0];
				node.Remove();
				tree.Nodes.Add(node);
			}
		}

		private void FindNodesToExpand(TreeNode parent, HashSet<string> nodes)
		{
			foreach (TreeNode node in parent.Nodes)
			{
				string parentString = parent.Text == "SkinnedMeshRenderer" ? parent.Parent.Text : parent.Text;
				if (nodes.Contains(parentString + "/" + node.Text))
				{
					node.Expand();
				}
				FindNodesToExpand(node, nodes);
			}
		}

		void InitMeshes()
		{
			ListViewItem[] meshItems = new ListViewItem[Editor.Meshes.Count];
			for (int i = 0; i < Editor.Meshes.Count; i++)
			{
				SkinnedMeshRenderer sMesh = Editor.Meshes[i];
				meshItems[i] = new ListViewItem(sMesh.m_GameObject.instance.m_Name);
				meshItems[i].Tag = i;
			}
			listViewMesh.Items.Clear();
			listViewMesh.Items.AddRange(meshItems);
			meshlistHeader.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
		}

		void InitMaterials()
		{
			HashSet<string> selectedItems = new HashSet<string>();
			foreach (ListViewItem item in listViewMaterial.SelectedItems)
			{
				selectedItems.Add(item.Text);
			}
			List<Tuple<string, int>> columnMaterials = new List<Tuple<string, int>>(Editor.Materials.Count);
			ListViewItem[] materialItems = new ListViewItem[Editor.Materials.Count];
			for (int i = 0; i < Editor.Materials.Count; i++)
			{
				Material mat = Editor.Materials[i];
				materialItems[i] = new ListViewItem(mat.m_Name);
				materialItems[i].Tag = i;

				columnMaterials.Add(new Tuple<string, int>(mat.m_Name, i));
			}
			listViewMaterial.Items.Clear();
			listViewMaterial.Items.AddRange(materialItems);
			materiallistHeader.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			if (selectedItems.Count > 0)
			{
				//listViewMaterial.ItemSelectionChanged -= listViewMaterial_ItemSelectionChanged;
				listViewMaterial.BeginUpdate();
				foreach (ListViewItem item in listViewMaterial.Items)
				{
					if (selectedItems.Contains(item.Text))
					{
						item.Selected = true;
					}
				}
				listViewMaterial.EndUpdate();
				//listViewMaterial.ItemSelectionChanged += listViewMaterial_ItemSelectionChanged;
			}

			ColumnSubmeshMaterial.DataSource = columnMaterials;
			//SetComboboxEvent = false;

			TreeNode materialsNode = new TreeNode("Materials");
			for (int i = 0; i < Editor.Materials.Count; i++)
			{
				TreeNode matNode = new TreeNode(Editor.Materials[i].m_Name);
				matNode.Tag = new DragSource(EditorVar, typeof(Material), i);
				materialsNode.Nodes.Add(matNode);
			}

			if (treeViewObjectTree.Nodes.Count > 1)
			{
				if (treeViewObjectTree.Nodes[1].IsExpanded)
				{
					materialsNode.Expand();
				}
				treeViewObjectTree.Nodes.RemoveAt(1);
			}
			treeViewObjectTree.Nodes.Insert(1, materialsNode);
		}

		void InitTextures()
		{
			/*for (int i = 0; i < matTexNameCombo.Length; i++)
			{
				matTexNameCombo[i].Items.Clear();
				matTexNameCombo[i].Items.Add("(none)");
			}*/

			HashSet<string> selectedItems = new HashSet<string>();
			foreach (ListViewItem item in listViewTexture.SelectedItems)
			{
				selectedItems.Add(item.Text);
			}
			ListViewItem[] textureItems = new ListViewItem[Editor.Textures.Count];
			for (int i = 0; i < Editor.Textures.Count; i++)
			{
				Texture2D tex = Editor.Textures[i];
				textureItems[i] = new ListViewItem(tex.m_Name);
				textureItems[i].Tag = i;
				/*for (int j = 0; j < matTexNameCombo.Length; j++)
				{
					matTexNameCombo[j].Items.Add(tex.m_Name);
				}*/
			}
			listViewTexture.Items.Clear();
			listViewTexture.Items.AddRange(textureItems);
			texturelistHeader.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			if (selectedItems.Count > 0)
			{
				//listViewTexture.ItemSelectionChanged -= listViewTexture_ItemSelectionChanged;
				listViewTexture.BeginUpdate();
				foreach (ListViewItem item in listViewTexture.Items)
				{
					if (selectedItems.Contains(item.Text))
					{
						item.Selected = true;
					}
				}
				listViewTexture.EndUpdate();
				//listViewTexture.ItemSelectionChanged += listViewTexture_ItemSelectionChanged;
			}

			TreeNode texturesNode = new TreeNode("Textures");
			TreeNode currentTexture = null;
			for (int i = 0; i < Editor.Textures.Count; i++)
			{
				TreeNode texNode = new TreeNode(Editor.Textures[i].m_Name);
				texNode.Tag = new DragSource(EditorVar, typeof(Texture2D), i);
				texturesNode.Nodes.Add(texNode);
				/*if (loadedTexture == i)
					currentTexture = texNode;*/
			}

			if (treeViewObjectTree.Nodes.Count > 2)
			{
				if (treeViewObjectTree.Nodes[2].IsExpanded)
				{
					texturesNode.Expand();
				}
				treeViewObjectTree.Nodes.RemoveAt(2);
			}
			treeViewObjectTree.Nodes.Insert(2, texturesNode);
			if (currentTexture != null)
				currentTexture.EnsureVisible();
		}

		TreeNode FindFrameNode(string name, TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				var source = node.Tag as DragSource?;
				if (source == null || source.Value.Type != typeof(Transform) && source.Value.Type != typeof(SkinnedMeshRenderer))
				{
					return null;
				}

				if (Editor.Frames[(int)source.Value.Id].m_GameObject.instance.m_Name == name)
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

		TreeNode FindFrameNode(Transform frame, TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				var source = node.Tag as DragSource?;
				if (source == null)
				{
					return null;
				}

				if (source.Value.Type == typeof(xxFrame))
				{
					if (Editor.Frames[(int)source.Value.Id].Equals(frame))
					{
						return node;
					}

					TreeNode found = FindFrameNode(frame, node.Nodes);
					if (found != null)
					{
						return found;
					}
				}
			}

			return null;
		}

		TreeNode FindBoneNode(Transform bone, TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				var source = node.Tag as DragSource?;

				if ((source != null) && (source.Value.Type == typeof(Matrix)))
				{
					var id = (int[])source.Value.Id;
					if (Editor.Meshes[id[0]].m_Bones[id[1]].instance.Equals(bone))
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

		TreeNode FindMaterialNode(string name)
		{
			foreach (TreeNode node in treeViewObjectTree.Nodes[1].Nodes)
			{
				var source = node.Tag as DragSource?;
				if (Editor.Materials[(int)source.Value.Id].m_Name == name)
				{
					return node;
				}
			}

			return null;
		}

		TreeNode FindMaterialNode(Material mat)
		{
			foreach (TreeNode node in treeViewObjectTree.Nodes[1].Nodes)
			{
				var source = node.Tag as DragSource?;
				if (Editor.Materials[(int)source.Value.Id].Equals(mat))
				{
					return node;
				}
			}

			return null;
		}

		TreeNode FindTextureNode(string name)
		{
			foreach (TreeNode node in treeViewObjectTree.Nodes[2].Nodes)
			{
				var source = node.Tag as DragSource?;
				if (Editor.Textures[(int)source.Value.Id].m_Name == name)
				{
					return node;
				}
			}

			return null;
		}

		TreeNode FindTextureNode(xxTexture tex)
		{
			foreach (TreeNode node in treeViewObjectTree.Nodes[2].Nodes)
			{
				var source = node.Tag as DragSource?;
				if (Editor.Textures[(int)source.Value.Id].Equals(tex))
				{
					return node;
				}
			}

			return null;
		}

		private void treeViewObjectTree_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				if (e.Item is TreeNode)
				{
					SkinnedMeshRenderer draggedMesh = null;
					TreeNode draggedItem = (TreeNode)e.Item;
					if (draggedItem.Tag is DragSource)
					{
						DragSource src = (DragSource)draggedItem.Tag;
						if (src.Type == typeof(SkinnedMeshRenderer))
						{
							draggedItem = ((TreeNode)e.Item).Parent;
							draggedMesh = Editor.Meshes[(int)src.Id];
						}
						else if (src.Type == typeof(Matrix))
						{
							SkinnedMeshRenderer mesh = Editor.Meshes[((int[])src.Id)[0]];
							Transform bone = mesh.m_Bones[((int[])src.Id)[1]].instance;
							draggedItem = FindFrameNode(bone.m_GameObject.instance.m_Name, treeViewObjectTree.Nodes);
						}
					}

					if ((bool)Gui.Config["WorkspaceScripting"])
					{
						if (EditorFormVar == null)
						{
							EditorFormVar = Gui.Scripting.GetNextVariable("EditorFormVar");
							Gui.Scripting.RunScript(EditorFormVar + " = SearchEditorForm(\"" + this.ToolTipText + "\")");
						}
						SetDraggedNode(draggedItem);
					}

					treeViewObjectTree.DoDragDrop(draggedItem, DragDropEffects.Copy);

					if (draggedMesh != null && draggedMesh.m_Mesh != null && Editor.Materials.Count > 0)
					{
						HashSet<int> matIndices = new HashSet<int>();
						HashSet<int> texIndices = new HashSet<int>();
						foreach (PPtr<Material> matPtr in draggedMesh.m_Materials)
						{
							Material mat = matPtr.instance;
							matIndices.Add(Editor.Materials.IndexOf(mat));
							foreach (var matTex in mat.m_SavedProperties.m_TexEnvs)
							{
								Texture2D tex = matTex.Value.m_Texture.instance;
								if (tex != null)
								{
									int texId = Editor.Textures.IndexOf(tex);
									texIndices.Add(texId);
								}
							}
						}

						TreeNode materialsNode = treeViewObjectTree.Nodes[1];
						foreach (TreeNode matNode in materialsNode.Nodes)
						{
							DragSource src = (DragSource)matNode.Tag;
							if (matIndices.Contains((int)src.Id))
							{
								ItemDragEventArgs args = new ItemDragEventArgs(MouseButtons.None, matNode);
								treeViewObjectTree_ItemDrag(null, args);
							}
						}
						TreeNode texturesNode = treeViewObjectTree.Nodes[2];
						foreach (TreeNode texNode in texturesNode.Nodes)
						{
							DragSource src = (DragSource)texNode.Tag;
							if (texIndices.Contains((int)src.Id))
							{
								ItemDragEventArgs args = new ItemDragEventArgs(MouseButtons.None, texNode);
								treeViewObjectTree_ItemDrag(null, args);
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

		public void SetDraggedNode(TreeNode node)
		{
			StringBuilder nodeAddress = new StringBuilder(100);
			while (node != null)
			{
				string delim;
				int idx;
				if (node.Parent != null)
				{
					idx = node.Parent.Nodes.IndexOf(node);
					delim = ", ";
				}
				else
				{
					idx = treeViewObjectTree.Nodes.IndexOf(node);
					delim = "{";
				}
				nodeAddress.Insert(0, idx);
				nodeAddress.Insert(0, delim);
				node = node.Parent;
			}
			nodeAddress.Append("}");
			Gui.Scripting.RunScript(EditorFormVar + ".SetDraggedNode(nodeAddress=" + nodeAddress + ")");
		}

		private void treeViewObjectTree_DragEnter(object sender, DragEventArgs e)
		{
			try
			{
				TreeNode node = (TreeNode)e.Data.GetData(typeof(TreeNode));
				if (node != null)
				{
					MarkEmptyDropZone();
				}
				UpdateDragDrop(sender, e);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void MarkEmptyDropZone()
		{
			treeViewObjectTree.BackColor = MARK_BACKGROUND_COLOR;
			SetBackColor(treeViewObjectTree.Nodes, Color.White);
		}

		private void SetBackColor(TreeNodeCollection nodes, Color col)
		{
			foreach (TreeNode node in nodes)
			{
				node.BackColor = col;
				SetBackColor(node.Nodes, col);
			}
		}

		private void treeViewObjectTree_DragLeave(object sender, EventArgs e)
		{
			UnmarkEmptyDropZone();
		}

		private void UnmarkEmptyDropZone()
		{
			treeViewObjectTree.BackColor = Color.White;
		}

		TreeNode lastSelectedNode = null;

		private void treeViewObjectTree_DrawNode(object sender, DrawTreeNodeEventArgs e)
		{
			if ((e.State & TreeNodeStates.Selected) != 0)
			{
				lastSelectedNode = e.Node;
				if (treeViewObjectTree.BackColor == MARK_BACKGROUND_COLOR)
				{
					treeViewObjectTree.BackColor = Color.White;
				}
			}
			else if (e.Node == lastSelectedNode && !e.Node.IsSelected && lastSelectedNode.BackColor == Color.White)
			{
				lastSelectedNode.TreeView.BackColor = MARK_BACKGROUND_COLOR;
				lastSelectedNode = null;
			}
			e.DrawDefault = true;
		}

		private void treeViewObjectTree_DragOver(object sender, DragEventArgs e)
		{
			try
			{
				UpdateDragDrop(sender, e);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeViewObjectTree_DragDrop(object sender, DragEventArgs e)
		{
			try
			{
				TreeNode node = (TreeNode)e.Data.GetData(typeof(TreeNode));
				if (node == null)
				{
					Gui.Docking.DockDragDrop(sender, e);
				}
				else
				{
					ProcessDragDropSources(node);
					dragOptions.checkBoxOkContinue.Checked = false;
				}
				UnmarkEmptyDropZone();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void ProcessDragDropSources(TreeNode node)
		{
			if (node.Tag is DragSource)
			{
				if ((node.Parent != null) && !node.Checked && node.StateImageIndex != (int)CheckState.Indeterminate)
				{
					return;
				}

				DragSource? dest = null;
				if (treeViewObjectTree.SelectedNode != null)
				{
					dest = treeViewObjectTree.SelectedNode.Tag as DragSource?;
				}

				DragSource source = (DragSource)node.Tag;
				if (source.Type == typeof(Transform))
				{
					dragOptions.ShowPanel(true);
					if (!dragOptions.checkBoxFrameDestinationLock.Checked)
					{
						var srcEditor = (AnimatorEditor)Gui.Scripting.Variables[source.Variable];
						var srcFrameName = srcEditor.Frames[(int)source.Id].m_GameObject.instance.m_Name;
						dragOptions.numericFrameId.Value = GetDestParentId(srcFrameName, dest);
					}
					if (dragOptions.checkBoxOkContinue.Checked || dragOptions.ShowDialog() == DialogResult.OK)
					{
						Gui.Scripting.RunScript(EditorVar + "." + dragOptions.FrameMethod.GetName() + "(srcFrame=" + source.Variable + ".Frames[" + (int)source.Id + "], srcMaterials=" + source.Variable + ".Materials, srcTextures=" + source.Variable + ".Textures, appendIfMissing=" + dragOptions.checkBoxFrameAppend.Checked + ", destParentId=" + dragOptions.numericFrameId.Value + ")");
						Changed = Changed;
						InitMaterials();
						InitTextures();
						RecreateFrames();
						SyncWorkspaces();
					}
				}
				else if (source.Type == typeof(Material))
				{
					Gui.Scripting.RunScript(EditorVar + ".MergeMaterial(mat=" + source.Variable + ".Materials[" + (int)source.Id + "])");
					Changed = Changed;
					RecreateMaterials();
				}
				else if (source.Type == typeof(Texture2D))
				{
					Gui.Scripting.RunScript(EditorVar + ".MergeTexture(tex=" + source.Variable + ".Textures[" + (int)source.Id + "])");
					Changed = Changed;
					RecreateTextures();
				}
				else if (source.Type == typeof(ImportedFrame))
				{
					dragOptions.ShowPanel(true);
					if (!dragOptions.checkBoxFrameDestinationLock.Checked)
					{
						var srcEditor = (ImportedEditor)Gui.Scripting.Variables[source.Variable];
						var srcFrameName = srcEditor.Frames[(int)source.Id].Name;
						dragOptions.numericFrameId.Value = GetDestParentId(srcFrameName, dest);
					}
					if (dragOptions.checkBoxOkContinue.Checked || dragOptions.ShowDialog() == DialogResult.OK)
					{
						try
						{
							Gui.Scripting.RunScript(EditorVar + "." + dragOptions.FrameMethod.GetName() + "(srcFrame=" + source.Variable + ".Frames[" + (int)source.Id + "], destParentId=" + dragOptions.numericFrameId.Value + ")");
							Changed = Changed;
							RecreateFrames();
							SyncWorkspaces();
						}
						catch (Exception e)
						{
							Report.ReportLog(e.ToString());
						}
					}
				}
				else if (source.Type == typeof(WorkspaceMesh))
				{
					dragOptions.ShowPanel(false);
					var srcEditor = (ImportedEditor)Gui.Scripting.Variables[source.Variable];

					if (!dragOptions.checkBoxMeshDestinationLock.Checked)
					{
						int destFrameId = -1;
						if (treeViewObjectTree.SelectedNode != null)
						{
							destFrameId = GetDestParentId(treeViewObjectTree.SelectedNode.Text, dest);
						}
						if (destFrameId < 0)
						{
							destFrameId = Editor.GetTransformId(srcEditor.Imported.MeshList[(int)source.Id].Name);
							if (destFrameId < 0)
							{
								destFrameId = 0;
							}
						}
						dragOptions.numericMeshId.Value = destFrameId;
					}

					if (!dragOptions.checkBoxMeshNormalsLock.Checked || !dragOptions.checkBoxMeshBonesLock.Checked)
					{
						bool normalsCopyNear = false;
						bool bonesCopyNear = false;
						if (srcEditor.Meshes != null)
						{
							normalsCopyNear = true;
							bonesCopyNear = true;
							foreach (ImportedMesh mesh in srcEditor.Meshes)
							{
								foreach (ImportedSubmesh submesh in mesh.SubmeshList)
								{
									foreach (ImportedVertex vert in submesh.VertexList)
									{
										if (vert.Normal.X != 0f || vert.Normal.Y != 0f || vert.Normal.Z != 0f)
										{
											normalsCopyNear = false;
											break;
										}
									}
								}
								if (mesh.BoneList != null && mesh.BoneList.Count > 0)
								{
									bonesCopyNear = false;
								}
							}
						}
						if (!dragOptions.checkBoxMeshNormalsLock.Checked)
						{
							if (normalsCopyNear)
							{
								dragOptions.radioButtonNormalsCopyNear.Checked = true;
							}
							else
							{
								dragOptions.radioButtonNormalsReplace.Checked = true;
							}
						}
						if (!dragOptions.checkBoxMeshBonesLock.Checked)
						{
							if (bonesCopyNear)
							{
								dragOptions.radioButtonBonesCopyNear.Checked = true;
							}
							else
							{
								dragOptions.radioButtonBonesReplace.Checked = true;
							}
						}
					}

					if (dragOptions.checkBoxOkContinue.Checked || dragOptions.ShowDialog() == DialogResult.OK)
					{
						// repeating only final choices for repeatability of the script
						WorkspaceMesh wsMesh = srcEditor.Meshes[(int)source.Id];
						foreach (ImportedSubmesh submesh in wsMesh.SubmeshList)
						{
							if (wsMesh.isSubmeshEnabled(submesh))
							{
								if (!wsMesh.isSubmeshReplacingOriginal(submesh))
								{
									Gui.Scripting.RunScript(source.Variable + ".setSubmeshReplacingOriginal(meshId=" + (int)source.Id + ", id=" + wsMesh.SubmeshList.IndexOf(submesh) + ", replaceOriginal=false)");
								}
							}
							else
							{
								Gui.Scripting.RunScript(source.Variable + ".setSubmeshEnabled(meshId=" + (int)source.Id + ", id=" + wsMesh.SubmeshList.IndexOf(submesh) + ", enabled=false)");
							}
						}
						Gui.Scripting.RunScript(EditorVar + ".ReplaceSkinnedMeshRenderer(mesh=" + source.Variable + ".Meshes[" + (int)source.Id + "], frameId=" + dragOptions.numericMeshId.Value + ", rootBoneId=-1, merge=" + dragOptions.radioButtonMeshMerge.Checked + ", normals=\"" + dragOptions.NormalsMethod.GetName() + "\", bones=\"" + dragOptions.BonesMethod.GetName() + "\")");
						Changed = Changed;
						RecreateMeshes();
					}
				}
				else if (source.Type == typeof(ImportedMaterial))
				{
					Gui.Scripting.RunScript(EditorVar + ".MergeMaterial(mat=" + source.Variable + ".Imported.MaterialList[" + (int)source.Id + "])");
					Changed = Changed;
					RecreateMaterials();
				}
				else if (source.Type == typeof(ImportedTexture))
				{
					Gui.Scripting.RunScript(EditorVar + ".MergeTexture(tex=" + source.Variable + ".Imported.TextureList[" + (int)source.Id + "])");
					Changed = Changed;
					RecreateTextures();
				}
			}
			else
			{
				foreach (TreeNode child in node.Nodes)
				{
					ProcessDragDropSources(child);
				}
			}
		}

		public void SyncWorkspaces()
		{
			SyncWorkspaces(null, null, 0);
		}

		public void SyncWorkspaces(string newName, Type type, int id)
		{
			List<DockContent> formWorkspaceList = FormWorkspace.GetWorkspacesOfForm(this);
			if (formWorkspaceList == null)
			{
				return;
			}
			foreach (FormWorkspace workspace in formWorkspaceList)
			{
				List<TreeNode> formNodes = workspace.ChildForms[this];
				List<TreeNode> removeNodes = new List<TreeNode>();
				HashSet<string> expanded = null;
				TreeView wsTreeView = null;
				foreach (TreeNode node in formNodes)
				{
					if (!NodeIsValid(node))
					{
						if (newName != null)
						{
							DragSource ds = (DragSource)node.Tag;
							if (ds.Type == type && (int)ds.Id == id)
							{
								node.Text = newName;
								continue;
							}
						}
						removeNodes.Add(node);
						if (expanded == null)
						{
							wsTreeView = node.TreeView;
							expanded = ExpandedNodes(wsTreeView);
						}
					}
				}
				foreach (TreeNode node in removeNodes)
				{
					workspace.RemoveNode(node);
					TreeNode newNode = null;
					DragSource ds = (DragSource)node.Tag;
					if (ds.Type == typeof(Transform))
					{
						newNode = FindFrameNode(node.Text, treeViewObjectTree.Nodes[0].Nodes);
					}
					else if (ds.Type == typeof(Material))
					{
						newNode = FindMaterialNode(node.Text);
					}
					else if (ds.Type == typeof(Texture2D))
					{
						newNode = FindTextureNode(node.Text);
					}
					if (newNode != null)
					{
						workspace.AddNode(newNode);
					}
				}
				if (expanded != null)
				{
					ExpandNodes(wsTreeView, expanded);
				}
			}
		}

		private bool NodeIsValid(TreeNode node)
		{
			DragSource ds = (DragSource)node.Tag;
			if (ds.Type == typeof(Transform))
			{
				int frameId = (int)ds.Id;
				if (frameId >= Editor.Frames.Count)
				{
					return false;
				}
				Transform nodeFrame = Editor.Frames[frameId];
				int realChilds = 0;
				foreach (TreeNode child in node.Nodes)
				{
					if (child.Tag != null)
					{
						if (((DragSource)child.Tag).Type == typeof(Transform))
						{
							realChilds++;
						}
					}
				}
				if (nodeFrame.m_GameObject.instance.m_Name != node.Text || nodeFrame.Count != realChilds)
				{
					return false;
				}
				foreach (TreeNode child in node.Nodes)
				{
					if (!NodeIsValid(child))
					{
						return false;
					}
				}
			}
			else if (ds.Type == typeof(Material))
			{
				int matId = (int)ds.Id;
				if (matId >= Editor.Materials.Count)
				{
					return false;
				}
				Material nodeMaterial = Editor.Materials[matId];
				if (nodeMaterial.m_Name != node.Text)
				{
					return false;
				}
			}
			else if (ds.Type == typeof(Texture2D))
			{
				int texId = (int)ds.Id;
				if (texId >= Editor.Textures.Count)
				{
					return false;
				}
				Texture2D nodeTexture = Editor.Textures[texId];
				if (nodeTexture.m_Name != node.Text)
				{
					return false;
				}
			}

			return true;
		}

		public void RecreateFrames()
		{
			/*CrossRefsClear();
			DisposeRenderObjects();
			LoadFrame(-1);
			LoadMesh(-1);*/
			InitFrames();
			InitMeshes();
			/*RecreateRenderObjects();
			RecreateCrossRefs();*/
		}

		private void RecreateMeshes()
		{
			/*CrossRefsClear();
			DisposeRenderObjects();
			LoadMesh(-1);*/
			InitFrames();
			InitMeshes();
			/*RecreateRenderObjects();
			RecreateCrossRefs();*/
		}

		private void RecreateMaterials()
		{
			/*CrossRefsClear();
			DisposeRenderObjects();
			LoadMaterial(-1);*/
			InitMaterials();
			/*RecreateRenderObjects();
			RecreateCrossRefs();
			LoadMesh(loadedMesh);*/
		}

		private void RecreateTextures()
		{
			/*CrossRefsClear();
			DisposeRenderObjects();
			LoadTexture(-1);*/
			InitTextures();
			/*RecreateRenderObjects();
			RecreateCrossRefs();
			LoadMaterial(loadedMaterial);*/
		}

		private int GetDestParentId(string srcFrameName, DragSource? dest)
		{
			int destParentId = -1;
			if (dest == null)
			{
				var destFrameId = Editor.GetTransformId(srcFrameName);
				if (destFrameId >= 0)
				{
					var destFrameParent = Editor.Frames[destFrameId].Parent;
					if (destFrameParent != null)
					{
						for (int i = 0; i < Editor.Frames.Count; i++)
						{
							if (Editor.Frames[i] == destFrameParent)
							{
								destParentId = i;
								break;
							}
						}
					}
				}
			}
			else if (dest.Value.Type == typeof(Transform))
			{
				destParentId = (int)dest.Value.Id;
			}

			return destParentId;
		}

		private void UpdateDragDrop(object sender, DragEventArgs e)
		{
			Point p = treeViewObjectTree.PointToClient(new Point(e.X, e.Y));
			TreeNode target = treeViewObjectTree.GetNodeAt(p);
			if ((target != null) && ((p.X < target.Bounds.Left) || (p.X > target.Bounds.Right) || (p.Y < target.Bounds.Top) || (p.Y > target.Bounds.Bottom)))
			{
				target = null;
			}
			treeViewObjectTree.SelectedNode = target;

			TreeNode node = (TreeNode)e.Data.GetData(typeof(TreeNode));
			if (node == null)
			{
				Gui.Docking.DockDragEnter(sender, e);
			}
			else
			{
				e.Effect = e.AllowedEffect & DragDropEffects.Copy;
			}
		}

		private void buttonObjectTreeExpand_Click(object sender, EventArgs e)
		{
			try
			{
				treeViewObjectTree.BeginUpdate();
				treeViewObjectTree.ExpandAll();
				treeViewObjectTree.EndUpdate();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonObjectTreeCollapse_Click(object sender, EventArgs e)
		{
			try
			{
				treeViewObjectTree.BeginUpdate();
				treeViewObjectTree.CollapseAll();
				treeViewObjectTree.EndUpdate();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}
	}
}
