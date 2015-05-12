using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using SlimDX;
using SlimDX.Direct3D9;
using WeifenLuo.WinFormsUI.Docking;

namespace SB3Utility
{
	public partial class FormImage : DockContent, IImageControl
	{
		ImportedTexture image;

		public ImportedTexture Image
		{
			get { return image; }
			set { LoadImage(value); }
		}

		public string ImageScriptVariable { get { return "GUItexture"; } }

		public FormImage()
		{
			try
			{
				InitializeComponent();

				panel1.Resize += new EventHandler(panel1_Resize);
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}

		void panel1_Resize(object sender, EventArgs e)
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

			if ((pictureBox1.Image != null) && (pictureBox1.Image.Width > 0) && (pictureBox1.Image.Height > 0))
			{
				Decimal x = (Decimal)panel1.Width / pictureBox1.Image.Width;
				Decimal y = (Decimal)panel1.Height / pictureBox1.Image.Height;
				if (x > y)
				{
					pictureBox1.Width = Decimal.ToInt32(pictureBox1.Image.Width * y);
					pictureBox1.Height = Decimal.ToInt32(pictureBox1.Image.Height * y);
				}
				else
				{
					pictureBox1.Width = Decimal.ToInt32(pictureBox1.Image.Width * x);
					pictureBox1.Height = Decimal.ToInt32(pictureBox1.Image.Height * x);
				}
			}
		}

		void LoadImage(ImportedTexture tex)
		{
			try
			{
				if (tex == null)
				{
					pictureBox1.Image = null;
					textBoxName.Text = String.Empty;
					textBoxSize.Text = String.Empty;
				}
				else
				{
					textBoxName.Text = tex.Name;

					if (tex.Data.Length > 0x12)
					{
						ImageInformation imageInfo;
						Texture renderTexture = Texture.FromMemory(Gui.Renderer.Device, tex.Data, 0, 0, -1, Usage.None, Format.Unknown, Pool.Managed, Filter.Default, Filter.Default, 0, out imageInfo);
						DataStream stream = Texture.ToStream(renderTexture, ImageFileFormat.Bmp);
						Bitmap bitmap = new Bitmap(stream);
						stream.Dispose();
						string format = renderTexture.GetLevelDescription(0).Format.GetDescription();
						int bpp = (format.Contains("A8") ? 8 : 0)
							+ (format.Contains("R8") ? 8 : 0) + (format.Contains("G8") ? 8 : 0) + (format.Contains("B8") ? 8 : 0);
						renderTexture.Dispose();
						pictureBox1.Image = bitmap;

						ResizeImage();
						if (!this.IsHidden)
						{
							Enabled = false;
							Activate();
							Enabled = true;
						}
						textBoxSize.Text = imageInfo.Width + "x" + imageInfo.Height + (bpp > 0 ? "x" + bpp : String.Empty);
					}
					else
					{
						pictureBox1.Image = null;
						textBoxSize.Text = "0x0";
					}
				}

				image = tex;
			}
			catch (Exception ex)
			{
				Utility.ReportException(ex);
			}
		}
	}
}
