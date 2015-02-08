using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SB3Utility
{
	public partial class FormNormalsAndTangents : Form
	{
		private SizeF startSize;

		public FormNormalsAndTangents()
		{
			InitializeComponent();
			startSize = new SizeF(Width, Height);
			this.SaveDesignSizes();
		}

		private void FormNormalsAndTagents_Shown(object sender, EventArgs e)
		{
			Size dialogSize = (Size)Gui.Config["DialogXXNormalsSize"];
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

		private void FormNormalsAndTagents_Resize(object sender, EventArgs e)
		{
			this.ResizeControls(startSize);
		}

		private void FormNormalsAndTagents_VisibleChanged(object sender, EventArgs e)
		{
			if (!Visible)
			{
				if (Width < (int)startSize.Width || Height < (int)startSize.Height)
				{
					Gui.Config["DialogXXNormalsSize"] = new Size(0, 0);
				}
				else
				{
					Gui.Config["DialogXXNormalsSize"] = this.Size;
				}
			}
		}
	}
}
