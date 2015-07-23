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
using SlimDX.Direct3D9;

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

		private enum MorphExportFormat
		{
			[Description("Metasequoia")]
			Mqo,
			[Description("FBX 2015.1")]
			Fbx,
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
		bool SetComboboxEvent = false;

		public string EditorFormVar { get; protected set; }
		TreeNode draggedNode = null;

		int loadedFrame = -1;
		int[] loadedBone = null;
		int[] highlightedBone = null;
		int loadedMesh = -1;
		int loadedMaterial = -1;
		int loadedTexture = -1;

		static Matrix[] copyMatrices = new Matrix[10];

		Dictionary<int, List<KeyList<Material>>> crossRefMeshMaterials = new Dictionary<int,List<KeyList<Material>>>();
		Dictionary<int, List<KeyList<Texture2D>>> crossRefMeshTextures = new Dictionary<int,List<KeyList<Texture2D>>>();
		Dictionary<int, List<KeyList<MeshRenderer>>> crossRefMaterialMeshes = new Dictionary<int,List<KeyList<MeshRenderer>>>();
		Dictionary<int, List<KeyList<Texture2D>>> crossRefMaterialTextures = new Dictionary<int,List<KeyList<Texture2D>>>();
		Dictionary<int, List<KeyList<MeshRenderer>>> crossRefTextureMeshes = new Dictionary<int,List<KeyList<MeshRenderer>>>();
		Dictionary<int, List<KeyList<Material>>> crossRefTextureMaterials = new Dictionary<int,List<KeyList<Material>>>();
		Dictionary<int, int> crossRefMeshMaterialsCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefMeshTexturesCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefMaterialMeshesCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefMaterialTexturesCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefTextureMeshesCount = new Dictionary<int, int>();
		Dictionary<int, int> crossRefTextureMaterialsCount = new Dictionary<int, int>();

		public List<RenderObjectUnity> renderObjectMeshes { get; protected set; }
		List<int> renderObjectIds;

		private bool listViewItemSyncSelectedSent = false;
		private bool textureLoading = false;

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
				LoadAnimator(false);
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
			DisposeRenderObjects();
			CrossRefsClear();
			ClearKeyList<Material>(crossRefMeshMaterials);
			ClearKeyList<Texture2D>(crossRefMeshTextures);
			ClearKeyList<MeshRenderer>(crossRefMaterialMeshes);
			ClearKeyList<Texture2D>(crossRefMaterialTextures);
			ClearKeyList<MeshRenderer>(crossRefTextureMeshes);
			ClearKeyList<Material>(crossRefTextureMaterials);
		}

		public void RecreateRenderObjects()
		{
			DisposeRenderObjects();

			if (Gui.Docking.DockRenderer.IsHidden)
			{
				return;
			}

			renderObjectMeshes = new List<RenderObjectUnity>(new RenderObjectUnity[Editor.Meshes.Count]);
			renderObjectIds = new List<int>(Editor.Meshes.Count);
			for (int i = 0; i < Editor.Meshes.Count; i++)
			{
				renderObjectIds.Add(-1);
			}

			foreach (ListViewItem item in listViewMesh.SelectedItems)
			{
				int id = (int)item.Tag;
				MeshRenderer meshR = Editor.Meshes[id];
				Transform meshTransform = meshR.m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
				HashSet<string> meshNames = new HashSet<string>() { meshTransform.GetTransformPath() };
				renderObjectMeshes[id] = new RenderObjectUnity(Editor, meshNames);

				RenderObjectUnity renderObj = renderObjectMeshes[id];
				renderObjectIds[id] = Gui.Renderer.AddRenderObject(renderObj);
			}

			HighlightSubmeshes();
			if (highlightedBone != null)
			{
				if (highlightedBone[0] >= Editor.Meshes.Count || highlightedBone[1] >= ((SkinnedMeshRenderer)Editor.Meshes[highlightedBone[0]]).m_Bones.Count)
				{
					highlightedBone = null;
				}
				else
				{
					HighlightBone(highlightedBone, true);
				}
			}
		}

		void DisposeRenderObjects()
		{
			if (renderObjectIds != null)
			{
				foreach (ListViewItem item in listViewMesh.SelectedItems)
				{
					if (renderObjectIds.Count > (int)item.Tag && renderObjectIds[(int)item.Tag] >= 0)
					{
						Gui.Renderer.RemoveRenderObject(renderObjectIds[(int)item.Tag]);
						renderObjectIds[(int)item.Tag] = -1;
					}
				}
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

			panelTexturePic.Resize += new EventHandler(panelTexturePic_Resize);
			splitContainer1.Panel2MinSize = tabControlViews.Width;

			dataGridViewMaterialTextures.RowHeadersDefaultCellStyle.Padding = new Padding(dataGridViewMaterialTextures.RowHeadersWidth);
			dataGridViewMaterialTextures.RowPostPaint += dataGridView_RowPostPaint;
			dataGridViewMaterialColours.RowHeadersDefaultCellStyle.Padding = new Padding(dataGridViewMaterialColours.RowHeadersWidth);
			dataGridViewMaterialColours.RowPostPaint += dataGridView_RowPostPaint;
			dataGridViewMaterialColours.CellValueChanged += dataGridViewMaterialColours_CellValueChanged;
			dataGridViewMaterialValues.RowHeadersDefaultCellStyle.Padding = new Padding(dataGridViewMaterialValues.RowHeadersWidth);
			dataGridViewMaterialValues.RowPostPaint += dataGridView_RowPostPaint;
			dataGridViewMaterialValues.CellValueChanged += dataGridViewMaterialValues_CellValueChanged;
			LoadMaterial(-1);

			DataGridViewEditor.InitDataGridViewSRT(dataGridViewFrameSRT, dataGridViewFrameMatrix);
			DataGridViewEditor.InitDataGridViewMatrix(dataGridViewFrameMatrix, dataGridViewFrameSRT);
			DataGridViewEditor.InitDataGridViewSRT(dataGridViewBoneSRT, dataGridViewBoneMatrix);
			DataGridViewEditor.InitDataGridViewMatrix(dataGridViewBoneMatrix, dataGridViewBoneSRT);

			textBoxFrameName.AfterEditTextChanged += new EventHandler(textBoxFrameName_AfterEditTextChanged);
			editTextBoxMeshName.AfterEditTextChanged += new EventHandler(editTextBoxMeshName_AfterEditTextChanged);
			textBoxMatName.AfterEditTextChanged += new EventHandler(textBoxMatName_AfterEditTextChanged);
			textBoxTexName.AfterEditTextChanged += new EventHandler(textBoxTexName_AfterEditTextChanged);

			ColumnSubmeshMaterial.DisplayMember = "Item1";
			ColumnSubmeshMaterial.ValueMember = "Item2";
			ColumnSubmeshMaterial.DefaultCellStyle.NullValue = "(invalid)";

			ColumnMaterialTexture.DisplayMember = "Item1";
			ColumnMaterialTexture.ValueMember = "Item2";
			ColumnMaterialTexture.DefaultCellStyle.NullValue = "(invalid)";

			comboBoxAvatar.DisplayMember = "Item1";
			comboBoxAvatar.ValueMember = "Item2";

			comboBoxMeshRendererMesh.DisplayMember = "Item1";
			comboBoxMeshRendererMesh.ValueMember = "Item2";

			comboBoxRendererRootBone.DisplayMember = "Item1";
			comboBoxRendererRootBone.ValueMember = "Item2";

			MeshExportFormat[] values = Enum.GetValues(typeof(MeshExportFormat)) as MeshExportFormat[];
			string[] descriptions = new string[values.Length];
			for (int i = 0; i < descriptions.Length; i++)
			{
				descriptions[i] = values[i].GetDescription();
			}
			comboBoxMeshExportFormat.Items.AddRange(descriptions);
			comboBoxMeshExportFormat.SelectedItem = Gui.Config["MeshExportFormat"];

			checkBoxMeshExportMqoSortMeshes.Checked = (bool)Gui.Config["ExportMqoSortMeshes"];
			checkBoxMeshExportMqoSortMeshes.CheckedChanged += checkBoxMeshExportMqoSortMeshes_CheckedChanged;

			MorphExportFormat[] morphValues = Enum.GetValues(typeof(MorphExportFormat)) as MorphExportFormat[];
			descriptions = new string[morphValues.Length];
			for (int i = 0; i < descriptions.Length; i++)
			{
				descriptions[i] = morphValues[i].GetDescription();
			}
			comboBoxMorphExportFormat.Items.AddRange(descriptions);
			comboBoxMorphExportFormat.SelectedIndex = 1;
		}

		// http://stackoverflow.com/questions/5825088/net-datagridview-remove-current-row-black-triangle
		void dataGridView_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
		{
			object o = ((DataGridView)sender).Rows[e.RowIndex].HeaderCell.Value;

			e.Graphics.DrawString(
				o != null ? o.ToString() : "",
				((DataGridView)sender).Font,
				Brushes.Black,
				new PointF((float)e.RowBounds.Left + 2, (float)e.RowBounds.Top + 4));
		}

		void LoadAnimator(bool refreshLists)
		{
			if (!refreshLists)
			{
				if (!Gui.Docking.DockRenderer.IsHidden)
				{
					renderObjectMeshes = new List<RenderObjectUnity>(new RenderObjectUnity[Editor.Meshes.Count]);
					renderObjectIds = new List<int>(Editor.Meshes.Count);
					for (int i = 0; i < Editor.Meshes.Count; i++)
					{
						renderObjectIds.Add(-1);
					}
				}
			}

			try
			{
				if (tabControlLists.TabPages.Contains(tabPageMorph))
				{
					bool keepEmIn = false;
					foreach (MeshRenderer meshR in Editor.Meshes)
					{
						Mesh mesh = Operations.GetMesh(meshR);
						if (mesh != null && mesh.m_Shapes.shapes.Count > 0)
						{
							keepEmIn = true;
							break;
						}
					}
					if (!keepEmIn)
					{
						tabControlLists.TabPages.Remove(tabPageMorph);
						tabControlViews.TabPages.Remove(tabPageMorphView);
					}
				}

				InitFrames();
				InitMeshes();
				InitMorphs();
				InitMaterials();
				InitTextures();
			}
			catch (Exception ex)
			{
				Report.ReportLog(ex.ToString());
			}

			if (!refreshLists)
			{
				RecreateCrossRefs();

				dragOptions = new FormAnimatorDragDrop(Editor);
			}
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

			comboBoxAvatar.Items.Clear();
			comboBoxAvatar.Items.Add(new Tuple<string, Component>("(none)", null));
			try
			{
				Editor.Parser.file.BeginLoadingSkippedComponents();
				for (int i = 0; i < Editor.Parser.file.Components.Count; i++)
				{
					Component asset = Editor.Parser.file.Components[i];
					if (asset.classID1 == UnityClassID.Avatar)
					{
						string assetName;
						if (asset is NotLoaded)
						{
							if (((NotLoaded)asset).Name != null)
							{
								assetName = ((NotLoaded)asset).Name;
							}
							else
							{
								Editor.Parser.file.SourceStream.Position = ((NotLoaded)asset).offset;
								assetName = Avatar.LoadName(Editor.Parser.file.SourceStream);
								((NotLoaded)asset).Name = assetName;
							}
						}
						else
						{
							assetName = ((Avatar)asset).m_Name;
						}
						comboBoxAvatar.Items.Add
						(
							new Tuple<string, Component>
							(
								asset.pathID + " " + assetName,
								asset
							)
						);
					}
				}
			}
			finally
			{
				Editor.Parser.file.EndLoadingSkippedComponents();
			}
		}

		private TreeNode CreateFrameTree(Transform frame, TreeNode parentNode)
		{
			TreeNode newNode = new TreeNode(frame.m_GameObject.instance.m_Name);
			newNode.Tag = new DragSource(EditorVar, typeof(Transform), Editor.Frames.IndexOf(frame));

			foreach (var pair in frame.m_GameObject.instance.m_Component)
			{
				Component asset = pair.Value.asset;
				if (asset == null)
				{
					continue;
				}

				switch (asset.classID2)
				{
				case UnityClassID.SkinnedMeshRenderer:
					SkinnedMeshRenderer frameSMR = (SkinnedMeshRenderer)asset;
					int sMeshId = Editor.Meshes.IndexOf(frameSMR);
					TreeNode sMeshNode = new TreeNode("SkinnedMeshRenderer");
					sMeshNode.Tag = new DragSource(EditorVar, typeof(SkinnedMeshRenderer), sMeshId);
					newNode.Nodes.Add(sMeshNode);

					if (frameSMR.m_Bones.Count > 0)
					{
						TreeNode boneListNode = new TreeNode(frameSMR.m_Bones.Count + " Bones");
						sMeshNode.Nodes.Add(boneListNode);
						for (int i = 0; i < frameSMR.m_Bones.Count; i++)
						{
							Transform bone = frameSMR.m_Bones[i].instance;
							TreeNode boneNode;
							if (bone != null && bone.m_GameObject.instance != null)
							{
								boneNode = new TreeNode(bone.m_GameObject.instance.m_Name);
							}
							else
							{
								boneNode = new TreeNode("invalid bone");
								boneNode.ForeColor = Color.OrangeRed;
							}
							boneNode.Tag = new DragSource(EditorVar, typeof(Matrix), new int[] { sMeshId, i });
							boneListNode.Nodes.Add(boneNode);
						}
					}
					break;
				case UnityClassID.MeshRenderer:
					MeshRenderer frameMR = (MeshRenderer)asset;
					int meshMRId = Editor.Meshes.IndexOf(frameMR);
					TreeNode meshRNode = new TreeNode("MeshRenderer");
					meshRNode.Tag = new DragSource(EditorVar, typeof(MeshRenderer), meshMRId);
					newNode.Nodes.Add(meshRNode);
					break;
				case UnityClassID.ParticleRenderer:
					ParticleRenderer framePR = (ParticleRenderer)asset;
					int meshPRId = Editor.Meshes.IndexOf(framePR);
					TreeNode meshPRNode = new TreeNode("ParticleRenderer");
					meshPRNode.Tag = new DragSource(EditorVar, typeof(ParticleRenderer), meshPRId);
					newNode.Nodes.Add(meshPRNode);
					break;
				case UnityClassID.MeshFilter:
				case UnityClassID.Transform:
					break;
				default:
					string text = asset.classID2.ToString() + (asset.classID1 != asset.classID2 ? " " + (int)asset.classID1 : String.Empty);
					TreeNode otherNode = new TreeNode(text);
					otherNode.ForeColor = Color.Red;
					newNode.Nodes.Add(otherNode);
					break;
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
				MeshRenderer meshR = Editor.Meshes[i];
				meshItems[i] = new ListViewItem(new string[] { meshR.m_GameObject.instance.m_Name, meshR.classID1.ToString() });
				meshItems[i].Tag = i;
			}
			listViewMesh.Items.Clear();
			listViewMesh.Items.AddRange(meshItems);
			meshlistHeaderNames.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			meshListHeaderType.Width = 60;

			comboBoxMeshRendererMesh.Items.Clear();
			comboBoxMeshRendererMesh.Items.Add(new Tuple<string, Component>("(none)", null));
			for (int i = 0; i < Editor.Parser.file.Components.Count; i++)
			{
				Component asset = Editor.Parser.file.Components[i];
				if (asset.classID1 == UnityClassID.Mesh)
				{
					comboBoxMeshRendererMesh.Items.Add
					(
						new Tuple<string, Component>
						(
							asset.pathID + " " + (asset is NotLoaded ? ((NotLoaded)asset).Name : ((Mesh)asset).m_Name),
							asset
						)
					);
				}
			}
		}

		void InitMorphs()
		{
			string keyframeName = editTextBoxMorphKeyframe.Text;
			List<TreeNode> morphMeshes = new List<TreeNode>();
			TreeNode selectedNode = null;
			for (int i = 0; i < Editor.Meshes.Count; i++)
			{
				MeshRenderer meshR = Editor.Meshes[i];
				Mesh mesh = Operations.GetMesh(meshR);
				if (mesh != null && mesh.m_Shapes.shapes.Count > 0)
				{
					TreeNode meshNode = new TreeNode(Operations.BlendShapeName(mesh) + " [" + mesh.m_Name + "]");
					meshNode.Tag = meshR;
					for (int j = 0; j < mesh.m_Shapes.shapes.Count; j++)
					{
						MeshBlendShapeChannel channel = mesh.m_Shapes.channels[j];
						MeshBlendShape shape = mesh.m_Shapes.shapes[j];
						TreeNode morphClipNode = new TreeNode(channel.frameIndex.ToString("D2") + ": " + Operations.BlendShapeKeyframeName(mesh, j) + " vertices=" + shape.vertexCount);
						morphClipNode.Tag = j;
						if (channel.name == keyframeName)
						{
							meshNode.Expand();
							selectedNode = morphClipNode;
						}
						meshNode.Nodes.Add(morphClipNode);
					}
					morphMeshes.Add(meshNode);
				}
			}
			treeViewMorphKeyframes.Nodes.Clear();
			treeViewMorphKeyframes.Nodes.AddRange(morphMeshes.ToArray());
			if (selectedNode != null)
			{
				treeViewMorphKeyframes.SelectedNode = selectedNode;
				selectedNode.EnsureVisible();
			}
		}

		void InitMaterials()
		{
			HashSet<string> selectedItems = new HashSet<string>();
			foreach (ListViewItem item in listViewMaterial.SelectedItems)
			{
				selectedItems.Add(item.Text);
			}
			ListViewItem[] materialItems = new ListViewItem[Editor.Materials.Count];
			for (int i = 0; i < Editor.Materials.Count; i++)
			{
				Material mat = Editor.Materials[i];
				materialItems[i] = new ListViewItem(mat.m_Name);
				materialItems[i].Tag = i;
			}
			listViewMaterial.Items.Clear();
			listViewMaterial.Items.AddRange(materialItems);
			materiallistHeader.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			if (selectedItems.Count > 0)
			{
				listViewMaterial.ItemSelectionChanged -= listViewMaterial_ItemSelectionChanged;
				listViewMaterial.BeginUpdate();
				foreach (ListViewItem item in listViewMaterial.Items)
				{
					if (selectedItems.Contains(item.Text))
					{
						item.Selected = true;
					}
				}
				listViewMaterial.EndUpdate();
				listViewMaterial.ItemSelectionChanged += listViewMaterial_ItemSelectionChanged;
			}

			List<Tuple<string, int, Component>> columnMaterials = new List<Tuple<string, int, Component>>(Editor.Materials.Count * 2);
			for (int i = 0; i < Editor.Parser.file.Components.Count; i++)
			{
				Component asset = Editor.Parser.file.Components[i];
				if (asset.classID1 == UnityClassID.Material)
				{
					columnMaterials.Add
					(
						new Tuple<string, int, Component>
						(
							asset.pathID + " " + (asset is NotLoaded ? ((NotLoaded)asset).Name : ((Material)asset).m_Name),
							Editor.Materials.IndexOf(asset as Material),
							asset
						)
					);
				}
			}
			ColumnSubmeshMaterial.DataSource = columnMaterials;
			SetComboboxEvent = false;

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
			}
			listViewTexture.Items.Clear();
			listViewTexture.Items.AddRange(textureItems);
			texturelistHeader.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			if (selectedItems.Count > 0)
			{
				listViewTexture.ItemSelectionChanged -= listViewTexture_ItemSelectionChanged;
				listViewTexture.BeginUpdate();
				foreach (ListViewItem item in listViewTexture.Items)
				{
					if (selectedItems.Contains(item.Text))
					{
						item.Selected = true;
					}
				}
				listViewTexture.EndUpdate();
				listViewTexture.ItemSelectionChanged += listViewTexture_ItemSelectionChanged;
			}

			List<Tuple<string, int, Component>> columnTextures = new List<Tuple<string, int, Component>>(Editor.Textures.Count * 2);
			columnTextures.Add
			(
				new Tuple<string, int, Component>("(none)", -1, null)
			);
			for (int i = 0; i < Editor.Parser.file.Components.Count; i++)
			{
				Component asset = Editor.Parser.file.Components[i];
				if (asset.classID1 == UnityClassID.Texture2D || asset.classID1 == UnityClassID.Cubemap)
				{
					columnTextures.Add
					(
						new Tuple<string, int, Component>
						(
							asset.pathID + " " + (asset is NotLoaded ? ((NotLoaded)asset).Name : ((Texture2D)asset).m_Name),
							Editor.Textures.IndexOf(asset as Texture2D),
							asset
						)
					);
				}
			}
			ColumnMaterialTexture.DataSource = columnTextures;
			SetComboboxEvent = false;

			TreeNode texturesNode = new TreeNode("Textures");
			TreeNode currentTexture = null;
			for (int i = 0; i < Editor.Textures.Count; i++)
			{
				TreeNode texNode = new TreeNode(Editor.Textures[i].m_Name);
				texNode.Tag = new DragSource(EditorVar, typeof(Texture2D), i);
				texturesNode.Nodes.Add(texNode);
				if (loadedTexture == i)
					currentTexture = texNode;
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

		void LoadFrame(int id)
		{
			if (id < 0)
			{
				textBoxFrameName.Text = String.Empty;
				DataGridViewEditor.LoadMatrix(Matrix.Identity, dataGridViewFrameSRT, dataGridViewFrameMatrix);
			}
			else
			{
				Transform frame = Editor.Frames[id];
				comboBoxAvatar.SelectedIndexChanged -= comboBoxAvatar_SelectedIndexChanged;
				if (id != 0 && (frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.Animator) == null && GetVirtualAnimator(frame.m_GameObject.instance) == null))
				{
					labelTransformName.Text = "Transform Name";
					comboBoxAvatar.SelectedIndex = -1;
					comboBoxAvatar.Enabled = false;
				}
				else
				{
					labelTransformName.Text = "Animator Name";
					Animator vAnimator = GetVirtualAnimator(frame.m_GameObject.instance);
					Avatar avatar = vAnimator != null ? vAnimator.m_Avatar.instance : Editor.Parser.m_Avatar.instance;
					comboBoxAvatar.SelectedIndex = -1;
					if (avatar != null)
					{
						for (int i = 0; i < comboBoxAvatar.Items.Count; i++)
						{
							Tuple<string, Component> item = (Tuple<string, Component>)comboBoxAvatar.Items[i];
							if (item.Item2 == Editor.Parser.m_Avatar.instance)
							{
								comboBoxAvatar.SelectedIndex = i;
								toolTip1.SetToolTip(comboBoxAvatar, comboBoxAvatar.Text);
								break;
							}
						}
					}
					comboBoxAvatar.Enabled = true;
				}
				comboBoxAvatar.SelectedIndexChanged += comboBoxAvatar_SelectedIndexChanged;
				textBoxFrameName.Text = frame.m_GameObject.instance.m_Name;
				DataGridViewEditor.LoadMatrix(frame.m_LocalScale, frame.m_LocalRotation, frame.m_LocalPosition, dataGridViewFrameSRT, dataGridViewFrameMatrix);
			}
			loadedFrame = id;
		}

		Animator GetVirtualAnimator(GameObject gameObject)
		{
			foreach (var var in Gui.Scripting.Variables)
			{
				if (var.Value is Unity3dEditor)
				{
					Unity3dEditor unityEditor = (Unity3dEditor)var.Value;
					if (unityEditor.Parser.Cabinet == Editor.Parser.file)
					{
						return unityEditor.GetVirtualAnimator(gameObject);
					}
				}
			}
			throw new Exception("Unity3dEditor not found");
		}

		void LoadBone(int[] id)
		{
			if (id == null)
			{
				textBoxBoneName.Text = String.Empty;
				editTextBoxBoneHash.Text = String.Empty;
				DataGridViewEditor.LoadMatrix(Matrix.Identity, dataGridViewBoneSRT, dataGridViewBoneMatrix);
			}
			else
			{
				SkinnedMeshRenderer smr = (SkinnedMeshRenderer)Editor.Meshes[id[0]];
				Transform bone = smr.m_Bones[id[1]].instance;
				textBoxBoneName.Text = bone != null && bone.m_GameObject.instance != null ? bone.m_GameObject.instance.m_Name : "invalid";
				Mesh mesh = smr.m_Mesh.instance;
				if (mesh != null)
				{
					editTextBoxBoneHash.Text = mesh.m_BoneNameHashes[id[1]].ToString();
					Matrix matrix = Matrix.Transpose(mesh.m_BindPose[id[1]]);
					DataGridViewEditor.LoadMatrix(matrix, dataGridViewBoneSRT, dataGridViewBoneMatrix);
				}
			}
			loadedBone = id;

			if (highlightedBone != null)
				HighlightBone(highlightedBone, false);
			if (loadedBone != null)
				HighlightBone(loadedBone, true);
			highlightedBone = loadedBone;
		}

		void LoadMesh(int id)
		{
			dataGridViewMesh.CellValueChanged -= dataGridViewMesh_CellValueChanged;
			dataGridViewMesh.Rows.Clear();
			comboBoxMeshRendererMesh.SelectedIndexChanged -= comboBoxMeshRendererMesh_SelectedIndexChanged;
			comboBoxRendererRootBone.SelectedIndexChanged -= comboBoxRendererRootBone_SelectedIndexChanged;

			if (id < 0)
			{
				textBoxRendererName.Text = String.Empty;
				checkBoxRendererEnabled.Checked = false;
				comboBoxMeshRendererMesh.SelectedIndex = -1;
				editTextBoxMeshName.Text = String.Empty;
				comboBoxRendererRootBone.SelectedIndex = -1;
				editTextBoxMeshRootBone.Text = String.Empty;
			}
			else
			{
				MeshRenderer meshR = Editor.Meshes[id];
				textBoxRendererName.Text = meshR.m_GameObject.instance.m_Name;
				checkBoxRendererEnabled.Checked = meshR.m_Enabled;
				if (meshR is SkinnedMeshRenderer)
				{
					SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshR;

					comboBoxRendererRootBone.Items.Clear();
					comboBoxRendererRootBone.Items.Add(new Tuple<string, int>("(none)", -1));
					HashSet<Transform> skeleton = Operations.GetSkeleton(sMesh);
					for (int i = 0; i < Editor.Frames.Count; i++)
					{
						Transform frame = Editor.Frames[i];
						if (skeleton.Contains(frame))
						{
							comboBoxRendererRootBone.Items.Add
							(
								new Tuple<string, int>
								(
									frame.m_GameObject.instance.m_Name,
									i
								)
							);
						}
					}

					comboBoxRendererRootBone.SelectedIndex = sMesh.m_RootBone.instance != null ? comboBoxRendererRootBone.FindStringExact(sMesh.m_RootBone.instance.m_GameObject.instance.m_Name) : 0;
				}
				Mesh mesh = Operations.GetMesh(meshR);
				if (mesh != null)
				{
					for (int i = 0; i < comboBoxMeshRendererMesh.Items.Count; i++)
					{
						Tuple<string, Component> item = (Tuple<string, Component>)comboBoxMeshRendererMesh.Items[i];
						if (item.Item2 == mesh)
						{
							comboBoxMeshRendererMesh.SelectedIndex = i;
							break;
						}
					}

					editTextBoxMeshName.Text = mesh.m_Name;
					if (Editor.Parser.m_Avatar.instance != null)
					{
						editTextBoxMeshRootBone.Text = Editor.Parser.m_Avatar.instance.FindBoneName(mesh.m_RootBoneNameHash);
					}

					for (int i = 0; i < mesh.m_SubMeshes.Count; i++)
					{
						SubMesh submesh = mesh.m_SubMeshes[i];
						dataGridViewMesh.Rows.Add
						(
							new object[]
							{
								submesh.vertexCount,
								submesh.indexCount / 3,
								i < meshR.m_Materials.Count && meshR.m_Materials[i].instance != null ? (object)Editor.Materials.IndexOf(meshR.m_Materials[i].instance) : null,
								submesh.topology.ToString()
							}
						);
					}
					for (int i = mesh.m_SubMeshes.Count; i < meshR.m_Materials.Count; i++)
					{
						dataGridViewMesh.Rows.Add
						(
							new object[]
							{
								null,
								null,
								meshR.m_Materials[i].instance != null ? (object)Editor.Materials.IndexOf(meshR.m_Materials[i].instance) : null,
								null
							}
						);
					}
					dataGridViewMesh.ClearSelection();
					int newHeight = dataGridViewMesh.ColumnHeadersHeight + 2 + Math.Min(dataGridViewMesh.Rows.Count, 4) * 22;
					dataGridViewMesh.Height = newHeight;
					groupBoxMesh.Height = dataGridViewMesh.Height + 135;
				}
				else
				{
					PPtr<Mesh> meshPtr = Operations.GetMeshPtr(meshR);
					if (meshPtr != null && meshPtr.m_FileID != 0)
					{
						editTextBoxMeshName.Text = "External Mesh FileID=" + meshPtr.m_FileID + " PathID=" + meshPtr.m_PathID;
					}
					comboBoxMeshRendererMesh.SelectedIndex = 0;
				}
			}

			comboBoxRendererRootBone.SelectedIndexChanged += comboBoxRendererRootBone_SelectedIndexChanged;
			comboBoxMeshRendererMesh.SelectedIndexChanged += comboBoxMeshRendererMesh_SelectedIndexChanged;
			dataGridViewMesh.CellValueChanged += dataGridViewMesh_CellValueChanged;
			loadedMesh = id;
		}

		void LoadMaterial(int id)
		{
			if (loadedMaterial >= 0)
			{
				loadedMaterial = -1;
			}
			dataGridViewMaterialTextures.Rows.Clear();
			dataGridViewMaterialColours.CellValueChanged -= dataGridViewMaterialColours_CellValueChanged;
			dataGridViewMaterialColours.Rows.Clear();
			dataGridViewMaterialValues.CellValueChanged -= dataGridViewMaterialValues_CellValueChanged;
			dataGridViewMaterialValues.Rows.Clear();

			if (id < 0)
			{
				textBoxMatName.Text = String.Empty;
				editTextBoxMatShader.Text = String.Empty;
				comboBoxMatShaderKeywords.Items.Clear();
			}
			else
			{
				Material mat = Editor.Materials[id];
				textBoxMatName.Text = mat.m_Name;

				if (mat.m_Shader.asset != null)
				{
					editTextBoxMatShader.Text = AssetCabinet.ToString(mat.m_Shader.asset);
				}
				else if (mat.m_Shader.m_FileID != 0)
				{
					editTextBoxMatShader.Text = "External Shader FileID=" + mat.m_Shader.m_FileID + " PathID=" + mat.m_Shader.m_PathID;
				}
				toolTip1.SetToolTip(editTextBoxMatShader, editTextBoxMatShader.Text);
				toolTip1.SetToolTip(labelMaterialShaderUsed, editTextBoxMatShader.Text);
				if (mat.m_ShaderKeywords.Count > 0)
				{
					comboBoxMatShaderKeywords.Items.AddRange(mat.m_ShaderKeywords.ToArray());
					comboBoxMatShaderKeywords.SelectedIndex = 0;
				}

				for (int i = 0; i < mat.m_SavedProperties.m_TexEnvs.Count; i++)
				{
					var matTex = mat.m_SavedProperties.m_TexEnvs[i];
					dataGridViewMaterialTextures.Rows.Add
					(
						new object[]
						{
							matTex.Value.m_Texture.instance != null ? (object)Editor.Textures.IndexOf(matTex.Value.m_Texture.instance) : null,
							matTex.Value.m_Offset.X + "," + matTex.Value.m_Offset.Y,
							matTex.Value.m_Scale.X + "," + matTex.Value.m_Scale.Y
						}
					);
					dataGridViewMaterialTextures.Rows[i].HeaderCell.Value = ShortLabel(matTex.Key.name);
					if (matTex.Value.m_Texture.m_FileID != 0)
					{
						Report.ReportLog(dataGridViewMaterialTextures.Rows[i].HeaderCell.Value + " with external Texture FileID=" + matTex.Value.m_Texture.m_FileID + " PathID=" + matTex.Value.m_Texture.m_PathID);
					}
				}
				if (mat.m_SavedProperties.m_TexEnvs.Count == 0)
				{
					dataGridViewMaterialTextures.Rows.Add
					(
						new object[] { null }
					);
					dataGridViewMaterialTextures.Rows[0].HeaderCell.Value = "No Texture";
					dataGridViewMaterialTextures.Enabled = false;
				}
				else
				{
					dataGridViewMaterialTextures.Enabled = true;
				}
				int newHeight = dataGridViewMaterialTextures.ColumnHeadersHeight + 2 + Math.Min(dataGridViewMaterialTextures.Rows.Count, 6) * 22;
				dataGridViewMaterialTextures.Height = newHeight;

				for (int i = 0; i < mat.m_SavedProperties.m_Colors.Count; i++)
				{
					var colPair = mat.m_SavedProperties.m_Colors[i];
					dataGridViewMaterialColours.Rows.Add
					(
						new object[]
						{
							colPair.Value.Red,
							colPair.Value.Green,
							colPair.Value.Blue,
							colPair.Value.Alpha
						}
					);
					dataGridViewMaterialColours.Rows[i].HeaderCell.Value = ShortLabel(colPair.Key.name);
				}
				if (mat.m_SavedProperties.m_Colors.Count == 0)
				{
					dataGridViewMaterialColours.Rows.Add
					(
						new object[] { null, null, null, null }
					);
					dataGridViewMaterialColours.Rows[0].HeaderCell.Value = "No Color";
					dataGridViewMaterialColours.Enabled = false;
				}
				else
				{
					dataGridViewMaterialColours.Enabled = true;
				}
				newHeight = dataGridViewMaterialColours.ColumnHeadersHeight + 2 + Math.Min(dataGridViewMaterialColours.Rows.Count, 6) * 22;
				dataGridViewMaterialColours.Top = dataGridViewMaterialTextures.Top + dataGridViewMaterialTextures.Height + 8;
				dataGridViewMaterialColours.Height = newHeight;

				for (int i = 0; i < mat.m_SavedProperties.m_Floats.Count; i++)
				{
					var floatPair = mat.m_SavedProperties.m_Floats[i];
					dataGridViewMaterialValues.Rows.Add
					(
						new object[] { floatPair.Value }
					);
					dataGridViewMaterialValues.Rows[i].HeaderCell.Value = ShortLabel(floatPair.Key.name);
				}
				if (mat.m_SavedProperties.m_Floats.Count == 0)
				{
					dataGridViewMaterialValues.Rows.Add
					(
						new object[] { null }
					);
					dataGridViewMaterialValues.Rows[0].HeaderCell.Value = "No Value";
					dataGridViewMaterialValues.Enabled = false;
				}
				else
				{
					dataGridViewMaterialValues.Enabled = true;
				}
				newHeight = 3 + Math.Min(dataGridViewMaterialValues.Rows.Count, 10) * 22;
				dataGridViewMaterialValues.Top = dataGridViewMaterialColours.Top + dataGridViewMaterialColours.Height + 8;
				dataGridViewMaterialValues.Height = newHeight;

				buttonMaterialRemove.Top = dataGridViewMaterialValues.Top;
				buttonMaterialCopy.Top = buttonMaterialRemove.Top + buttonMaterialRemove.Height + 8;
			}

			dataGridViewMaterialColours.CellValueChanged += dataGridViewMaterialColours_CellValueChanged;
			dataGridViewMaterialValues.CellValueChanged += dataGridViewMaterialValues_CellValueChanged;
			loadedMaterial = id;
		}

		private string ShortLabel(string text)
		{
			int start = text[0] == '_' ? 1 : 0;
			int len = text.EndsWith("Color") && text.Length - start > 6 ? text.Length - start - 5 : text.Length - start;
			return text.Substring(start, len);
		}

		void LoadTexture(int id)
		{
			textureLoading = true;
			if (pictureBoxTexture.Image != null)
			{
				pictureBoxTexture.Image.Dispose();
				pictureBoxTexture.Image = null;
			}
			if (id < 0)
			{
				textBoxTexName.Text = String.Empty;
				textBoxTexSize.Text = String.Empty;
				labelTextureFormat.Text = String.Empty;
				editTextBoxTexDimension.Text = String.Empty;
				checkBoxTextureMipMap.Checked = false;
				editTextBoxTexImageCount.Text = String.Empty;
				editTextBoxTexColorSpace.Text = String.Empty;
				editTextBoxTexLightMap.Text = String.Empty;
				editTextBoxTexFilterMode.Text = String.Empty;
				editTextBoxTexMipBias.Text = String.Empty;
				editTextBoxTexAniso.Text = String.Empty;
				editTextBoxTexWrapMode.Text = String.Empty;
			}
			else
			{
				Texture2D tex = Editor.Textures[id];
				labelTextureClass.Text = tex.classID1.ToString();
				textBoxTexName.Text = tex.m_Name;
				textBoxTexSize.Text = tex.m_Width + "x" + tex.m_Height;
				labelTextureFormat.Text = tex.m_TextureFormat.ToString();
				editTextBoxTexDimension.Text = tex.m_TextureDimension.ToString();
				checkBoxTextureMipMap.Checked = tex.m_MipMap;
				editTextBoxTexImageCount.Text = tex.m_ImageCount.ToString();
				editTextBoxTexColorSpace.Text = tex.m_ColorSpace.ToString();
				editTextBoxTexLightMap.Text = tex.m_LightmapFormat.ToString();
				editTextBoxTexFilterMode.Text = tex.m_TextureSettings.m_FilterMode.ToString();
				editTextBoxTexMipBias.Text = tex.m_TextureSettings.m_MipBias.ToFloatString();
				editTextBoxTexAniso.Text = tex.m_TextureSettings.m_Aniso.ToString();
				editTextBoxTexWrapMode.Text = tex.m_TextureSettings.m_WrapMode.ToString();

				ImportedTexture impTex;
				using (MemoryStream mem = new MemoryStream())
				{
					tex.Export(mem);
					mem.Position = 0;
					impTex = new ImportedTexture(mem, tex.m_Name);
				}
				Texture renderTexture = null;
				try
				{
					renderTexture = Texture.FromMemory(Gui.Renderer.Device, impTex.Data);
					using (Image image = Image.FromStream(Texture.ToStream(renderTexture, tex.m_Height <= 512 && tex.m_Width <= 512 ? ImageFileFormat.Png : ImageFileFormat.Bmp)))
					{
						int shift = 0;
						for (int max = tex.m_Width > tex.m_Height ? tex.m_Width : tex.m_Height; max > 256; max >>= 1)
						{
							shift++;
						}
						pictureBoxTexture.Image = new Bitmap(image, new Size(tex.m_Width >> shift, tex.m_Height >> shift));
					}
					string format = renderTexture.GetLevelDescription(0).Format.GetDescription();
					int bpp = (format.Contains("A8") ? 8 : 0)
						+ (format.Contains("R8") ? 8 : 0)
						+ (format.Contains("G8") ? 8 : 0)
						+ (format.Contains("B8") ? 8 : 0);
					if (bpp > 0)
					{
						textBoxTexSize.Text += "x" + bpp;
					}
				}
				catch (Exception e)
				{
					pictureBoxTexture.Image = pictureBoxTexture.ErrorImage;
					Utility.ReportException(e);
				}
				finally
				{
					if (renderTexture != null)
					{
						renderTexture.Dispose();
					}
				}

				ResizeImage();
			}
			textureLoading = false;
			loadedTexture = id;
		}

		TreeNode FindFrameNode(string name, TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				var source = node.Tag as DragSource?;
				if (source == null || source.Value.Type != typeof(Transform))
				{
					continue;
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
				if (source == null || source.Value.Type != typeof(Transform))
				{
					continue;
				}

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

			return null;
		}

		void FindBoneNodes(Transform bone, TreeNodeCollection nodes, List<TreeNode> boneNodes)
		{
			foreach (TreeNode node in nodes)
			{
				var source = node.Tag as DragSource?;

				if ((source != null) && (source.Value.Type == typeof(Matrix)))
				{
					var id = (int[])source.Value.Id;
					SkinnedMeshRenderer smr = (SkinnedMeshRenderer)Editor.Meshes[id[0]];
					Mesh mesh = Operations.GetMesh(smr);
					if (smr.m_Bones[id[1]].instance != null && smr.m_Bones[id[1]].instance.Equals(bone)
						|| mesh != null && Editor.Parser.m_Avatar.instance.FindBoneName(mesh.m_BoneNameHashes[id[1]]) == bone.m_GameObject.instance.m_Name)
					{
						boneNodes.Add(node);
						break;
					}
				}

				FindBoneNodes(bone, node.Nodes, boneNodes);
			}
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

		TreeNode FindTextureNode(Texture2D tex)
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

			var meshes = Editor.Meshes;
			var materials = Editor.Materials;
			var textures = Editor.Textures;

			for (int i = 0; i < meshes.Count; i++)
			{
				crossRefMeshMaterials.Add(i, new List<KeyList<Material>>(materials.Count));
				crossRefMeshTextures.Add(i, new List<KeyList<Texture2D>>(textures.Count));
				crossRefMaterialMeshesCount.Add(i, 0);
				crossRefTextureMeshesCount.Add(i, 0);
			}

			for (int i = 0; i < materials.Count; i++)
			{
				crossRefMaterialMeshes.Add(i, new List<KeyList<MeshRenderer>>(meshes.Count));
				crossRefMaterialTextures.Add(i, new List<KeyList<Texture2D>>(textures.Count));
				crossRefMeshMaterialsCount.Add(i, 0);
				crossRefTextureMaterialsCount.Add(i, 0);
			}

			for (int i = 0; i < textures.Count; i++)
			{
				crossRefTextureMeshes.Add(i, new List<KeyList<MeshRenderer>>(meshes.Count));
				crossRefTextureMaterials.Add(i, new List<KeyList<Material>>(materials.Count));
				crossRefMeshTexturesCount.Add(i, 0);
				crossRefMaterialTexturesCount.Add(i, 0);
			}

			Dictionary<string, List<string>> missingTextures = new Dictionary<string, List<string>>();
			for (int i = 0; i < materials.Count; i++)
			{
				Material mat = materials[i];
				for (int j = 0; j < mat.m_SavedProperties.m_TexEnvs.Count; j++)
				{
					var matTex = mat.m_SavedProperties.m_TexEnvs[j];
					if (matTex.Value.m_Texture.instance != null)
					{
						string matTexName = matTex.Value.m_Texture.instance.m_Name;
						bool foundMatTex = false;
						for (int m = 0; m < textures.Count; m++)
						{
							Texture2D tex = textures[m];
							if (matTexName == tex.m_Name)
							{
								crossRefMaterialTextures[i].Add(new KeyList<Texture2D>(textures, m));
								crossRefTextureMaterials[m].Add(new KeyList<Material>(materials, i));
								foundMatTex = true;
								break;
							}
						}
						if (!foundMatTex)
						{
							List<string> matNames = null;
							if (!missingTextures.TryGetValue(matTexName, out matNames))
							{
								matNames = new List<string>(1);
								matNames.Add(mat.m_Name);
								missingTextures.Add(matTexName, matNames);
							}
							else if (!matNames.Contains(mat.m_Name))
							{
								matNames.Add(mat.m_Name);
							}
						}
					}
				}
			}

			for (int i = 0; i < meshes.Count; i++)
			{
				MeshRenderer meshParent = meshes[i];
				for (int j = 0; j < meshParent.m_Materials.Count; j++)
				{
					Material mat = meshParent.m_Materials[j].instance;
					int matIdx = Editor.Materials.IndexOf(mat);
					if (matIdx >= 0)
					{
						crossRefMeshMaterials[i].Add(new KeyList<Material>(materials, matIdx));
						crossRefMaterialMeshes[matIdx].Add(new KeyList<MeshRenderer>(meshes, i));
						for (int k = 0; k < mat.m_SavedProperties.m_TexEnvs.Count; k++)
						{
							var matTex = mat.m_SavedProperties.m_TexEnvs[k];
							if (matTex.Value.m_Texture.instance != null)
							{
								string matTexName = matTex.Value.m_Texture.instance.m_Name;
								bool foundMatTex = false;
								for (int m = 0; m < textures.Count; m++)
								{
									Texture2D tex = textures[m];
									if (matTexName == tex.m_Name)
									{
										crossRefMeshTextures[i].Add(new KeyList<Texture2D>(textures, m));
										crossRefTextureMeshes[m].Add(new KeyList<MeshRenderer>(meshes, i));
										foundMatTex = true;
										break;
									}
								}
								if (!foundMatTex)
								{
									List<string> matNames = null;
									if (!missingTextures.TryGetValue(matTexName, out matNames))
									{
										matNames = new List<string>(1);
										matNames.Add(mat.m_Name);
										missingTextures.Add(matTexName, matNames);
									}
									else if (!matNames.Contains(mat.m_Name))
									{
										matNames.Add(mat.m_Name);
									}
								}
							}
						}
					}
					else if (mat != null)
					{
						meshParent.m_Materials[j] = new PPtr<Material>((Component)null);
						Report.ReportLog("Warning: Mesh " + meshParent.m_GameObject.instance.m_Name + " Object " + j + " has an invalid material index");
					}
				}
			}
			if (missingTextures.Count > 0)
			{
				foreach (var missing in missingTextures)
				{
					string mats = String.Empty;
					foreach (string mat in missing.Value)
					{
						mats += (mats == String.Empty ? " " : ", ") + mat;
					}
					Report.ReportLog("Warning: Couldn't find texture " + missing.Key + " for material(s)" + mats);
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
				int meshParent = (int)listViewMesh.SelectedItems[i].Tag;
				CrossRefAddItem(crossRefMeshMaterials[meshParent], crossRefMeshMaterialsCount, listViewMeshMaterial, listViewMaterial);
				CrossRefAddItem(crossRefMeshTextures[meshParent], crossRefMeshTexturesCount, listViewMeshTexture, listViewTexture);
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
					ListViewItem item = new ListViewItem(AssetCabinet.ToString((Component)keylist.List[keylist.Index]));
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

		private void listViewMeshMaterial_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewMaterial_ItemSelectionChanged(sender, e);
		}

		private void listViewMeshMaterial_KeyUp(object sender, KeyEventArgs e)
		{
			listViewMaterial_KeyUp(sender, e);
		}

		private void listViewMeshTexture_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewTexture_ItemSelectionChanged(sender, e);
		}

		private void listViewMeshTexture_KeyUp(object sender, KeyEventArgs e)
		{
			listViewTexture_KeyUp(sender, e);
		}

		private void listViewMaterialMesh_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewMesh_ItemSelectionChanged(sender, e);
		}

		private void listViewMaterialMesh_KeyUp(object sender, KeyEventArgs e)
		{
			listViewMesh_KeyUp(sender, e);
		}

		private void listViewMaterialTexture_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewTexture_ItemSelectionChanged(sender, e);
		}

		private void listViewMaterialTexture_KeyUp(object sender, KeyEventArgs e)
		{
			listViewTexture_KeyUp(sender, e);
		}

		private void listViewTextureMesh_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewMesh_ItemSelectionChanged(sender, e);
		}

		private void listViewTextureMesh_KeyUp(object sender, KeyEventArgs e)
		{
			listViewMesh_KeyUp(sender, e);
		}

		private void listViewTextureMaterial_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			listViewMaterial_ItemSelectionChanged(sender, e);
		}

		private void listViewTextureMaterial_KeyUp(object sender, KeyEventArgs e)
		{
			listViewMaterial_KeyUp(sender, e);
		}

		private void treeViewObjectTree_AfterSelect(object sender, TreeViewEventArgs e)
		{
			try
			{
				if (e.Node.Tag is DragSource)
				{
					var tag = (DragSource)e.Node.Tag;
					if (tag.Type == typeof(Transform))
					{
						tabControlViews.SelectTabWithoutLoosingFocus(tabPageFrameView);
						LoadFrame((int)tag.Id);
						/*if (checkBoxMeshNewSkin.Checked)
						{
							buttonFrameAddBone.Text = SkinFrames.Contains(Editor.Frames[loadedFrame].Name) ? "Remove From Skin" : "Add To Skin";
						}*/
					}
					else if (tag.Type == typeof(MeshRenderer) || tag.Type == typeof(SkinnedMeshRenderer))
					{
						SetListViewAfterNodeSelect(listViewMesh, tag);
					}
					else if (tag.Type == typeof(Matrix))
					{
						tabControlViews.SelectTabWithoutLoosingFocus(tabPageBoneView);
						int[] ids = (int[])tag.Id;
						LoadBone(ids);
					}
					else if (tag.Type == typeof(Material))
					{
						SetListViewAfterNodeSelect(listViewMaterial, tag);
					}
					else if (tag.Type == typeof(Texture2D))
					{
						SetListViewAfterNodeSelect(listViewTexture, tag);
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeViewObjectTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			if (e.Node.Tag is DragSource && ((DragSource)e.Node.Tag).Type == typeof(Matrix) && e.Node.IsSelected)
			{
				if (highlightedBone != null)
				{
					HighlightBone(highlightedBone, false);
					highlightedBone = null;
				}
				else
				{
					highlightedBone = (int[])((DragSource)e.Node.Tag).Id;
					HighlightBone(highlightedBone, true);
				}
			}
		}

		private void HighlightBone(int[] boneIds, bool show)
		{
			if (renderObjectMeshes != null && renderObjectMeshes.Count > boneIds[0])
			{
				RenderObjectUnity renderObj = renderObjectMeshes[boneIds[0]];
				if (renderObj != null)
				{
					renderObj.HighlightBone(Editor.Meshes[boneIds[0]], boneIds[1], show);
					Gui.Renderer.Render();
				}
			}
		}

		private void SetListViewAfterNodeSelect(ListView listView, DragSource tag)
		{
			while (listView.SelectedItems.Count > 0)
			{
				listView.SelectedItems[0].Selected = false;
			}

			for (int i = 0; i < listView.Items.Count; i++)
			{
				var item = listView.Items[i];
				if ((int)item.Tag == (int)tag.Id)
				{
					item.Selected = true;
					break;
				}
			}
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
							draggedMesh = (SkinnedMeshRenderer)Editor.Meshes[(int)src.Id];
						}
						else if (src.Type == typeof(Matrix))
						{
							SkinnedMeshRenderer mesh = (SkinnedMeshRenderer)Editor.Meshes[((int[])src.Id)[0]];
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
					dragOptions.ShowPanel(FormAnimatorDragDrop.ShowPanelOption.Frame);
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
					RefreshFormUnity();
				}
				else if (source.Type == typeof(Texture2D))
				{
					Gui.Scripting.RunScript(EditorVar + ".MergeTexture(tex=" + source.Variable + ".Textures[" + (int)source.Id + "])");
					Changed = Changed;
					RecreateTextures();
					RefreshFormUnity();
				}
				else if (source.Type == typeof(ImportedFrame))
				{
					dragOptions.ShowPanel(FormAnimatorDragDrop.ShowPanelOption.Frame);
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
					dragOptions.ShowPanel(FormAnimatorDragDrop.ShowPanelOption.Mesh);
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
						Gui.Scripting.RunScript(EditorVar + ".ReplaceMeshRenderer(mesh=" + source.Variable + ".Meshes[" + (int)source.Id + "], frameId=" + dragOptions.numericMeshId.Value + ", rootBoneId=-1, merge=" + dragOptions.radioButtonMeshMerge.Checked + ", normals=\"" + dragOptions.NormalsMethod.GetName() + "\", bones=\"" + dragOptions.BonesMethod.GetName() + "\", targetFullMesh=" + dragOptions.radioButtonNearestMesh.Checked + ")");
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

		private void RefreshFormUnity()
		{
			List<DockContent> formUnity3dList;
			if (Gui.Docking.DockContents.TryGetValue(typeof(FormUnity3d), out formUnity3dList))
			{
				foreach (FormUnity3d form in formUnity3dList)
				{
					if (form.Editor.Parser.Cabinet == Editor.Parser.file)
					{
						form.InitSubfileLists(false);
						break;
					}
				}
			}
		}

		private void treeViewObjectTree_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION || treeViewObjectTree.SelectedNode == null)
			{
				return;
			}
			DragSource? src = treeViewObjectTree.SelectedNode.Tag as DragSource?;
			if (src != null)
			{
				if (src.Value.Type == typeof(Transform))
				{
					buttonFrameRemove_Click(null, null);
				}
				else if (src.Value.Type == typeof(MeshRenderer))
				{
					buttonMeshRemove_Click(null, null);
				}
				else if (src.Value.Type == typeof(Matrix))
				{
					buttonBoneRemove_Click(null, null);
				}
				else if (src.Value.Type == typeof(Material))
				{
					buttonMaterialRemove_Click(null, null);
				}
				else if (src.Value.Type == typeof(Texture2D))
				{
					buttonTextureRemove_Click(null, null);
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
			CrossRefsClear();
			DisposeRenderObjects();
			LoadFrame(-1);
			LoadMesh(-1);
			InitFrames();
			InitMeshes();
			InitMorphs();
			InitMaterials();
			InitTextures();
			RecreateRenderObjects();
			RecreateCrossRefs();
		}

		private void RecreateMeshes()
		{
			int oldLoadedMesh = loadedMesh;
			CrossRefsClear();
			DisposeRenderObjects();
			LoadMesh(-1);
			InitFrames();
			InitMeshes();
			InitMorphs();
			InitMaterials();
			InitTextures();
			RecreateRenderObjects();
			RecreateCrossRefs();
			LoadMaterial(loadedMaterial);
			if (oldLoadedMesh != -1 && oldLoadedMesh < Editor.Meshes.Count)
			{
				LoadMesh(oldLoadedMesh);
			}
		}

		private void RecreateMaterials()
		{
			CrossRefsClear();
			DisposeRenderObjects();
			LoadMaterial(-1);
			InitMaterials();
			InitTextures();
			RecreateRenderObjects();
			RecreateCrossRefs();
			LoadMesh(loadedMesh);
		}

		private void RecreateTextures()
		{
			CrossRefsClear();
			DisposeRenderObjects();
			LoadTexture(-1);
			InitTextures();
			RecreateRenderObjects();
			RecreateCrossRefs();
			LoadMaterial(loadedMaterial);
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
				CloseBoneNodes(treeViewObjectTree.Nodes);
				treeViewObjectTree.EndUpdate();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void CloseBoneNodes(TreeNodeCollection childs)
		{
			foreach (TreeNode child in childs)
			{
				if (child.Text.Contains(" Bones"))
				{
					child.Collapse();
				}
				else
				{
					CloseBoneNodes(child.Nodes);
				}
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

		private void buttonObjectTreeRefresh_Click(object sender, EventArgs e)
		{
			LoadAnimator(true);
			SyncWorkspaces();
		}

		void textBoxFrameName_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".SetFrameName(id=" + loadedFrame + ", name=\"" + textBoxFrameName.Text + "\")");
				Changed = Changed;

				RecreateRenderObjects();

				Transform frame = Editor.Frames[loadedFrame];
				TreeNode frameNode = FindFrameNode(frame, treeViewObjectTree.Nodes);
				frameNode.Text = frame.m_GameObject.instance.m_Name;
				List<TreeNode> boneNodes = new List<TreeNode>();
				FindBoneNodes(frame, treeViewObjectTree.Nodes[0].Nodes, boneNodes);
				foreach (TreeNode node in boneNodes)
				{
					node.Text = frame.m_GameObject.instance.m_Name;
					if (node.ForeColor == Color.OrangeRed)
					{
						node.ForeColor = treeViewObjectTree.ForeColor;
					}
				}
				SyncWorkspaces(frame.m_GameObject.instance.m_Name, typeof(Transform), loadedFrame);

				MeshRenderer frameMesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
				if (frameMesh == null)
				{
					frameMesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshRenderer);
				}
				if (frameMesh == null)
				{
					frameMesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.ParticleRenderer);
				}
				if (frameMesh != null)
				{
					RenameListViewItems(Editor.Meshes, listViewMesh, frameMesh, frame.m_GameObject.instance.m_Name);
					RenameListViewItems(Editor.Meshes, listViewMaterialMesh, frameMesh, frame.m_GameObject.instance.m_Name);
					RenameListViewItems(Editor.Meshes, listViewTextureMesh, frameMesh, frame.m_GameObject.instance.m_Name);
					if (loadedMesh >= 0 && Editor.Meshes[loadedMesh] == frameMesh)
					{
						textBoxRendererName.Text = Editor.Meshes[loadedMesh].m_GameObject.instance.m_Name;
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void comboBoxAvatar_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				Tuple<string, Component> item = (Tuple<string, Component>)comboBoxAvatar.SelectedItem;
				int componentIndex = Editor.Parser.file.Components.IndexOf(item.Item2);
				Gui.Scripting.RunScript(EditorVar + ".LoadAndSetAvatar(componentIndex=" + componentIndex + ")");

				if (item.Item2 is NotLoaded)
				{
					Avatar avatar = (Avatar)((NotLoaded)item.Item2).replacement;
					comboBoxAvatar.SelectedIndexChanged -= comboBoxAvatar_SelectedIndexChanged;
					comboBoxAvatar.Items[comboBoxAvatar.SelectedIndex] = new Tuple<string, Component>(item.Item1, avatar);
					comboBoxAvatar.SelectedIndexChanged += comboBoxAvatar_SelectedIndexChanged;
				}
				Changed = Changed;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMoveUp_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}

				var frame = Editor.Frames[loadedFrame];
				var parent = (Transform)frame.Parent;
				if (parent == null)
				{
					return;
				}

				int idx = parent.IndexOf(frame);
				if ((idx > 0) && (idx < parent.Count))
				{
					TreeNode node = FindFrameNode(frame, treeViewObjectTree.Nodes);
					TreeNode parentNode = node.Parent;
					bool selected = node.Equals(node.TreeView.SelectedNode);
					int nodeIdx = node.Index;
					node.TreeView.BeginUpdate();
					parentNode.Nodes.RemoveAt(nodeIdx);
					parentNode.Nodes.Insert(nodeIdx - 1, node);
					if (selected)
					{
						node.TreeView.SelectedNode = node;
					}
					node.TreeView.EndUpdate();

					var source = (DragSource)parentNode.Tag;
					Gui.Scripting.RunScript(EditorVar + ".MoveFrame(id=" + loadedFrame + ", parent=" + (int)source.Id + ", index=" + (idx - 1) + ")");
					Changed = Changed;
					SyncWorkspaces();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMoveDown_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}

				var frame = Editor.Frames[loadedFrame];
				var parent = (Transform)frame.Parent;
				if (parent == null)
				{
					return;
				}

				int idx = parent.IndexOf(frame);
				if ((idx >= 0) && (idx < (parent.Count - 1)))
				{
					TreeNode node = FindFrameNode(frame, treeViewObjectTree.Nodes);
					TreeNode parentNode = node.Parent;
					bool selected = node.Equals(node.TreeView.SelectedNode);
					int nodeIdx = node.Index;
					node.TreeView.BeginUpdate();
					parentNode.Nodes.RemoveAt(nodeIdx);
					parentNode.Nodes.Insert(nodeIdx + 1, node);
					if (selected)
					{
						node.TreeView.SelectedNode = node;
					}
					node.TreeView.EndUpdate();

					var source = (DragSource)parentNode.Tag;
					Gui.Scripting.RunScript(EditorVar + ".MoveFrame(id=" + loadedFrame + ", parent=" + (int)source.Id + ", index=" + (idx + 1) + ")");
					Changed = Changed;
					SyncWorkspaces();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameVirtualAnimator_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}

				if ((bool)Gui.Scripting.RunScript(EditorVar + ".CreateVirtualAnimator(id=" + loadedFrame + ")"))
				{
					RefreshFormUnity();
					LoadFrame(loadedFrame);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameRemove_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}
				if (Editor.Frames[loadedFrame].Parent == null)
				{
					Report.ReportLog("Can't remove the root frame");
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".RemoveFrame(id=" + loadedFrame + ")");
				Changed = Changed;

				RecreateFrames();
				SyncWorkspaces();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameAddBone_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}

				if (listViewMesh.SelectedItems.Count == 0)
				{
					return;
				}
				string meshNames = String.Empty;
				List<int> reselect = new List<int>(listViewMesh.SelectedIndices.Count);
				for (int i = 0; i < listViewMesh.SelectedItems.Count; i++)
				{
					int meshId = (int)listViewMesh.SelectedItems[i].Tag;
					meshNames += "\"" + Editor.Meshes[meshId].m_GameObject.instance.m_Name + "\", ";
					reselect.Add(meshId);
				}
				meshNames = meshNames.Substring(0, meshNames.Length - 2);

				Gui.Scripting.RunScript(EditorVar + ".AddBone(id=" + loadedFrame + ", meshes={ " + meshNames + " })");
				Changed = Changed;

				InitFrames();
				RecreateRenderObjects();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dataGridViewSRT_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			DataGridViewEditor.dataGridViewSRT_CellValueChanged(sender, e);
		}

		private void dataGridViewMatrix_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			DataGridViewEditor.dataGridViewMatrix_CellValueChanged(sender, e);
		}

		private void buttonFrameMatrixIdentity_Click(object sender, EventArgs e)
		{
			try
			{
				DataGridViewEditor.LoadMatrix(Matrix.Identity, dataGridViewFrameSRT, dataGridViewFrameMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMatrixCombined_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}

				Transform frame = Editor.Frames[loadedFrame];
				Matrix m = Transform.WorldTransform(frame);
				DataGridViewEditor.LoadMatrix(m, dataGridViewFrameSRT, dataGridViewFrameMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMatrixInverse_Click(object sender, EventArgs e)
		{
			try
			{
				Matrix m = DataGridViewEditor.GetMatrix(dataGridViewFrameMatrix);
				DataGridViewEditor.LoadMatrix(Matrix.Invert(m), dataGridViewFrameSRT, dataGridViewFrameMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMatrixGrow_Click(object sender, EventArgs e)
		{
			try
			{
				float ratio = Decimal.ToSingle(numericFrameMatrixRatio.Value);
				Vector3[] srt = DataGridViewEditor.GetSRT(dataGridViewFrameSRT);
				srt[0] = srt[0] * ratio;
				srt[2] = srt[2] * ratio;
				DataGridViewEditor.LoadMatrix(FbxUtility.SRTToMatrix(srt[0], srt[1], srt[2]), dataGridViewFrameSRT, dataGridViewFrameMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMatrixShrink_Click(object sender, EventArgs e)
		{
			try
			{
				float ratio = Decimal.ToSingle(numericFrameMatrixRatio.Value);
				Vector3[] srt = DataGridViewEditor.GetSRT(dataGridViewFrameSRT);
				srt[0] = srt[0] / ratio;
				srt[2] = srt[2] / ratio;
				DataGridViewEditor.LoadMatrix(FbxUtility.SRTToMatrix(srt[0], srt[1], srt[2]), dataGridViewFrameSRT, dataGridViewFrameMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMatrixCopy_Click(object sender, EventArgs e)
		{
			try
			{
				copyMatrices[Decimal.ToInt32(numericFrameMatrixNumber.Value) - 1] = DataGridViewEditor.GetMatrix(dataGridViewFrameMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMatrixPaste_Click(object sender, EventArgs e)
		{
			try
			{
				DataGridViewEditor.LoadMatrix(copyMatrices[Decimal.ToInt32(numericFrameMatrixNumber.Value) - 1], dataGridViewFrameSRT, dataGridViewFrameMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonFrameMatrixApply_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedFrame < 0)
				{
					return;
				}

				Matrix m = DataGridViewEditor.GetMatrix(dataGridViewFrameMatrix);
				string command = EditorVar + ".SetFrameMatrix(id=" + loadedFrame;
				for (int i = 0; i < 4; i++)
				{
					for (int j = 0; j < 4; j++)
					{
						command += ", m" + (i + 1) + (j + 1) + "=" + m[i, j].ToFloatString();
					}
				}
				command += ")";
				Gui.Scripting.RunScript(command);
				Changed = Changed;
				if (checkBoxFrameMatrixUpdate.Checked)
				{
					Transform frame = Editor.Frames[loadedFrame];
					m = Transform.WorldTransform(frame);
					m.Invert();

					for (int i = 0; i < Editor.Meshes.Count; i++)
					{
						SkinnedMeshRenderer smr = Editor.Meshes[i] as SkinnedMeshRenderer;
						if (smr != null && smr.m_Mesh.instance != null)
						{
							int boneIdx = Operations.FindBoneIndex(smr.m_Bones, frame);
							if (boneIdx >= 0)
							{
								command = EditorVar + ".SetBoneMatrix(meshId=" + i + ", boneId=" + boneIdx;
								for (int j = 0; j < 4; j++)
								{
									for (int k = 0; k < 4; k++)
									{
										command += ", m" + (j + 1) + (k + 1) + "=" + m[j, k].ToFloatString();
									}
								}
								command += ")";
								Gui.Scripting.RunScript(command);
							}
						}
					}
				}

				RecreateRenderObjects();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneGotoFrame_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedBone != null)
				{
					SkinnedMeshRenderer smr = (SkinnedMeshRenderer)Editor.Meshes[loadedBone[0]];
					Mesh mesh = smr.m_Mesh.instance;
					if (mesh != null)
					{
						Transform bone = smr.m_Bones[loadedBone[1]].instance;
						TreeNode node = FindFrameNode(bone, treeViewObjectTree.Nodes);
						if (node != null)
						{
							tabControlLists.SelectedTab = tabPageObject;
							treeViewObjectTree.SelectedNode = node;
							node.Expand();
							node.EnsureVisible();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneRemove_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedBone == null)
				{
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".RemoveBone(meshId=" + loadedBone[0] + ", boneId=" + loadedBone[1] + ")");
				Changed = Changed;

				LoadBone(null);
				RecreateRenderObjects();
				InitFrames();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneGetHash_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedBone == null || loadedFrame < 0)
				{
					return;
				}

				uint pathHash = (uint)Gui.Scripting.RunScript(EditorVar + ".GetTransformHash(id=" + loadedFrame + ")");
				editTextBoxBoneHash.Text = pathHash.ToString();
				editTextBoxBoneHash.Focus();
				editTextBoxBoneHash.Select(editTextBoxBoneHash.Text.Length, 0);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void editTextBoxBoneHash_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedBone == null)
				{
					return;
				}

				uint boneHash = uint.Parse(editTextBoxBoneHash.Text);
				Gui.Scripting.RunScript(EditorVar + ".SetMeshBoneHash(id=" + loadedBone[0] + ", boneId=" + loadedBone[1] + ", hash=" + boneHash + ")");
				Changed = Changed;

				SkinnedMeshRenderer sMesh = Editor.Meshes[loadedBone[0]] as SkinnedMeshRenderer;
				if (sMesh != null && sMesh.m_Bones[loadedBone[1]].instance != null)
				{
					TreeNode meshFrameNode = FindFrameNode(sMesh.m_GameObject.instance.m_Name, treeViewObjectTree.Nodes[0].Nodes);
					TreeNode meshNode = meshFrameNode.Nodes[0];
					TreeNode bonesNode = meshNode.Nodes[0];
					TreeNode boneNode = bonesNode.Nodes[loadedBone[1]];
					boneNode.Text = sMesh.m_Bones[loadedBone[1]].instance.m_GameObject.instance.m_Name;
					if (boneNode.ForeColor == Color.OrangeRed)
					{
						boneNode.ForeColor = treeViewObjectTree.ForeColor;
					}
					textBoxBoneName.Text = boneNode.Text;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneMatrixIdentity_Click(object sender, EventArgs e)
		{
			try
			{
				DataGridViewEditor.LoadMatrix(Matrix.Identity, dataGridViewBoneSRT, dataGridViewBoneMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneMatrixInverse_Click(object sender, EventArgs e)
		{
			try
			{
				Matrix m = DataGridViewEditor.GetMatrix(dataGridViewBoneMatrix);
				DataGridViewEditor.LoadMatrix(Matrix.Invert(m), dataGridViewBoneSRT, dataGridViewBoneMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneMatrixGrow_Click(object sender, EventArgs e)
		{
			try
			{
				float ratio = Decimal.ToSingle(numericBoneMatrixRatio.Value);
				Vector3[] srt = DataGridViewEditor.GetSRT(dataGridViewBoneSRT);
				srt[0] = srt[0] * ratio;
				srt[2] = srt[2] * ratio;
				DataGridViewEditor.LoadMatrix(FbxUtility.SRTToMatrix(srt[0], srt[1], srt[2]), dataGridViewBoneSRT, dataGridViewBoneMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneMatrixShrink_Click(object sender, EventArgs e)
		{
			try
			{
				float ratio = Decimal.ToSingle(numericBoneMatrixRatio.Value);
				Vector3[] srt = DataGridViewEditor.GetSRT(dataGridViewBoneSRT);
				srt[0] = srt[0] / ratio;
				srt[2] = srt[2] / ratio;
				DataGridViewEditor.LoadMatrix(FbxUtility.SRTToMatrix(srt[0], srt[1], srt[2]), dataGridViewBoneSRT, dataGridViewBoneMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneMatrixCopy_Click(object sender, EventArgs e)
		{
			try
			{
				copyMatrices[Decimal.ToInt32(numericBoneMatrixNumber.Value) - 1] = DataGridViewEditor.GetMatrix(dataGridViewBoneMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneMatrixPaste_Click(object sender, EventArgs e)
		{
			try
			{
				DataGridViewEditor.LoadMatrix(copyMatrices[Decimal.ToInt32(numericBoneMatrixNumber.Value) - 1], dataGridViewBoneSRT, dataGridViewBoneMatrix);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonBoneMatrixApply_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedBone == null)
				{
					return;
				}

				Matrix m = DataGridViewEditor.GetMatrix(dataGridViewBoneMatrix);
				string command = EditorVar + ".SetBoneMatrix(meshId=" + loadedBone[0] + ", boneId=" + loadedBone[1];
				for (int i = 0; i < 4; i++)
				{
					for (int j = 0; j < 4; j++)
					{
						command += ", m" + (i + 1) + (j + 1) + "=" + m[i, j].ToFloatString();
					}
				}
				command += ")";
				Gui.Scripting.RunScript(command);
				Changed = Changed;
				if (checkBoxBoneMatrixUpdate.Checked)
				{
					Matrix newBoneMatrix = m;
					SkinnedMeshRenderer meshFromBone = (SkinnedMeshRenderer)Editor.Meshes[loadedBone[0]];
					Transform frame = meshFromBone.m_Bones[loadedBone[1]].instance;
					m = Transform.WorldTransform(frame.Parent);
					Matrix boneFrameMatrix = newBoneMatrix;
					boneFrameMatrix.Invert();
					m.Invert();
					boneFrameMatrix = boneFrameMatrix * m;

					command = EditorVar + ".SetFrameMatrix(id=" + Editor.Frames.IndexOf(frame);
					for (int i = 0; i < 4; i++)
					{
						for (int j = 0; j < 4; j++)
						{
							command += ", m" + (i + 1) + (j + 1) + "=" + boneFrameMatrix[i, j].ToFloatString();
						}
					}
					command += ")";
					Gui.Scripting.RunScript(command);

					for (int i = 0; i < Editor.Meshes.Count; i++)
					{
						SkinnedMeshRenderer smr = Editor.Meshes[i] as SkinnedMeshRenderer;
						if (smr != null && smr != meshFromBone && smr.m_Mesh.instance != null)
						{
							int boneIdx = Operations.FindBoneIndex(smr.m_Bones, frame);
							if (boneIdx >= 0)
							{
								command = EditorVar + ".SetBoneMatrix(meshId=" + i + ", boneId=" + boneIdx;
								for (int j = 0; j < 4; j++)
								{
									for (int k = 0; k < 4; k++)
									{
										command += ", m" + (j + 1) + (k + 1) + "=" + newBoneMatrix[j, k].ToFloatString();
									}
								}
								command += ")";
								Gui.Scripting.RunScript(command);
							}
						}
					}
				}

				RecreateRenderObjects();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void listViewMesh_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			try
			{
				if (listViewItemSyncSelectedSent == false)
				{
					listViewItemSyncSelectedSent = true;
					listViewMeshMaterial.BeginUpdate();
					listViewMeshTexture.BeginUpdate();

					int id = (int)e.Item.Tag;
					if (e.IsSelected)
					{
						tabControlViews.SelectTabWithoutLoosingFocus(tabPageMeshView);
						LoadMesh(id);
						CrossRefAddItem(crossRefMeshMaterials[id], crossRefMeshMaterialsCount, listViewMeshMaterial, listViewMaterial);
						CrossRefAddItem(crossRefMeshTextures[id], crossRefMeshTexturesCount, listViewMeshTexture, listViewTexture);

						if (!Gui.Docking.DockRenderer.IsHidden)
						{
							if (renderObjectMeshes[id] == null)
							{
								MeshRenderer meshR = Editor.Meshes[id];
								Transform meshTransform = meshR.m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform);
								HashSet<string> meshNames = new HashSet<string>() { meshTransform.GetTransformPath() };
								renderObjectMeshes[id] = new RenderObjectUnity(Editor, meshNames);
							}
							RenderObjectUnity renderObj = renderObjectMeshes[id];
							if (renderObjectIds[id] == -1)
							{
								renderObjectIds[id] = Gui.Renderer.AddRenderObject(renderObj);
							}

							Gui.Docking.DockRenderer.Enabled = false;
							Gui.Docking.DockRenderer.Activate();
							Gui.Docking.DockRenderer.Enabled = true;
							if ((bool)Gui.Config["AutoCenterView"])
							{
								Gui.Renderer.CenterView();
							}
						}
					}
					else
					{
						if (id == loadedMesh)
						{
							LoadMesh(-1);
						}
						CrossRefRemoveItem(crossRefMeshMaterials[id], crossRefMeshMaterialsCount, listViewMeshMaterial);
						CrossRefRemoveItem(crossRefMeshTextures[id], crossRefMeshTexturesCount, listViewMeshTexture);

						if (renderObjectIds != null && renderObjectIds.Count > id && renderObjectIds[id] >= 0)
						{
							Gui.Renderer.RemoveRenderObject(renderObjectIds[id]);
							renderObjectIds[id] = -1;
						}
					}

					CrossRefSetSelected(e.IsSelected, listViewMesh, id);
					CrossRefSetSelected(e.IsSelected, listViewMaterialMesh, id);
					CrossRefSetSelected(e.IsSelected, listViewTextureMesh, id);

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

		private void listViewMesh_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION || loadedMesh == -1)
			{
				return;
			}
			buttonMeshRemove_Click(null, null);
		}

		private void comboBoxMeshRendererMesh_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				Tuple<string, Component> item = (Tuple<string, Component>)comboBoxMeshRendererMesh.SelectedItem;
				int componentIndex = Editor.Parser.file.Components.IndexOf(item.Item2);
				Gui.Scripting.RunScript(EditorVar + ".LoadAndSetMesh(meshId=" + loadedMesh + ", componentIndex=" + componentIndex + ")");
				Changed = Changed;

				RecreateMeshes();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void comboBoxRendererRootBone_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				Tuple<string, int> item = (Tuple<string, int>)comboBoxRendererRootBone.SelectedItem;
				Gui.Scripting.RunScript(EditorVar + ".SetSkinnedMeshRendererRootBone(meshId=" + loadedMesh + ", frameId=" + item.Item2 + ")");
				Changed = Changed;

				RecreateMeshes();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void comboBoxMeshExportFormat_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				switch ((MeshExportFormat)comboBoxMeshExportFormat.SelectedIndex)
				{
				case MeshExportFormat.Mqo:
					panelMeshExportOptionsMqo.BringToFront();
					break;
				case MeshExportFormat.Fbx:
				case MeshExportFormat.Fbx_2006:
				case MeshExportFormat.ColladaFbx:
				case MeshExportFormat.Dxf:
				case MeshExportFormat.Obj:
					panelMeshExportOptionsFbx.BringToFront();
					break;
				default:
					panelMeshExportOptionsDefault.BringToFront();
					break;
				}

				MeshExportFormat[] values = Enum.GetValues(typeof(MeshExportFormat)) as MeshExportFormat[];
				string description = values[comboBoxMeshExportFormat.SelectedIndex].GetDescription();
				Gui.Config["MeshExportFormat"] = description;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMeshExport_Click(object sender, EventArgs e)
		{
			try
			{
				DirectoryInfo dir = new DirectoryInfo(exportDir);

				StringBuilder meshes = new StringBuilder(1000);
				if (!checkBoxMeshExportNoMesh.Checked)
				{
					if (listViewMesh.SelectedItems.Count > 0)
					{
						for (int i = 0; i < listViewMesh.SelectedItems.Count; i++)
						{
							meshes.Append(EditorVar).Append(".Meshes[").Append((int)listViewMesh.SelectedItems[i].Tag).Append("], ");
						}
					}
					else
					{
						if (listViewMesh.Items.Count <= 0)
						{
							Report.ReportLog("There are no meshes for exporting");
							return;
						}

						for (int i = 0; i < listViewMesh.Items.Count; i++)
						{
							meshes.Append(EditorVar).Append(".Meshes[").Append((int)listViewMesh.Items[i].Tag).Append("], ");
						}
					}
					meshes.Insert(0, "{ ");
					meshes.Length -= 2;
					meshes.Append(" }");
				}
				else
				{
					meshes.Append("null");
				}

				Report.ReportLog("Started exporting to " + comboBoxMeshExportFormat.SelectedItem + " format...");
				Application.DoEvents();

				string xaVars = "null";

				int startKeyframe = -1;
				int endKeyframe = 0;
				bool linear = checkBoxMeshExportFbxLinearInterpolation.Checked;

				switch ((MeshExportFormat)comboBoxMeshExportFormat.SelectedIndex)
				{
				case MeshExportFormat.Mqo:
					Gui.Scripting.RunScript("ExportMqo(parser=" + ParserVar + ", meshes=" + meshes + ", dirPath=\"" + dir.FullName + "\", singleMqo=" + checkBoxMeshExportMqoSingleFile.Checked + ", worldCoords=" + checkBoxMeshExportMqoWorldCoords.Checked + ", sortMeshes=" + checkBoxMeshExportMqoSortMeshes.Checked + ")");
					break;
				case MeshExportFormat.ColladaFbx:
					Gui.Scripting.RunScript("ExportFbx(animator=" + ParserVar + ", meshes=" + meshes + ", animationParsers=" + xaVars + ", startKeyframe=" + startKeyframe + ", endKeyframe=" + endKeyframe + ", linear=" + linear + ", EulerFilter=" + (bool)Gui.Config["FbxExportAnimationEulerFilter"] + ", filterPrecision=" + ((float)Gui.Config["FbxExportAnimationFilterPrecision"]).ToFloatString() + ", path=\"" + Utility.GetDestFile(dir, "meshes", ".dae") + "\", exportFormat=\".dae\", allFrames=" + checkBoxMeshExportFbxAllFrames.Checked + ", allBones=" + checkBoxMeshExportAllBones.Checked + ", skins=" + checkBoxMeshExportFbxSkins.Checked + ", compatibility=" + false + ")");
					break;
				case MeshExportFormat.Fbx:
					Gui.Scripting.RunScript("ExportFbx(animator=" + ParserVar + ", meshes=" + meshes + ", animationParsers=" + xaVars + ", startKeyframe=" + startKeyframe + ", endKeyframe=" + endKeyframe + ", linear=" + linear + ", EulerFilter=" + (bool)Gui.Config["FbxExportAnimationEulerFilter"] + ", filterPrecision=" + ((float)Gui.Config["FbxExportAnimationFilterPrecision"]).ToFloatString() + ", path=\"" + Utility.GetDestFile(dir, "meshes", ".fbx") + "\", exportFormat=\".fbx\", allFrames=" + checkBoxMeshExportFbxAllFrames.Checked + ", allBones=" + checkBoxMeshExportAllBones.Checked + ", skins=" + checkBoxMeshExportFbxSkins.Checked + ", compatibility=" + false + ")");
					break;
				case MeshExportFormat.Fbx_2006:
					Gui.Scripting.RunScript("ExportFbx(animator=" + ParserVar + ", meshes=" + meshes + ", animationParsers=" + xaVars + ", startKeyframe=" + startKeyframe + ", endKeyframe=" + endKeyframe + ", linear=" + linear + ", EulerFilter=" + (bool)Gui.Config["FbxExportAnimationEulerFilter"] + ", filterPrecision=" + ((float)Gui.Config["FbxExportAnimationFilterPrecision"]).ToFloatString() + ", path=\"" + Utility.GetDestFile(dir, "meshes", ".fbx") + "\", exportFormat=\".fbx\", allFrames=" + checkBoxMeshExportFbxAllFrames.Checked + ", allBones=" + checkBoxMeshExportAllBones.Checked + ", skins=" + checkBoxMeshExportFbxSkins.Checked + ", compatibility=" + true + ")");
					break;
				case MeshExportFormat.Dxf:
					Gui.Scripting.RunScript("ExportFbx(animator=" + ParserVar + ", meshes=" + meshes + ", animationParsers=" + xaVars + ", startKeyframe=" + startKeyframe + ", endKeyframe=" + endKeyframe + ", linear=" + linear + ", EulerFilter=" + (bool)Gui.Config["FbxExportAnimationEulerFilter"] + ", filterPrecision=" + ((float)Gui.Config["FbxExportAnimationFilterPrecision"]).ToFloatString() + ", path=\"" + Utility.GetDestFile(dir, "meshes", ".dxf") + "\", exportFormat=\".dxf\", allFrames=" + checkBoxMeshExportFbxAllFrames.Checked + ", allBones=" + checkBoxMeshExportAllBones.Checked + ", skins=" + checkBoxMeshExportFbxSkins.Checked + ", compatibility=" + false + ")");
					break;
				case MeshExportFormat.Obj:
					Gui.Scripting.RunScript("ExportFbx(animator=" + ParserVar + ", meshes=" + meshes + ", animationParsers=" + xaVars + ", startKeyframe=" + startKeyframe + ", endKeyframe=" + endKeyframe + ", linear=" + linear + ", EulerFilter=" + (bool)Gui.Config["FbxExportAnimationEulerFilter"] + ", filterPrecision=" + ((float)Gui.Config["FbxExportAnimationFilterPrecision"]).ToFloatString() + ", path=\"" + Utility.GetDestFile(dir, "meshes", ".obj") + "\", exportFormat=\".obj\", allFrames=" + checkBoxMeshExportFbxAllFrames.Checked + ", allBones=" + checkBoxMeshExportAllBones.Checked + ", skins=" + checkBoxMeshExportFbxSkins.Checked + ", compatibility=" + false + ")");
					break;
				default:
					throw new Exception("Unexpected ExportFormat");
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void checkBoxMeshExportMqoSortMeshes_CheckedChanged(object sender, EventArgs e)
		{
			Gui.Config["ExportMqoSortMeshes"] = checkBoxMeshExportMqoSortMeshes.Checked;
		}

		private void buttonMeshGotoFrame_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedMesh >= 0)
				{
					TreeNode node = FindFrameNode(Editor.Meshes[loadedMesh].m_GameObject.instance.FindLinkedComponent(UnityClassID.Transform), treeViewObjectTree.Nodes);
					if (node != null)
					{
						tabControlLists.SelectedTab = tabPageObject;
						treeViewObjectTree.SelectedNode = node;
						node.Expand();
						node.EnsureVisible();
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMeshRemove_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedMesh < 0)
				{
					return;
				}

				List<int> descendingIds = new List<int>(listViewMesh.SelectedItems.Count);
				foreach (ListViewItem item in listViewMesh.SelectedItems)
				{
					descendingIds.Add((int)item.Tag);
				}
				descendingIds.Sort();
				descendingIds.Reverse();
				foreach (int id in descendingIds)
				{
					Gui.Scripting.RunScript(EditorVar + ".RemoveMeshRenderer(id=" + id + ")");
				}
				Changed = Changed;

				loadedMesh = -1;
				RecreateMeshes();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMeshNormals_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedMesh < 0)
				{
					return;
				}

				using (var normals = new FormNormalsAndTangents())
				{
					if (normals.ShowDialog() == DialogResult.OK)
					{
						string editors = "editors={";
						string numMeshes = "numMeshes={";
						string meshes = "meshes={";
						List<DockContent> animatorList = null;
						Gui.Docking.DockContents.TryGetValue(typeof(FormAnimator), out animatorList);
						foreach (FormAnimator animator in animatorList)
						{
							if (animator.listViewMesh.SelectedItems.Count == 0)
							{
								continue;
							}

							editors += animator.EditorVar + ", ";
							numMeshes += animator.listViewMesh.SelectedItems.Count + ", ";
							foreach (ListViewItem item in animator.listViewMesh.SelectedItems)
							{
								meshes += (int)item.Tag + ", ";
							}
						}
						string idArgs = editors.Substring(0, editors.Length - 2) + "}, " + numMeshes.Substring(0, numMeshes.Length - 2) + "}, " + meshes.Substring(0, meshes.Length - 2) + "}";
						if (normals.checkBoxNormalsForSelectedMeshes.Checked)
						{
							Gui.Scripting.RunScript(EditorVar + ".CalculateNormals(" + idArgs + ", threshold=" + ((float)normals.numericThreshold.Value).ToFloatString() + ")");
							Changed = Changed;
						}
						if (normals.checkBoxCalculateTangents.Checked)
						{
							Gui.Scripting.RunScript(EditorVar + ".CalculateTangents(" + idArgs + ")");
							Changed = Changed;
						}

						RecreateRenderObjects();
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonSkinnedMeshRendererAttributes_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedMesh < 0)
				{
					return;
				}

				using (var attributesDialog = new FormRendererMeshAttributes(Editor.Meshes[loadedMesh], dataGridViewMesh.SelectedRows.Count > 0 ? dataGridViewMesh.SelectedRows[0].Index : -1))
				{
					if (attributesDialog.ShowDialog() == DialogResult.OK)
					{
						if (Operations.GetMesh(Editor.Meshes[loadedMesh]) != null)
						{
							Gui.Scripting.RunScript(EditorVar + ".SetMeshAttributes(id=" + loadedMesh + ", readable=" + attributesDialog.checkBoxMeshReadable.Checked + ", keepVertices=" + attributesDialog.checkBoxMeshKeepVertices.Checked + ", keepIndices=" + attributesDialog.checkBoxMeshKeepIndices.Checked + ", usageFlags=" + attributesDialog.editTextBoxMeshUsageFlags.Text + ")");
						}
						if (Editor.Meshes[loadedMesh] is SkinnedMeshRenderer)
						{
							Gui.Scripting.RunScript(EditorVar + ".SetSkinnedMeshRendererAttributes(id=" + loadedMesh + ", quality=" + attributesDialog.editTextBoxSkinnedMeshRendererQuality.Text + ", updateWhenOffScreen=" + attributesDialog.checkBoxSkinnedMeshRendererUpdateWhenOffScreen.Checked + ", dirtyAABB=" + attributesDialog.checkBoxSkinnedMeshRendererDirtyAABB.Checked + ")");
						}
						Gui.Scripting.RunScript(EditorVar + ".SetRendererAttributes(id=" + loadedMesh + ", castShadows=" + attributesDialog.checkBoxRendererCastShadows.Checked + ", receiveShadows=" + attributesDialog.checkBoxRendererReceiveShadows.Checked + ", lightmap=" + attributesDialog.editTextBoxRendererLightMap.Text + ", lightProbes=" + attributesDialog.checkBoxRendererUseLightProbes.Checked + ", sortingLayer= " + attributesDialog.editTextBoxRendererSortingLayerID.Text + ", sortingOrder=" + attributesDialog.editTextBoxRendererSortingOrder.Text + ")");
						Changed = Changed;
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void editTextBoxMeshName_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedMesh < 0)
				{
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".SetMeshName(id=" + loadedMesh + ", name=\"" + editTextBoxMeshName.Text + "\")");
				Changed = Changed;
				RecreateMeshes();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dataGridViewMesh_SelectionChanged(object sender, EventArgs e)
		{
			try
			{
				/*if (!checkBoxSubmeshReorder.Checked)
				{*/
					HighlightSubmeshes();
				/*}
				else
				{
					Gui.Scripting.RunScript(EditorVar + ".MoveSubmesh(meshId=" + loadedMesh + ", submeshId=" + (int)checkBoxSubmeshReorder.Tag + ", newPosition=" + dataGridViewMesh.SelectedRows[0].Index + ")");
					Changed = Changed;

					RecreateRenderObjects();
					int pos = dataGridViewMesh.SelectedRows[0].Index;
					DataGridViewRow src = dataGridViewMesh.Rows[(int)checkBoxSubmeshReorder.Tag];
					dataGridViewMesh.Rows.Remove(src);
					dataGridViewMesh.Rows.Insert(pos, src);

					checkBoxSubmeshReorder.Text = "Reorder";
					checkBoxSubmeshReorder.Checked = false;
				}*/
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void HighlightSubmeshes()
		{
			if (loadedMesh < 0 || Gui.Docking.DockRenderer.IsHidden)
			{
				return;
			}

			RenderObjectUnity renderObj = renderObjectMeshes[loadedMesh];
			if (renderObj != null)
			{
				renderObj.HighlightSubmesh.Clear();
				foreach (DataGridViewRow row in dataGridViewMesh.SelectedRows)
				{
					renderObj.HighlightSubmesh.Add(row.Index);
				}
				Gui.Renderer.Render();
			}
		}

		private void dataGridViewMesh_KeyDown(object sender, KeyEventArgs e)
		{
			try
			{
				if (e.KeyData == Keys.Escape)
				{
					while (dataGridViewMesh.SelectedRows.Count > 0)
					{
						dataGridViewMesh.SelectedRows[0].Selected = false;
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dataGridViewMesh_CellClick(object sender, DataGridViewCellEventArgs e)
		{
			try
			{
				if ((dataGridViewMesh.CurrentRow != null) && (dataGridViewMesh.CurrentCell.ColumnIndex == 2))
				{
					dataGridViewMesh.BeginEdit(true);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

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
							comboBoxCell.SelectionChangeCommitted -= new EventHandler(comboBoxCell_SelectionChangeCommitted);

							//Add the event handler.
							comboBoxCell.SelectionChangeCommitted += new EventHandler(comboBoxCell_SelectionChangeCommitted);
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

		private void comboBoxCell_SelectionChangeCommitted(object sender, EventArgs e)
		{
			try
			{
				ComboBox combo = (ComboBox)sender;
				if (combo.SelectedValue == null)
				{
					return;
				}

				List<Tuple<string, int, Component>> columnMaterials = (List<Tuple<string, int, Component>>)ColumnSubmeshMaterial.DataSource;
				int matIdx = combo.SelectedIndex != -1 ? columnMaterials[combo.SelectedIndex].Item2 : -1;
				int rowIdx = dataGridViewMesh.CurrentCell.RowIndex;
				MeshRenderer meshRenderer = Editor.Meshes[loadedMesh];
				Material subMeshMat = meshRenderer.m_Materials[rowIdx].instance;
				if (subMeshMat == null || Editor.Materials.IndexOf(subMeshMat) != matIdx)
				{
					dataGridViewMesh.CommitEdit(DataGridViewDataErrorContexts.Commit);

					Mesh mesh = Operations.GetMesh(meshRenderer);
					if (mesh != null)
					{
						if (matIdx == -1)
						{
							Gui.Scripting.RunScript(EditorVar + ".LoadAndSetSubMeshMaterial(meshId=" + loadedMesh + ", subMeshId=" + rowIdx + ", componentIndex=" + Editor.Parser.file.Components.IndexOf(columnMaterials[combo.SelectedIndex].Item3) + ")");
							Changed = Changed;
						}
						else if (Editor.Materials[matIdx] != subMeshMat)
						{
							Gui.Scripting.RunScript(EditorVar + ".SetSubMeshMaterial(meshId=" + loadedMesh + ", subMeshId=" + rowIdx + ", material=" + matIdx + ")");
							Changed = Changed;
						}
						else
						{
							return;
						}

						if (matIdx == -1)
						{
							InitMaterials();
							InitTextures();
						}
						RecreateRenderObjects();
						RecreateCrossRefs();
						subMeshMat = meshRenderer.m_Materials[rowIdx].instance;
						dataGridViewMesh.CurrentCell.Value = Editor.Materials.IndexOf(subMeshMat);
						combo.SelectedValue = dataGridViewMesh.CurrentCell.Value;
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dataGridViewMesh_DataError(object sender, DataGridViewDataErrorEventArgs e)
		{
			e.ThrowException = false;
		}

		private void dataGridViewMesh_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			if (e.ColumnIndex == 3)
			{
				int topology = int.Parse((string)dataGridViewMesh.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
				Gui.Scripting.RunScript(EditorVar + ".SetSubMeshTopology(meshId=" + loadedMesh + ", subMeshId=" + e.RowIndex + ", topology=" + topology + ")");
				Changed = Changed;
			}
		}

		private void buttonMeshMirrorV_Click(object sender, EventArgs e)
		{
			if (loadedMesh < 0)
			{
				return;
			}

			try
			{
				Gui.Scripting.RunScript(EditorVar + ".MirrorV(meshId=" + loadedMesh + ")");
				Changed = Changed;

				RecreateRenderObjects();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMeshSubmeshAddDeleteMaterial_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedMesh < 0)
				{
					return;
				}

				if (sender == buttonMeshSubmeshAddMaterial)
				{
					Gui.Scripting.RunScript(EditorVar + ".AddRendererMaterial(meshId=" + loadedMesh + ", materialId=" + -1 + ")");
				}
				else if (Editor.Meshes[loadedMesh].m_Materials.Count > 0)
				{
					Gui.Scripting.RunScript(EditorVar + ".RemoveRendererMaterial(meshId=" + loadedMesh + ")");
				}
				LoadMesh(loadedMesh);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMeshSubmeshRemove_Click(object sender, EventArgs e)
		{
			try
			{
				if ((loadedMesh < 0) || (dataGridViewMesh.SelectedRows.Count <= 0))
				{
					return;
				}

				dataGridViewMesh.SelectionChanged -= new EventHandler(dataGridViewMesh_SelectionChanged);

				int lastSelectedRow = -1;
				List<int> indices = new List<int>();
				foreach (DataGridViewRow row in dataGridViewMesh.SelectedRows)
				{
					indices.Add(row.Index);
					lastSelectedRow = row.Index;
				}
				indices.Sort();

				Mesh mesh = Operations.GetMesh(Editor.Meshes[loadedMesh]);
				int numSubmeshes = mesh != null ? mesh.m_SubMeshes.Count : 0;
				for (int i = 0; i < indices.Count; i++)
				{
					int index = indices[i] - i;
					if (index < numSubmeshes)
					{
						Gui.Scripting.RunScript(EditorVar + ".RemoveSubMesh(meshId=" + loadedMesh + ", subMeshId=" + index + ")");
						Changed = Changed;
					}
				}

				dataGridViewMesh.SelectionChanged += new EventHandler(dataGridViewMesh_SelectionChanged);

				bool meshRemoved = Operations.GetMesh(Editor.Meshes[loadedMesh]) == null;
				if (meshRemoved)
				{
					RecreateMeshes();
				}
				else
				{
					LoadMesh(loadedMesh);
					if (lastSelectedRow == dataGridViewMesh.Rows.Count)
					{
						lastSelectedRow--;
					}
					dataGridViewMesh.Rows[lastSelectedRow].Selected = true;
					dataGridViewMesh.FirstDisplayedScrollingRowIndex = lastSelectedRow;
					RecreateRenderObjects();
					RecreateCrossRefs();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeViewMorphKeyframe_AfterSelect(object sender, TreeViewEventArgs e)
		{
			try
			{
				tabControlViews.SelectTabWithoutLoosingFocus(tabPageMorphView);
				checkBoxMorphNormals.CheckedChanged -= checkBoxMorphNormals_CheckedChanged;
				checkBoxMorphTangents.CheckedChanged -= checkBoxMorphTangents_CheckedChanged;
				if (e.Node.Tag is int)
				{
					SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)e.Node.Parent.Tag;
					Mesh mesh = Operations.GetMesh(sMesh);
					if (mesh != null)
					{
						int shapeIndex = (int)e.Node.Tag;
						editTextBoxMorphKeyframe.Text = mesh.m_Shapes.channels[shapeIndex].name;
						editTextBoxMorphKeyframeHash.Text = mesh.m_Shapes.channels[shapeIndex].nameHash.ToString("X");
						textBoxMorphFrameIndex.Text = mesh.m_Shapes.channels[shapeIndex].frameIndex.ToString();
						editTextBoxMorphFrameCount.Text = mesh.m_Shapes.channels[shapeIndex].frameCount.ToString();
						checkBoxMorphNormals.Checked = mesh.m_Shapes.shapes[shapeIndex].hasNormals;
						checkBoxMorphTangents.Checked = mesh.m_Shapes.shapes[shapeIndex].hasTangents;
						editTextBoxMorphWeightRange.Text = sMesh.m_BlendShapeWeights[shapeIndex].ToFloatString() + "-" + mesh.m_Shapes.fullWeights[shapeIndex].ToFloatString();
					}
				}
				else
				{
					editTextBoxMorphKeyframe.Text = String.Empty;
					editTextBoxMorphKeyframeHash.Text = String.Empty;
					textBoxMorphFrameIndex.Text = String.Empty;
					editTextBoxMorphFrameCount.Text = String.Empty;
					checkBoxMorphNormals.Checked = false;
					checkBoxMorphTangents.Checked = false;
					editTextBoxMorphWeightRange.Text = String.Empty;
				}
				checkBoxMorphTangents.CheckedChanged += checkBoxMorphTangents_CheckedChanged;
				checkBoxMorphNormals.CheckedChanged += checkBoxMorphNormals_CheckedChanged;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void checkBoxMorphTangents_CheckedChanged(object sender, EventArgs e)
		{
		}

		void checkBoxMorphNormals_CheckedChanged(object sender, EventArgs e)
		{
		}

		void editTextBoxMorphWeightRange_AfterEditTextChanged(object sender, EventArgs e)
		{
		}

		void editTextBoxMorphKeyframe_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (treeViewMorphKeyframes.SelectedNode.Tag is int)
				{
					int index = (int)treeViewMorphKeyframes.SelectedNode.Tag;
					SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)treeViewMorphKeyframes.SelectedNode.Parent.Tag;
					Gui.Scripting.RunScript(EditorVar + ".RenameMorphKeyframe(meshId=" + Editor.Meshes.IndexOf(sMesh) + ", morphIndex=" + index + ", name=\"" + editTextBoxMorphKeyframe.Text + "\")");
					InitMorphs();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void editTextBoxMorphFrameIndexCount_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (treeViewMorphKeyframes.SelectedNode.Tag is int)
				{
					int index = (int)treeViewMorphKeyframes.SelectedNode.Tag;
					SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)treeViewMorphKeyframes.SelectedNode.Parent.Tag;
					Gui.Scripting.RunScript(EditorVar + ".SetMorphKeyframeIndexCount(meshId=" + Editor.Meshes.IndexOf(sMesh) + ", morphIndex=" + index + ", index=" + textBoxMorphFrameIndex.Text + ", count=" + editTextBoxMorphFrameCount.Text + ")");
					InitMorphs();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void checkBoxStartEndKeyframe_Click(object sender, EventArgs e)
		{
			try
			{
				CheckBox origin = (CheckBox)sender;
				if (!origin.Checked)
				{
					Tuple<RenderObjectUnity, SkinnedMeshRenderer> tuple = (Tuple<RenderObjectUnity, SkinnedMeshRenderer>)origin.Tag;
					if (tuple != null)
					{
						float unsetMorphFactor = tuple.Item1.UnsetMorphKeyframe(tuple.Item2, sender == checkBoxMorphStartKeyframe);
						Gui.Renderer.Render();
						trackBarMorphFactor.ValueChanged -= trackBarMorphFactor_ValueChanged;
						trackBarMorphFactor.Value = (int)(trackBarMorphFactor.Maximum * unsetMorphFactor);
						trackBarMorphFactor.ValueChanged += trackBarMorphFactor_ValueChanged;
					}
					origin.Text = sender == checkBoxMorphStartKeyframe ? "Start" : "End";
					return;
				}

				if (treeViewMorphKeyframes.SelectedNode == null || !(treeViewMorphKeyframes.SelectedNode.Tag is int))
				{
					return;
				}

				SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)treeViewMorphKeyframes.SelectedNode.Parent.Tag;
				int blendShapeIndex = (int)treeViewMorphKeyframes.SelectedNode.Tag;
				RenderObjectUnity renderObj = renderObjectMeshes[Editor.Meshes.IndexOf(sMesh)];
				if (renderObj == null)
				{
					Report.ReportLog("Mesh " + sMesh.m_GameObject.instance.m_Name + " not displayed.");
					return;
				}
				float setMorphFactor = renderObj.SetMorphKeyframe(sMesh, blendShapeIndex, sender == checkBoxMorphStartKeyframe);
				origin.Tag = new Tuple<RenderObjectUnity, SkinnedMeshRenderer>(renderObj, sMesh);
				Gui.Renderer.Render();
				trackBarMorphFactor.ValueChanged -= trackBarMorphFactor_ValueChanged;
				trackBarMorphFactor.Value = (int)(trackBarMorphFactor.Maximum * setMorphFactor);
				trackBarMorphFactor.ValueChanged += trackBarMorphFactor_ValueChanged;
				origin.Text = Operations.BlendShapeKeyframeName(Operations.GetMesh(sMesh), blendShapeIndex);
				toolTip1.SetToolTip(origin, Graphics.FromHwnd(Handle).MeasureString(origin.Text, origin.Font).Width >= origin.Width - 6 ? origin.Text : null);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void trackBarMorphFactor_ValueChanged(object sender, EventArgs e)
		{
			try
			{
				Tuple<RenderObjectUnity, SkinnedMeshRenderer> tuple = (Tuple<RenderObjectUnity, SkinnedMeshRenderer>)checkBoxMorphStartKeyframe.Tag;
				if (tuple != null)
				{
					tuple.Item1.SetTweenFactor(tuple.Item2, trackBarMorphFactor.Value / (float)trackBarMorphFactor.Maximum);
					Gui.Renderer.Render();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMorphClipExport_Click(object sender, EventArgs e)
		{
			try
			{
				if (treeViewMorphKeyframes.SelectedNode == null)
				{
					Report.ReportLog("No morph clip was selected");
					return;
				}
				if (listViewMesh.SelectedIndices.Count == 0)
				{
					Report.ReportLog("No mesh was selected");
					return;
				}

				TreeNode meshNode = treeViewMorphKeyframes.SelectedNode;
				if (meshNode.Parent != null)
				{
					meshNode = meshNode.Parent;
				}
				SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshNode.Tag;
				StringBuilder meshes = new StringBuilder(1000);
				if (listViewMesh.SelectedItems.Count > 0)
				{
					for (int i = 0; i < listViewMesh.SelectedItems.Count; i++)
					{
						meshes.Append(EditorVar).Append(".Meshes[").Append((int)listViewMesh.SelectedItems[i].Tag).Append("], ");
					}
				}
				meshes.Insert(0, "{ ");
				meshes.Length -= 2;
				meshes.Append(" }");

				string path = Editor.Parser.file.Parser.FilePath;
				if (path.ToLower().EndsWith(".unity3d"))
				{
					path = path.Substring(0, path.Length - 8);
				}
				else if (path.ToLower().EndsWith(".assets"))
				{
					path = path.Substring(0, path.Length - 7);
				}
				path += @"\" + Editor.Parser.m_GameObject.instance.m_Name;
				switch ((MorphExportFormat)comboBoxMorphExportFormat.SelectedIndex)
				{
				case MorphExportFormat.Mqo:
					Gui.Scripting.RunScript("ExportMorphMqo(dirPath=\"" + path + "\", parser=" + ParserVar + ", meshName=\"" + sMesh.m_GameObject.instance.m_Name + "\")");
					break;
				case MorphExportFormat.Fbx:
				case MorphExportFormat.Fbx_2006:
					Mesh mesh = Operations.GetMesh(sMesh);
					DirectoryInfo dir = new DirectoryInfo(path);
					path = Utility.GetDestFile(dir, sMesh.m_GameObject.instance.m_Name + "-" + (mesh != null ? Operations.BlendShapeName(mesh) : "") + "-", ".fbx");
					Gui.Scripting.RunScript("ExportMorphFbx(animator=" + ParserVar + ", path=\"" + path + "\", meshes=" + meshes + ", exportFormat=\"" + ".fbx" + "\", oneBlendShape=" + checkBoxMorphFbxOptionOneBlendshape.Checked + ", compatibility=" + ((MorphExportFormat)comboBoxMorphExportFormat.SelectedIndex == MorphExportFormat.Fbx_2006) + ")");
					break;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMorphDeleteKeyframe_Click(object sender, EventArgs e)
		{
			try
			{
				if (treeViewMorphKeyframes.SelectedNode == null)
				{
					Report.ReportLog("No morph clip was selected");
					return;
				}
				if (treeViewMorphKeyframes.SelectedNode.Tag is int)
				{
					int index = (int)treeViewMorphKeyframes.SelectedNode.Tag;
					TreeNode meshNode = treeViewMorphKeyframes.SelectedNode.Parent;
					SkinnedMeshRenderer sMesh = (SkinnedMeshRenderer)meshNode.Tag;
					Gui.Scripting.RunScript(EditorVar + ".DeleteMorphKeyframe(meshId=" + Editor.Meshes.IndexOf(sMesh) + ", morphIndex=" + index + ")");
					InitMorphs();
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeViewMorphKeyframes_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				if (e.Item is TreeNode)
				{
					treeViewMorphKeyframes.DoDragDrop(e.Item, DragDropEffects.Copy);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeViewMorphKeyframes_DragEnter(object sender, DragEventArgs e)
		{
			try
			{
				UpdateDragDropMorphs(sender, e);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeViewMorphKeyframes_DragOver(object sender, DragEventArgs e)
		{
			try
			{
				UpdateDragDropMorphs(sender, e);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeViewMorphKeyframes_DragDrop(object sender, DragEventArgs e)
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
					ProcessDragDropMorphs(node);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void ProcessDragDropMorphs(TreeNode node)
		{
			if (node.Tag is DragSource)
			{
				if ((node.Parent != null) && !node.Checked && node.StateImageIndex != (int)CheckState.Indeterminate)
				{
					return;
				}

				DragSource? dest = null;
				if (treeViewMorphKeyframes.SelectedNode != null)
				{
					dest = treeViewMorphKeyframes.SelectedNode.Tag as DragSource?;
				}

				DragSource source = (DragSource)node.Tag;
				if (source.Type == typeof(WorkspaceMorph))
				{
					dragOptions.ShowPanel(FormAnimatorDragDrop.ShowPanelOption.Morph);
					var srcEditor = (ImportedEditor)Gui.Scripting.Variables[source.Variable];
					dragOptions.textBoxName.Text = srcEditor.Morphs[(int)source.Id].Name;
					if (dragOptions.ShowDialog() == DialogResult.OK)
					{
						// repeating only final choices for repeatability of the script
						WorkspaceMorph wsMorph = srcEditor.Morphs[(int)source.Id];
						foreach (ImportedMorphKeyframe keyframe in wsMorph.KeyframeList)
						{
							if (!wsMorph.isMorphKeyframeEnabled(keyframe))
							{
								Gui.Scripting.RunScript(source.Variable + ".setMorphKeyframeEnabled(morphId=" + (int)source.Id + ", id=" + wsMorph.KeyframeList.IndexOf(keyframe) + ", enabled=false)");
							}
						}
						Gui.Scripting.RunScript(EditorVar + ".ReplaceMorph(morph=" + source.Variable + ".Morphs[" + (int)source.Id + "], destMorphName=\"" + dragOptions.textBoxName.Text + "\", replaceNormals=" + dragOptions.radioButtonReplaceNormalsYes.Checked + ", minSquaredDistance=" + ((float)dragOptions.numericUpDownMinimumDistanceSquared.Value).ToFloatString() + ")");
						Changed = Changed;

						InitMorphs();
						tabControlLists.SelectedTab = tabPageMorph;
					}
				}
			}
			else
			{
				foreach (TreeNode child in node.Nodes)
				{
					ProcessDragDropMorphs(child);
				}
			}
		}

		private void UpdateDragDropMorphs(object sender, DragEventArgs e)
		{
			Point p = treeViewMorphKeyframes.PointToClient(new Point(e.X, e.Y));
			TreeNode target = treeViewMorphKeyframes.GetNodeAt(p);
			if ((target != null) && ((p.X < target.Bounds.Left) || (p.X > target.Bounds.Right) || (p.Y < target.Bounds.Top) || (p.Y > target.Bounds.Bottom)))
			{
				target = null;
			}
			treeViewMorphKeyframes.SelectedNode = target;

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

		private void treeViewMorphKeyframes_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION)
			{
				return;
			}
			buttonMorphDeleteKeyframe_Click(null, null);
		}

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
						tabControlViews.SelectTabWithoutLoosingFocus(tabPageMaterialView);
						LoadMaterial(id);
						CrossRefAddItem(crossRefMaterialMeshes[id], crossRefMaterialMeshesCount, listViewMaterialMesh, listViewMesh);
						CrossRefAddItem(crossRefMaterialTextures[id], crossRefMaterialTexturesCount, listViewMaterialTexture, listViewTexture);
					}
					else
					{
						if (id == loadedMaterial)
						{
							LoadMaterial(-1);
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
				Report.ReportLog(ex.ToString());
				Utility.ReportException(ex);
			}
		}

		private void listViewMaterial_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION || loadedMaterial == -1)
			{
				return;
			}
			buttonMaterialRemove_Click(null, null);
		}

		void textBoxMatName_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedMaterial < 0)
				{
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".SetMaterialName(id=" + loadedMaterial + ", name=\"" + textBoxMatName.Text + "\")");
				Changed = Changed;

				Material mat = Editor.Materials[loadedMaterial];
				RenameListViewItems(Editor.Materials, listViewMaterial, mat, mat.m_Name);
				RenameListViewItems(Editor.Materials, listViewMeshMaterial, mat, mat.m_Name);
				RenameListViewItems(Editor.Materials, listViewTextureMaterial, mat, mat.m_Name);

				InitMaterials();
				SyncWorkspaces(mat.m_Name, typeof(Material), loadedMaterial);
				LoadMaterial(loadedMaterial);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
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
			listView.Sort();
		}

		private void dataGridViewMaterialTextures_CellClick(object sender, DataGridViewCellEventArgs e)
		{
			try
			{
				if ((dataGridViewMaterialTextures.CurrentRow != null) && (dataGridViewMaterialTextures.CurrentCell.ColumnIndex == 0))
				{
					dataGridViewMaterialTextures.BeginEdit(true);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dataGridViewMaterialTextures_DataError(object sender, DataGridViewDataErrorEventArgs e)
		{
			e.ThrowException = false;
		}

		// http://connect.microsoft.com/VisualStudio/feedback/details/151567/datagridviewcomboboxcell-needs-selectedindexchanged-event
		private void dataGridViewMaterialTextures_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
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
							comboBoxCell.SelectionChangeCommitted -= new EventHandler(matTexComboBoxCell_SelectionChangeCommitted);

							//Add the event handler.
							comboBoxCell.SelectionChangeCommitted += new EventHandler(matTexComboBoxCell_SelectionChangeCommitted);
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

		private void matTexComboBoxCell_SelectionChangeCommitted(object sender, EventArgs e)
		{
			try
			{
				ComboBox combo = (ComboBox)sender;
				if (combo.SelectedValue == null)
				{
					return;
				}

				List<Tuple<string, int, Component>> columnTextures = (List<Tuple<string, int, Component>>)ColumnMaterialTexture.DataSource;
				int texIdx = combo.SelectedIndex != -1 ? columnTextures[combo.SelectedIndex].Item2 : -1;
				int rowIdx = dataGridViewMaterialTextures.CurrentCell.RowIndex;
				Material mat = Editor.Materials[loadedMaterial];
				Texture2D matTex = mat.m_SavedProperties.m_TexEnvs[rowIdx].Value.m_Texture.instance;
				if (matTex == null || Editor.Textures.IndexOf(matTex) != texIdx)
				{
					dataGridViewMesh.CommitEdit(DataGridViewDataErrorContexts.Commit);

						if (texIdx == -1 || Editor.Textures[texIdx] != matTex)
						{
							Gui.Scripting.RunScript(EditorVar + ".SetMaterialTexture(id=" + loadedMaterial + ", index=" + rowIdx + ", componentIndex=" + Editor.Parser.file.Components.IndexOf(columnTextures[combo.SelectedIndex].Item3) + ")");
							Changed = Changed;
						}
						else
						{
							return;
						}

						if (texIdx == -1)
						{
							InitMaterials();
							InitTextures();
						}
						RecreateRenderObjects();
						RecreateCrossRefs();
						matTex = mat.m_SavedProperties.m_TexEnvs[rowIdx].Value.m_Texture.instance;
						dataGridViewMaterialTextures.CurrentCell.Value = Editor.Textures.IndexOf(matTex);
						combo.SelectedValue = dataGridViewMaterialTextures.CurrentCell.Value;
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dataGridViewMaterialTextures_KeyDown(object sender, KeyEventArgs e)
		{
			try
			{
				if (e.KeyData == Keys.Escape)
				{
					while (dataGridViewMaterialTextures.SelectedRows.Count > 0)
					{
						dataGridViewMaterialTextures.SelectedRows[0].Selected = false;
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void dataGridViewMaterialColours_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			try
			{
				if (dataGridViewMaterialColours.SelectedCells[0].Value is string)
				{
					dataGridViewMaterialColours.SelectedCells[0].Value = Single.Parse((string)dataGridViewMaterialColours.SelectedCells[0].Value);
					Gui.Scripting.RunScript(EditorVar + ".SetMaterialColour(id=" + loadedMaterial + ", index=" + e.RowIndex + ", colour=" + MatMatrixColorScript(e.RowIndex) + ")");
					Changed = Changed;

					RecreateRenderObjects();
					RecreateCrossRefs();
					LoadMaterial(loadedMaterial);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		string MatMatrixColorScript(int index)
		{
			DataGridViewRow row = dataGridViewMaterialColours.Rows[index];
			return "{ " +
				((float)row.Cells[0].Value).ToFloatString() + ", " +
				((float)row.Cells[1].Value).ToFloatString() + ", " +
				((float)row.Cells[2].Value).ToFloatString() + ", " +
				((float)row.Cells[3].Value).ToFloatString() + " }";
		}

		private void dataGridViewMaterialValues_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			try
			{
				if (dataGridViewMaterialValues.SelectedCells[0].Value is string)
				{
					float value = Single.Parse((string)dataGridViewMaterialValues.SelectedCells[0].Value);
					dataGridViewMaterialValues.SelectedCells[0].Value = value;
					Gui.Scripting.RunScript(EditorVar + ".SetMaterialValue(id=" + loadedMaterial + ", index=" + e.RowIndex + ", value=" + value.ToFloatString() + ")");
					Changed = Changed;

					RecreateRenderObjects();
					RecreateCrossRefs();
					LoadMaterial(loadedMaterial);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMaterialRemove_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedMaterial < 0)
				{
					return;
				}

				List<int> descendingIds = new List<int>(listViewMaterial.SelectedItems.Count);
				List<Component> assets = new List<Component>(listViewMaterial.SelectedItems.Count);
				foreach (ListViewItem item in listViewMaterial.SelectedItems)
				{
					descendingIds.Add((int)item.Tag);
					assets.Add(Editor.Materials[(int)item.Tag]);
				}
				descendingIds.Sort();
				descendingIds.Reverse();
				foreach (int id in descendingIds)
				{
					Gui.Scripting.RunScript(EditorVar + ".RemoveMaterial(id=" + id + ")");
				}
				Changed = Changed;

				RecreateRenderObjects();
				LoadMesh(loadedMesh);
				InitMaterials();
				SyncWorkspaces();
				RecreateCrossRefs();
				LoadMaterial(-1);

				List<DockContent> formUnity3dList;
				if (Gui.Docking.DockContents.TryGetValue(typeof(FormUnity3d), out formUnity3dList))
				{
					foreach (FormUnity3d form in formUnity3dList)
					{
						if (form.Editor.Parser.Cabinet == Editor.Parser.file)
						{
							for (int i = 0; i < form.materialsList.Items.Count; i++)
							{
								if (assets.Contains((Component)form.materialsList.Items[i].Tag))
								{
									form.materialsList.Items.RemoveAt(i);
									i--;
								}
							}
							form.materialsList.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
							if (form.materialsList.Columns.Count > 1)
							{
								form.materialsList.Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
							}
							form.materialsList.Sort();
							((TabPage)form.materialsList.Parent).Text = "Materials [" + form.materialsList.Items.Count + "]";
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonMaterialCopy_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedMaterial < 0)
				{
					return;
				}

				Material newMat = (Material)Gui.Scripting.RunScript(EditorVar + ".CopyMaterial(id=" + loadedMaterial + ")");
				Changed = Changed;

				int oldMatIndex = -1;
				while (listViewMaterial.SelectedItems.Count > 0)
				{
					oldMatIndex = listViewMaterial.SelectedItems[0].Index;
					listViewMaterial.SelectedItems[0].Selected = false;
				}
				InitMaterials();
				RecreateCrossRefs();
				LoadMesh(loadedMesh);
				listViewMaterial.Items[oldMatIndex].Selected = true;

				RefreshFormUnity();
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

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
						tabControlViews.SelectTabWithoutLoosingFocus(tabPageTextureView);
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

		private void listViewTexture_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyData != MASS_DESTRUCTION_KEY_COMBINATION || loadedTexture == -1)
			{
				return;
			}
			buttonTextureRemove_Click(null, null);
		}

		void textBoxTexName_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (loadedTexture < 0)
				{
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".SetTextureName(id=" + loadedTexture + ", name=\"" + textBoxTexName.Text + "\")");
				Changed = Changed;

				Texture2D tex = Editor.Textures[loadedTexture];
				RenameListViewItems(Editor.Textures, listViewTexture, tex, tex.m_Name);
				RenameListViewItems(Editor.Textures, listViewMeshTexture, tex, tex.m_Name);
				RenameListViewItems(Editor.Textures, listViewMaterialTexture, tex, tex.m_Name);

				InitTextures();
				SyncWorkspaces(tex.m_Name, typeof(Texture2D), loadedTexture);
				LoadMaterial(loadedMaterial);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonTextureExport_Click(object sender, EventArgs e)
		{
			try
			{
				foreach (ListViewItem item in listViewTexture.SelectedItems)
				{
					int id = (int)item.Tag;
					DirectoryInfo dirInfo = Directory.GetParent(exportDir);
					Gui.Scripting.RunScript(EditorVar + ".ExportTexture(id=" + id + ", path=\"" + dirInfo.FullName + "\")");
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonTextureRemove_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedTexture < 0)
				{
					return;
				}

				List<int> descendingIds = new List<int>(listViewTexture.SelectedItems.Count);
				foreach (ListViewItem item in listViewTexture.SelectedItems)
				{
					descendingIds.Add((int)item.Tag);
				}
				descendingIds.Sort();
				descendingIds.Reverse();
				foreach (int id in descendingIds)
				{
					Gui.Scripting.RunScript(EditorVar + ".RemoveTexture(id=" + id + ")");
				}
				Changed = Changed;

				RecreateRenderObjects();
				InitTextures();
				SyncWorkspaces();
				RecreateCrossRefs();
				LoadMaterial(loadedMaterial);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonTextureAdd_Click(object sender, EventArgs e)
		{
			try
			{
				if (Gui.ImageControl.Image == null)
				{
					Report.ReportLog("An image hasn't been loaded");
					return;
				}

				List<string> vars = FormImageFiles.GetSelectedImageVariables();
				if (vars == null)
				{
					Report.ReportLog("An image hasn't been selected");
					return;
				}

				foreach (string var in vars)
				{
					Gui.Scripting.RunScript(EditorVar + ".AddTexture(image=" + var + ")");
				}
				Changed = Changed;

				RecreateRenderObjects();
				InitTextures();
				RecreateCrossRefs();
				LoadMaterial(loadedMaterial);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonTextureReplace_Click(object sender, EventArgs e)
		{
			try
			{
				if (loadedTexture < 0)
				{
					return;
				}
				if (Gui.ImageControl.Image == null)
				{
					Report.ReportLog("An image hasn't been loaded");
					return;
				}

				Gui.Scripting.RunScript(EditorVar + ".ReplaceTexture(id=" + loadedTexture + ", image=" + Gui.ImageControl.ImageScriptVariable + ")");
				Changed = Changed;

				RecreateRenderObjects();
				InitTextures();
				RecreateCrossRefs();
				LoadMaterial(loadedMaterial);
				foreach (ListViewItem item in listViewTexture.Items)
				{
					if ((int)item.Tag == loadedTexture)
					{
						item.Selected = true;
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void editTextBoxTexAttributes_AfterEditTextChanged(object sender, EventArgs e)
		{
			try
			{
				if (textureLoading)
				{
					return;
				}
				Gui.Scripting.RunScript(EditorVar + ".SetTextureAttributes(id=" + loadedTexture + GetTextureAttributes() + ")");
				Changed = Changed;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void checkBoxTextureMipMap_CheckedChanged(object sender, EventArgs e)
		{
			try
			{
				if (textureLoading)
				{
					return;
				}
				Gui.Scripting.RunScript(EditorVar + ".SetTextureAttributes(id=" + loadedTexture + GetTextureAttributes() + ")");
				Changed = Changed;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		string GetTextureAttributes()
		{
			return ", dimension=" + editTextBoxTexDimension.Text
				+ ", mipMap=" + checkBoxTextureMipMap.Checked
				+ ", imageCount=" + editTextBoxTexImageCount.Text
				+ ", colorSpace=" + editTextBoxTexColorSpace.Text
				+ ", lightMap=" + editTextBoxTexLightMap.Text
				+ ", filterMode=" + editTextBoxTexFilterMode.Text
				+ ", mipBias=" + Single.Parse(editTextBoxTexMipBias.Text).ToFloatString()
				+ ", aniso=" + editTextBoxTexAniso.Text
				+ ", wrapMode=" + editTextBoxTexWrapMode.Text;
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
					pictureBoxTexture.Size = new Size
					(
						Decimal.ToInt32(pictureBoxTexture.Image.Width * y),
						Decimal.ToInt32(pictureBoxTexture.Image.Height * y)
					);
				}
				else
				{
					pictureBoxTexture.Size = new Size
					(
						Decimal.ToInt32(pictureBoxTexture.Image.Width * x),
						Decimal.ToInt32(pictureBoxTexture.Image.Height * x)
					);
				}
			}
		}
	}
}
