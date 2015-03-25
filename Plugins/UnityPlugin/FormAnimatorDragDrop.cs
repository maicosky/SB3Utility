using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using SB3Utility;

namespace UnityPlugin
{
	public partial class FormAnimatorDragDrop : Form
	{
		public enum ShowPanelOption
		{
			Frame,
			Mesh,
			Morph
		};

		public CopyFrameMethod FrameMethod { get; protected set; }
		public CopyMeshMethod NormalsMethod { get; protected set; }
		public CopyMeshMethod BonesMethod { get; protected set; }

		private AnimatorEditor editor;

		private SizeF startSize;

		public FormAnimatorDragDrop(AnimatorEditor destEditor)
		{
			InitializeComponent();
			startSize = new SizeF(Width, Height);
			this.SaveDesignSizes();
			editor = destEditor;

			numericFrameId.Maximum = editor.Frames.Count - 1;
			numericMeshId.Maximum = editor.Frames.Count - 1;
		}

		public void ShowPanel(ShowPanelOption panelOption)
		{
			switch (panelOption)
			{
			case ShowPanelOption.Frame:
				panelFrame.BringToFront();
				break;
			case ShowPanelOption.Mesh:
				panelMesh.BringToFront();
				break;
			case ShowPanelOption.Morph:
				panelMorph.BringToFront();
				break;
			}
		}

		private void radioButtonFrameMerge_CheckedChanged(object sender, EventArgs e)
		{
			FrameMethod = CopyFrameMethod.MergeFrame;
		}

		private void radioButtonFrameAdd_CheckedChanged(object sender, EventArgs e)
		{
			FrameMethod = CopyFrameMethod.AddFrame;
		}

		private void radioButtonFrameReplace_CheckedChanged(object sender, EventArgs e)
		{
			FrameMethod = CopyFrameMethod.ReplaceFrame;
		}

		private void numericFrameId_ValueChanged(object sender, EventArgs e)
		{
			textBoxFrameName.Text = (numericFrameId.Value < 0) ? String.Empty : editor.Frames[Decimal.ToInt32(numericFrameId.Value)].m_GameObject.instance.m_Name;
		}

		private void numericMeshId_ValueChanged(object sender, EventArgs e)
		{
			textBoxMeshName.Text = (numericMeshId.Value < 0) ? String.Empty : editor.Frames[Decimal.ToInt32(numericMeshId.Value)].m_GameObject.instance.m_Name;
		}

		private void radioButtonNormalsReplace_CheckedChanged(object sender, EventArgs e)
		{
			NormalsMethod = CopyMeshMethod.Replace;
		}

		private void radioButtonNormalsCopyNear_CheckedChanged(object sender, EventArgs e)
		{
			NormalsMethod = CopyMeshMethod.CopyNear;
		}

		private void radioButtonNormalsCopyOrder_CheckedChanged(object sender, EventArgs e)
		{
			NormalsMethod = CopyMeshMethod.CopyOrder;
		}

		private void radioButtonBonesReplace_CheckedChanged(object sender, EventArgs e)
		{
			BonesMethod = CopyMeshMethod.Replace;
		}

		private void radioButtonBonesCopyNear_CheckedChanged(object sender, EventArgs e)
		{
			BonesMethod = CopyMeshMethod.CopyNear;
		}

		private void radioButtonBonesCopyOrder_CheckedChanged(object sender, EventArgs e)
		{
			BonesMethod = CopyMeshMethod.CopyOrder;
		}

		private void checkBoxOkContinue_Click(object sender, EventArgs e)
		{
			if (checkBoxOkContinue.Checked)
			{
				this.DialogResult = DialogResult.OK;
			}
		}

		private void FormAnimatorDragDrop_Shown(object sender, EventArgs e)
		{
			Size dialogSize = (Size)Gui.Config["DialogXXDragDropSize"];
			if (dialogSize.Width != 0 && dialogSize.Height != 0)
			{
				Width = dialogSize.Width;
				Height = dialogSize.Height;
				this.ResizeControls(startSize);
			}
			else
			{
				Width = (int)startSize.Width;
				Height = (int)startSize.Height;
				this.ResetControls();
			}
		}

		private void FormAnimatorDragDrop_Resize(object sender, EventArgs e)
		{
			this.ResizeControls(startSize);
		}

		private void FormAnimatorDragDrop_VisibleChanged(object sender, EventArgs e)
		{
			if (!Visible)
			{
				if (Width < (int)startSize.Width || Height < (int)startSize.Height)
				{
					Gui.Config["DialogXXDragDropSize"] = new Size(0, 0);
				}
				else
				{
					Gui.Config["DialogXXDragDropSize"] = this.Size;
				}
			}
		}
	}
}
