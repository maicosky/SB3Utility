namespace AiDroidPlugin
{
	partial class FormREA
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			CustomDispose();

			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormREA));
			this.label1 = new System.Windows.Forms.Label();
			this.listViewAnimationTrack = new System.Windows.Forms.ListView();
			this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.buttonAnimationPlayPause = new System.Windows.Forms.Button();
			this.imageList1 = new System.Windows.Forms.ImageList(this.components);
			this.trackBarAnimationKeyframe = new System.Windows.Forms.TrackBar();
			this.numericAnimationSpeed = new System.Windows.Forms.NumericUpDown();
			this.numericAnimationKeyframe = new System.Windows.Forms.NumericUpDown();
			this.labelSkeletalRender = new System.Windows.Forms.Label();
			this.label30 = new System.Windows.Forms.Label();
			this.textBoxANICunk1 = new SB3Utility.EditTextBox();
			this.textBoxANICunk2 = new SB3Utility.EditTextBox();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
			this.buttonAnimationTrackRemove = new System.Windows.Forms.Button();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.trackBarAnimationKeyframe)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numericAnimationSpeed)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numericAnimationKeyframe)).BeginInit();
			this.groupBox2.SuspendLayout();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(0, 0);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(89, 13);
			this.label1.TabIndex = 182;
			this.label1.Text = "Animation Tracks";
			// 
			// listViewAnimationTrack
			// 
			this.listViewAnimationTrack.AllowDrop = true;
			this.listViewAnimationTrack.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listViewAnimationTrack.AutoArrange = false;
			this.listViewAnimationTrack.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4,
            this.columnHeader1});
			this.listViewAnimationTrack.FullRowSelect = true;
			this.listViewAnimationTrack.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
			this.listViewAnimationTrack.HideSelection = false;
			this.listViewAnimationTrack.LabelEdit = true;
			this.listViewAnimationTrack.LabelWrap = false;
			this.listViewAnimationTrack.Location = new System.Drawing.Point(3, 16);
			this.listViewAnimationTrack.Name = "listViewAnimationTrack";
			this.listViewAnimationTrack.ShowGroups = false;
			this.listViewAnimationTrack.ShowItemToolTips = true;
			this.listViewAnimationTrack.Size = new System.Drawing.Size(276, 125);
			this.listViewAnimationTrack.Sorting = System.Windows.Forms.SortOrder.Ascending;
			this.listViewAnimationTrack.TabIndex = 30;
			this.listViewAnimationTrack.UseCompatibleStateImageBehavior = false;
			this.listViewAnimationTrack.View = System.Windows.Forms.View.Details;
			this.listViewAnimationTrack.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listViewAnimationTrack_AfterLabelEdit);
			this.listViewAnimationTrack.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.listViewAnimationTrack_ItemDrag);
			this.listViewAnimationTrack.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listViewAnimationTrack_ItemSelectionChanged);
			this.listViewAnimationTrack.DragDrop += new System.Windows.Forms.DragEventHandler(this.listViewAnimationTrack_DragDrop);
			this.listViewAnimationTrack.DragEnter += new System.Windows.Forms.DragEventHandler(this.listViewAnimationTrack_DragEnter);
			this.listViewAnimationTrack.DragOver += new System.Windows.Forms.DragEventHandler(this.listViewAnimationTrack_DragOver);
			// 
			// columnHeader3
			// 
			this.columnHeader3.Text = "Track Name";
			this.columnHeader3.Width = 134;
			// 
			// columnHeader4
			// 
			this.columnHeader4.Text = "Keyframes";
			this.columnHeader4.Width = 64;
			// 
			// columnHeader1
			// 
			this.columnHeader1.Text = "Length";
			this.columnHeader1.Width = 66;
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox1.Controls.Add(this.buttonAnimationPlayPause);
			this.groupBox1.Controls.Add(this.trackBarAnimationKeyframe);
			this.groupBox1.Controls.Add(this.numericAnimationSpeed);
			this.groupBox1.Controls.Add(this.numericAnimationKeyframe);
			this.groupBox1.Controls.Add(this.labelSkeletalRender);
			this.groupBox1.Controls.Add(this.label30);
			this.groupBox1.Location = new System.Drawing.Point(3, 147);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(413, 65);
			this.groupBox1.TabIndex = 80;
			this.groupBox1.TabStop = false;
			// 
			// buttonAnimationPlayPause
			// 
			this.buttonAnimationPlayPause.ImageAlign = System.Drawing.ContentAlignment.BottomCenter;
			this.buttonAnimationPlayPause.ImageIndex = 0;
			this.buttonAnimationPlayPause.ImageList = this.imageList1;
			this.buttonAnimationPlayPause.Location = new System.Drawing.Point(7, 12);
			this.buttonAnimationPlayPause.Name = "buttonAnimationPlayPause";
			this.buttonAnimationPlayPause.Size = new System.Drawing.Size(20, 19);
			this.buttonAnimationPlayPause.TabIndex = 82;
			this.buttonAnimationPlayPause.UseVisualStyleBackColor = true;
			this.buttonAnimationPlayPause.Click += new System.EventHandler(this.buttonAnimationPlayPause_Click);
			// 
			// imageList1
			// 
			this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
			this.imageList1.TransparentColor = System.Drawing.Color.White;
			this.imageList1.Images.SetKeyName(0, "play.bmp");
			this.imageList1.Images.SetKeyName(1, "pause.bmp");
			// 
			// trackBarAnimationKeyframe
			// 
			this.trackBarAnimationKeyframe.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.trackBarAnimationKeyframe.AutoSize = false;
			this.trackBarAnimationKeyframe.Location = new System.Drawing.Point(33, 14);
			this.trackBarAnimationKeyframe.Name = "trackBarAnimationKeyframe";
			this.trackBarAnimationKeyframe.Size = new System.Drawing.Size(279, 18);
			this.trackBarAnimationKeyframe.TabIndex = 84;
			this.trackBarAnimationKeyframe.TickStyle = System.Windows.Forms.TickStyle.None;
			this.trackBarAnimationKeyframe.ValueChanged += new System.EventHandler(this.trackBarAnimationKeyframe_ValueChanged);
			// 
			// numericAnimationSpeed
			// 
			this.numericAnimationSpeed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numericAnimationSpeed.DecimalPlaces = 1;
			this.numericAnimationSpeed.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.numericAnimationSpeed.Location = new System.Drawing.Point(316, 39);
			this.numericAnimationSpeed.Name = "numericAnimationSpeed";
			this.numericAnimationSpeed.Size = new System.Drawing.Size(55, 20);
			this.numericAnimationSpeed.TabIndex = 88;
			this.numericAnimationSpeed.Value = new decimal(new int[] {
            15,
            0,
            0,
            0});
			this.numericAnimationSpeed.ValueChanged += new System.EventHandler(this.numericAnimationSpeed_ValueChanged);
			// 
			// numericAnimationKeyframe
			// 
			this.numericAnimationKeyframe.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numericAnimationKeyframe.Location = new System.Drawing.Point(316, 13);
			this.numericAnimationKeyframe.Name = "numericAnimationKeyframe";
			this.numericAnimationKeyframe.Size = new System.Drawing.Size(55, 20);
			this.numericAnimationKeyframe.TabIndex = 86;
			this.numericAnimationKeyframe.ValueChanged += new System.EventHandler(this.numericAnimationKeyframe_ValueChanged);
			// 
			// labelSkeletalRender
			// 
			this.labelSkeletalRender.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelSkeletalRender.AutoSize = true;
			this.labelSkeletalRender.Location = new System.Drawing.Point(372, 17);
			this.labelSkeletalRender.Name = "labelSkeletalRender";
			this.labelSkeletalRender.Size = new System.Drawing.Size(21, 13);
			this.labelSkeletalRender.TabIndex = 148;
			this.labelSkeletalRender.Text = "/ 0";
			// 
			// label30
			// 
			this.label30.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label30.AutoSize = true;
			this.label30.Location = new System.Drawing.Point(371, 43);
			this.label30.Name = "label30";
			this.label30.Size = new System.Drawing.Size(38, 13);
			this.label30.TabIndex = 146;
			this.label30.Text = "Speed";
			// 
			// textBoxANICunk1
			// 
			this.textBoxANICunk1.Location = new System.Drawing.Point(6, 19);
			this.textBoxANICunk1.Name = "textBoxANICunk1";
			this.textBoxANICunk1.Size = new System.Drawing.Size(33, 20);
			this.textBoxANICunk1.TabIndex = 52;
			this.toolTip1.SetToolTip(this.textBoxANICunk1, "Integer");
			// 
			// textBoxANICunk2
			// 
			this.textBoxANICunk2.Location = new System.Drawing.Point(6, 45);
			this.textBoxANICunk2.Name = "textBoxANICunk2";
			this.textBoxANICunk2.Size = new System.Drawing.Size(33, 20);
			this.textBoxANICunk2.TabIndex = 54;
			this.toolTip1.SetToolTip(this.textBoxANICunk2, "Float");
			// 
			// groupBox2
			// 
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox2.Controls.Add(this.label3);
			this.groupBox2.Controls.Add(this.label2);
			this.groupBox2.Controls.Add(this.textBoxANICunk1);
			this.groupBox2.Controls.Add(this.textBoxANICunk2);
			this.groupBox2.Location = new System.Drawing.Point(285, 0);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(131, 71);
			this.groupBox2.TabIndex = 50;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Unknowns";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(45, 48);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(27, 13);
			this.label3.TabIndex = 56;
			this.label3.Text = "FPS";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(43, 22);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(79, 13);
			this.label2.TabIndex = 55;
			this.label2.Text = "Max Keyframes";
			// 
			// buttonAnimationTrackRemove
			// 
			this.buttonAnimationTrackRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonAnimationTrackRemove.Location = new System.Drawing.Point(286, 90);
			this.buttonAnimationTrackRemove.Name = "buttonAnimationTrackRemove";
			this.buttonAnimationTrackRemove.Size = new System.Drawing.Size(96, 23);
			this.buttonAnimationTrackRemove.TabIndex = 183;
			this.buttonAnimationTrackRemove.Text = "Remove Tracks";
			this.buttonAnimationTrackRemove.UseVisualStyleBackColor = true;
			this.buttonAnimationTrackRemove.Click += new System.EventHandler(this.buttonAnimationTrackRemove_Click);
			// 
			// FormREA
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(419, 217);
			this.Controls.Add(this.buttonAnimationTrackRemove);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.listViewAnimationTrack);
			this.Controls.Add(this.groupBox1);
			this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.Name = "FormREA";
			this.Text = "FormREA";
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.trackBarAnimationKeyframe)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numericAnimationSpeed)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numericAnimationKeyframe)).EndInit();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ListView listViewAnimationTrack;
		private System.Windows.Forms.ColumnHeader columnHeader3;
		private System.Windows.Forms.ColumnHeader columnHeader4;
		private System.Windows.Forms.ColumnHeader columnHeader1;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Button buttonAnimationPlayPause;
		private System.Windows.Forms.TrackBar trackBarAnimationKeyframe;
		public System.Windows.Forms.NumericUpDown numericAnimationSpeed;
		private System.Windows.Forms.NumericUpDown numericAnimationKeyframe;
		private System.Windows.Forms.Label labelSkeletalRender;
		private System.Windows.Forms.Label label30;
		private System.Windows.Forms.ImageList imageList1;
		private SB3Utility.EditTextBox textBoxANICunk1;
		private SB3Utility.EditTextBox textBoxANICunk2;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.ToolTip toolTip1;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button buttonAnimationTrackRemove;
	}
}