using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using System.IO;
using SlimDX;

namespace SB3Utility
{
	[Plugin]
	[PluginTool("&Workspace", "Ctrl+W")]
	public partial class FormWorkspace : DockContent
	{
		public FormWorkspace()
		{
			try
			{
				InitializeComponent();

				Gui.Docking.ShowDockContent(this, Gui.Docking.DockFiles, ContentCategory.Others);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		public FormWorkspace(string path, IImported importer, string editorVar, ImportedEditor editor)
		{
			try
			{
				InitializeComponent();

				InitWorkspace(path, importer, editorVar, editor);

				Gui.Docking.ShowDockContent(this, Gui.Docking.DockFiles, ContentCategory.Others);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void InitWorkspace(string path, IImported importer, string editorVar, ImportedEditor editor)
		{
			this.Text = Path.GetFileName(path);
			this.ToolTipText = path;

			if (editor.Frames != null)
			{
				TreeNode root = new TreeNode(typeof(ImportedFrame).Name);
				root.Checked = true;
				this.treeView.AddChild(root);

				for (int i = 0; i < importer.FrameList.Count; i++)
				{
					var frame = importer.FrameList[i];
					TreeNode node = new TreeNode(frame.Name);
					node.Checked = true;
					node.Tag = new DragSource(editorVar, typeof(ImportedFrame), editor.Frames.IndexOf(frame));
					this.treeView.AddChild(root, node);

					foreach (var child in frame)
					{
						BuildTree(editorVar, child, node, editor);
					}
				}
			}

			AddList(editor.Meshes, typeof(ImportedMesh).Name, editorVar);
			AddList(importer.MaterialList, typeof(ImportedMaterial).Name, editorVar);
			AddList(importer.TextureList, typeof(ImportedTexture).Name, editorVar);
			AddList(editor.Morphs, typeof(ImportedMorph).Name, editorVar);
			AddList(editor.Animations, typeof(ImportedAnimation).Name, editorVar);

			foreach (TreeNode root in this.treeView.Nodes)
			{
				root.Expand();
			}
			if (this.treeView.Nodes.Count > 0)
			{
				this.treeView.Nodes[0].EnsureVisible();
			}

			this.treeView.AfterCheck += treeView_AfterCheck;
		}

		private void AddList<T>(List<T> list, string rootName, string editorVar)
		{
			if ((list != null) && (list.Count > 0))
			{
				TreeNode root = new TreeNode(rootName);
				root.Checked = true;
				this.treeView.AddChild(root);

				for (int i = 0; i < list.Count; i++)
				{
					dynamic item = list[i];
					TreeNode node = new TreeNode(item is WorkspaceAnimation
						? ((WorkspaceAnimation)item).importedAnimation is ImportedKeyframedAnimation
							? "Animation" + i : "Animation(Reduced Keys)" + i
						: item.Name);
					node.Checked = true;
					node.Tag = new DragSource(editorVar, typeof(T), i);
					this.treeView.AddChild(root, node);
					if (item is WorkspaceMesh)
					{
						WorkspaceMesh mesh = item;
						for (int j = 0; j < mesh.SubmeshList.Count; j++)
						{
							ImportedSubmesh submesh = mesh.SubmeshList[j];
							TreeNode submeshNode = new TreeNode();
							submeshNode.Checked = mesh.isSubmeshEnabled(submesh);
							submeshNode.Tag = submesh;
							submeshNode.ContextMenuStrip = this.contextMenuStripSubmesh;
							this.treeView.AddChild(node, submeshNode);
							UpdateSubmeshNode(submeshNode);
						}
					}
					else if (item is WorkspaceMorph)
					{
						WorkspaceMorph morph = item;
						for (int j = 0; j < morph.KeyframeList.Count; j++)
						{
							ImportedMorphKeyframe keyframe = morph.KeyframeList[j];
							TreeNode keyframeNode = new TreeNode();
							keyframeNode.Checked = morph.isMorphKeyframeEnabled(keyframe);
							keyframeNode.Tag = keyframe;
							keyframeNode.ContextMenuStrip = this.contextMenuStripMorphKeyframe;
							this.treeView.AddChild(node, keyframeNode);
							UpdateMorphKeyframeNode(keyframeNode);
						}
					}
					else if (item is WorkspaceAnimation)
					{
						WorkspaceAnimation animation = item;
						if (animation.importedAnimation is ImportedKeyframedAnimation)
						{
							List<ImportedAnimationKeyframedTrack> trackList = ((ImportedKeyframedAnimation)animation.importedAnimation).TrackList;
							for (int j = 0; j < trackList.Count; j++)
							{
								ImportedAnimationKeyframedTrack track = trackList[j];
								TreeNode trackNode = new TreeNode();
								trackNode.Checked = animation.isTrackEnabled(track);
								trackNode.Tag = track;
								int numKeyframes = 0;
								foreach (ImportedAnimationKeyframe keyframe in track.Keyframes)
								{
									if (keyframe != null)
										numKeyframes++;
								}
								trackNode.Text = "Track: " + track.Name + ", Keyframes: " + numKeyframes;
								this.treeView.AddChild(node, trackNode);
							}
						}
						else if (animation.importedAnimation is ImportedSampledAnimation)
						{
							List<ImportedAnimationSampledTrack> trackList = ((ImportedSampledAnimation)animation.importedAnimation).TrackList;
							for (int j = 0; j < trackList.Count; j++)
							{
								ImportedAnimationSampledTrack track = trackList[j];
								TreeNode trackNode = new TreeNode();
								trackNode.Checked = animation.isTrackEnabled(track);
								trackNode.Tag = track;
								int numScalings = 0;
								for (int k = 0; k < track.Scalings.Length; k++)
								{
									if (track.Scalings[k] != null)
										numScalings++;
								}
								int numRotations = 0;
								for (int k = 0; k < track.Rotations.Length; k++)
								{
									if (track.Rotations[k] != null)
										numRotations++;
								}
								int numTranslations = 0;
								for (int k = 0; k < track.Translations.Length; k++)
								{
									if (track.Translations[k] != null)
										numTranslations++;
								}
								trackNode.Text = "Track: " + track.Name + ", Length: " + track.Scalings.Length + ", Scalings: " + numScalings + ", Rotations: " + numRotations + ", Translations: " + numTranslations;
								this.treeView.AddChild(node, trackNode);
							}
						}
					}
				}
			}
		}

		private void UpdateSubmeshNode(TreeNode node)
		{
			ImportedSubmesh submesh = (ImportedSubmesh)node.Tag;
			TreeNode meshNode = node.Parent;
			DragSource dragSrc = (DragSource)meshNode.Tag;
			var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
			bool replaceSubmesh = srcEditor.Meshes[(int)dragSrc.Id].isSubmeshReplacingOriginal(submesh);
			node.Text = "Sub: V " + submesh.VertexList.Count + ", F " + submesh.FaceList.Count + ", Base: " + submesh.Index + ", Replace: " + replaceSubmesh + ", Mat: " + submesh.Material + ", World:" + submesh.WorldCoords;
		}

		private void UpdateMorphKeyframeNode(TreeNode node)
		{
			ImportedMorphKeyframe keyframe = (ImportedMorphKeyframe)node.Tag;
			TreeNode morphNode = node.Parent;
			DragSource dragSrc = (DragSource)morphNode.Tag;
			var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
			string newName = srcEditor.Morphs[(int)dragSrc.Id].getMorphKeyframeNewName(keyframe);
			node.Text = "Morph: " + keyframe.Name + (newName != String.Empty ? ", Rename to: " + newName : null);
		}

		public static void UpdateAnimationNode(TreeNode node, WorkspaceAnimation animation)
		{
			int id = (int)((DragSource)node.Tag).Id;
			if (animation.importedAnimation is ImportedKeyframedAnimation)
			{
				node.Text = "Animation" + id;
				List<ImportedAnimationKeyframedTrack> trackList = ((ImportedKeyframedAnimation)animation.importedAnimation).TrackList;
				for (int i = 0; i < trackList.Count; i++)
				{
					ImportedAnimationKeyframedTrack track = trackList[i];
					TreeNode trackNode = node.Nodes[i];
					trackNode.Tag = track;
					int numKeyframes = 0;
					foreach (ImportedAnimationKeyframe keyframe in track.Keyframes)
					{
						if (keyframe != null)
							numKeyframes++;
					}
					trackNode.Text = "Track: " + track.Name + ", Keyframes: " + numKeyframes;
				}
			}
			else if (animation.importedAnimation is ImportedSampledAnimation)
			{
				node.Text = "Animation(Reduced Keys)" + id;
				List<ImportedAnimationSampledTrack> trackList = ((ImportedSampledAnimation)animation.importedAnimation).TrackList;
				for (int i = 0; i < trackList.Count; i++)
				{
					ImportedAnimationSampledTrack track = trackList[i];
					TreeNode trackNode = node.Nodes[i];
					trackNode.Tag = track;
					int numScalings = 0;
					for (int k = 0; k < track.Scalings.Length; k++)
					{
						if (track.Scalings[k] != null)
							numScalings++;
					}
					int numRotations = 0;
					for (int k = 0; k < track.Rotations.Length; k++)
					{
						if (track.Rotations[k] != null)
							numRotations++;
					}
					int numTranslations = 0;
					for (int k = 0; k < track.Translations.Length; k++)
					{
						if (track.Translations[k] != null)
							numTranslations++;
					}
					trackNode.Text = "Track: " + track.Name + ", Length: " + track.Scalings.Length + ", Scalings: " + numScalings + ", Rotations: " + numRotations + ", Translations: " + numTranslations;
				}
			}
		}

		private void BuildTree(string editorVar, ImportedFrame frame, TreeNode parent, ImportedEditor editor)
		{
			TreeNode node = new TreeNode(frame.Name);
			node.Checked = true;
			node.Tag = new DragSource(editorVar, typeof(ImportedFrame), editor.Frames.IndexOf(frame));
			this.treeView.AddChild(parent, node);

			foreach (var child in frame)
			{
				BuildTree(editorVar, child, node, editor);
			}
		}

		private void treeView_AfterCheck(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Tag is ImportedSubmesh)
			{
				TreeNode submeshNode = e.Node;
				ImportedSubmesh submesh = (ImportedSubmesh)submeshNode.Tag;
				TreeNode meshNode = submeshNode.Parent;
				DragSource dragSrc = (DragSource)meshNode.Tag;
				var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
				srcEditor.Meshes[(int)dragSrc.Id].setSubmeshEnabled(submesh, submeshNode.Checked);
			}
			else if (e.Node.Tag is ImportedMorphKeyframe)
			{
				TreeNode keyframeNode = e.Node;
				ImportedMorphKeyframe keyframe = (ImportedMorphKeyframe)keyframeNode.Tag;
				TreeNode morphNode = keyframeNode.Parent;
				DragSource dragSrc = (DragSource)morphNode.Tag;
				var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
				srcEditor.Morphs[(int)dragSrc.Id].setMorphKeyframeEnabled(keyframe, keyframeNode.Checked);
			}
			else if (e.Node.Tag is ImportedAnimationKeyframedTrack)
			{
				TreeNode trackNode = e.Node;
				ImportedAnimationKeyframedTrack track = (ImportedAnimationKeyframedTrack)trackNode.Tag;
				TreeNode animationNode = trackNode.Parent;
				DragSource dragSrc = (DragSource)animationNode.Tag;
				var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
				srcEditor.Animations[(int)dragSrc.Id].setTrackEnabled(track, trackNode.Checked);
			}
			else if (e.Node.Tag is ImportedAnimationSampledTrack)
			{
				TreeNode trackNode = e.Node;
				ImportedAnimationSampledTrack track = (ImportedAnimationSampledTrack)trackNode.Tag;
				TreeNode animationNode = trackNode.Parent;
				DragSource dragSrc = (DragSource)animationNode.Tag;
				var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
				srcEditor.Animations[(int)dragSrc.Id].setTrackEnabled(track, trackNode.Checked);
			}
		}

		private void treeView_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				if (e.Item is TreeNode)
				{
					treeView.DoDragDrop(e.Item, DragDropEffects.Copy);
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeView_DragEnter(object sender, DragEventArgs e)
		{
			try
			{
				UpdateDragStatus(sender, e);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void treeView_DragOver(object sender, DragEventArgs e)
		{
			try
			{
				UpdateDragStatus(sender, e);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void UpdateDragStatus(object sender, DragEventArgs e)
		{
			Point p = treeView.PointToClient(new Point(e.X, e.Y));
			TreeNode target = treeView.GetNodeAt(p);
			if ((target != null) && ((p.X < target.Bounds.Left) || (p.X > target.Bounds.Right) || (p.Y < target.Bounds.Top) || (p.Y > target.Bounds.Bottom)))
			{
				target = null;
			}
			treeView.SelectedNode = target;

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

		private void treeView_DragDrop(object sender, DragEventArgs e)
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
					if (node.TreeView != treeView)
					{
						DragSource? source = node.Tag as DragSource?;
						if (source != null)
						{
							TreeNode clone = (TreeNode)node.Clone();
							clone.Checked = true;

							TreeNode type = null;
							foreach (TreeNode root in treeView.Nodes)
							{
								if (root.Text == source.Value.Type.Name)
								{
									type = root;
									break;
								}
							}

							if (type == null)
							{
								type = new TreeNode(source.Value.Type.Name);
								type.Checked = true;
								treeView.AddChild(type);
							}

							treeView.AddChild(type, clone);
						}
						else
						{
							foreach (TreeNode child in node.Nodes)
							{
								e.Data.SetData(child);
								treeView_DragDrop(sender, e);
							}
						}

						foreach (TreeNode root in treeView.Nodes)
						{
							root.Expand();
						}
						if (treeView.Nodes.Count > 0)
						{
							treeView.Nodes[0].EnsureVisible();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		private void buttonRemove_Click(object sender, EventArgs e)
		{
			if (treeView.SelectedNode != null)
			{
				TreeNode parent = treeView.SelectedNode.Parent;
				if (parent == null)
				{
					treeView.RemoveChild(treeView.SelectedNode);
				}
				else if (parent.Parent == null)
				{
					if (parent.Nodes.Count <= 1)
					{
						treeView.RemoveChild(parent);
					}
					else
					{
						treeView.RemoveChild(treeView.SelectedNode);
					}
				}
			}
		}

		private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			treeView.BeginUpdate();
			treeView.ExpandAll();
			treeView.EndUpdate();
		}

		private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			treeView.BeginUpdate();
			treeView.CollapseAll();
			treeView.EndUpdate();
		}

		private void contextMenuStripSubmesh_Opening(object sender, CancelEventArgs e)
		{
			Point contextLoc = new Point(contextMenuStripSubmesh.Left, contextMenuStripSubmesh.Top);
			Point relativeLoc = treeView.PointToClient(contextLoc);
			TreeNode submeshNode = treeView.GetNodeAt(relativeLoc);
			ImportedSubmesh submesh = (ImportedSubmesh)submeshNode.Tag;
			toolStripTextBoxTargetPosition.Text = submesh.Index.ToString();
			TreeNode meshNode = submeshNode.Parent;
			DragSource dragSrc = (DragSource)meshNode.Tag;
			var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
			bool replaceSubmesh = srcEditor.Meshes[(int)dragSrc.Id].isSubmeshReplacingOriginal(submesh);
			replaceToolStripMenuItem.Checked = replaceSubmesh;
			toolStripTextBoxMaterialName.Text = submesh.Material;
			worldCoordinatesToolStripMenuItem.Checked = submesh.WorldCoords;
		}

		private void toolStripTextBoxTargetPosition_AfterEditTextChanged(object sender, EventArgs e)
		{
			Point contextLoc = new Point(contextMenuStripSubmesh.Left, contextMenuStripSubmesh.Top);
			Point relativeLoc = treeView.PointToClient(contextLoc);
			TreeNode submeshNode = treeView.GetNodeAt(relativeLoc);
			ImportedSubmesh submesh = (ImportedSubmesh)submeshNode.Tag;
			int newIndex;
			if (Int32.TryParse(toolStripTextBoxTargetPosition.Text, out newIndex))
			{
				submesh.Index = newIndex;
				UpdateSubmeshNode(submeshNode);
			}
		}

		private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Point contextLoc = new Point(contextMenuStripSubmesh.Left, contextMenuStripSubmesh.Top);
			Point relativeLoc = treeView.PointToClient(contextLoc);
			TreeNode submeshNode = treeView.GetNodeAt(relativeLoc);
			ImportedSubmesh submesh = (ImportedSubmesh)submeshNode.Tag;
			TreeNode meshNode = submeshNode.Parent;
			DragSource dragSrc = (DragSource)meshNode.Tag;
			var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
			bool replaceSubmesh = srcEditor.Meshes[(int)dragSrc.Id].isSubmeshReplacingOriginal(submesh);
			replaceSubmesh ^= true;
			srcEditor.Meshes[(int)dragSrc.Id].setSubmeshReplacingOriginal(submesh, replaceSubmesh);
			replaceToolStripMenuItem.Checked = replaceSubmesh;
			UpdateSubmeshNode(submeshNode);
		}

		private void toolStripTextBoxMaterialName_AfterEditTextChanged(object sender, EventArgs e)
		{
			Point contextLoc = new Point(contextMenuStripSubmesh.Left, contextMenuStripSubmesh.Top);
			Point relativeLoc = treeView.PointToClient(contextLoc);
			TreeNode submeshNode = treeView.GetNodeAt(relativeLoc);
			ImportedSubmesh submesh = (ImportedSubmesh)submeshNode.Tag;
			submesh.Material = toolStripTextBoxMaterialName.Text;
			UpdateSubmeshNode(submeshNode);
		}

		private void worldCoordinatesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Point contextLoc = new Point(contextMenuStripSubmesh.Left, contextMenuStripSubmesh.Top);
			Point relativeLoc = treeView.PointToClient(contextLoc);
			TreeNode submeshNode = treeView.GetNodeAt(relativeLoc);
			ImportedSubmesh submesh = (ImportedSubmesh)submeshNode.Tag;
			submesh.WorldCoords ^= true;
			worldCoordinatesToolStripMenuItem.Checked = submesh.WorldCoords;
			UpdateSubmeshNode(submeshNode);
		}

		private void contextMenuStripMorphKeyframe_Opening(object sender, CancelEventArgs e)
		{
			Point contextLoc = new Point(contextMenuStripMorphKeyframe.Left, contextMenuStripMorphKeyframe.Top);
			Point relativeLoc = treeView.PointToClient(contextLoc);
			TreeNode morphKeyframeNode = treeView.GetNodeAt(relativeLoc);
			ImportedMorphKeyframe keyframe = (ImportedMorphKeyframe)morphKeyframeNode.Tag;
			TreeNode morphNode = morphKeyframeNode.Parent;
			DragSource dragSrc = (DragSource)morphNode.Tag;
			var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
			string newName = srcEditor.Morphs[(int)dragSrc.Id].getMorphKeyframeNewName(keyframe);
			toolStripEditTextBoxNewMorphKeyframeName.Text = newName;
		}

		private void toolStripEditTextBoxNewMorphKeyframeName_AfterEditTextChanged(object sender, EventArgs e)
		{
			Point contextLoc = new Point(contextMenuStripMorphKeyframe.Left, contextMenuStripMorphKeyframe.Top);
			Point relativeLoc = treeView.PointToClient(contextLoc);
			TreeNode morphKeyframeNode = treeView.GetNodeAt(relativeLoc);
			ImportedMorphKeyframe keyframe = (ImportedMorphKeyframe)morphKeyframeNode.Tag;
			TreeNode morphNode = morphKeyframeNode.Parent;
			DragSource dragSrc = (DragSource)morphNode.Tag;
			var srcEditor = (ImportedEditor)Gui.Scripting.Variables[dragSrc.Variable];
			string newName = toolStripEditTextBoxNewMorphKeyframeName.Text;
			srcEditor.Morphs[(int)dragSrc.Id].setMorphKeyframeNewName(keyframe, newName != String.Empty ? newName : null);
			UpdateMorphKeyframeNode(morphKeyframeNode);
		}

		private void treeView_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			if (!(e.Node.Tag is DragSource) || ((DragSource)e.Node.Tag).Type != typeof(WorkspaceMesh))
			{
				e.CancelEdit = true;
			}
		}

		private void treeView_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			DragSource source = (DragSource)e.Node.Tag;
			if (source.Type == typeof(WorkspaceMesh))
			{
				var srcEditor = (ImportedEditor)Gui.Scripting.Variables[source.Variable];
				ImportedMesh iMesh = srcEditor.Imported.MeshList[(int)source.Id];
				WorkspaceMesh wsMesh = srcEditor.Meshes[(int)source.Id];
				iMesh.Name = wsMesh.Name = e.Label;
			}
		}
	}
}
