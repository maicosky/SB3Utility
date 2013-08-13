using WeifenLuo.WinFormsUI.Docking;

namespace PPD_Preview_Clothes
{
	partial class FormSVIEX : DockContent
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
			this.comboBoxTargetXX = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.checkBoxShowTargetNormals = new System.Windows.Forms.CheckBox();
			this.comboBoxTargetMeshes = new System.Windows.Forms.ComboBox();
			this.comboBoxTargetSVIEXunits = new System.Windows.Forms.ComboBox();
			this.textBoxTargetedSubmeshes = new SB3Utility.EditTextBox();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.checkBoxShowSourceNormals = new System.Windows.Forms.CheckBox();
			this.comboBoxCorrectlyLitMeshes = new System.Windows.Forms.ComboBox();
			this.comboBoxSourceSVIEXunits = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.comboBoxCorrectlyLitXX = new System.Windows.Forms.ComboBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBoxSourceSubmeshes = new SB3Utility.EditTextBox();
			this.buttonApproximateNormals = new System.Windows.Forms.Button();
			this.progressBarApproximation = new System.Windows.Forms.ProgressBar();
			this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
			this.numericUpDownNearVertexSqDist = new System.Windows.Forms.NumericUpDown();
			this.label5 = new System.Windows.Forms.Label();
			this.checkBoxNearestNormal = new System.Windows.Forms.CheckBox();
			this.checkBoxAutomatic = new System.Windows.Forms.CheckBox();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownNearVertexSqDist)).BeginInit();
			this.SuspendLayout();
			// 
			// comboBoxTargetXX
			// 
			this.comboBoxTargetXX.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBoxTargetXX.FormattingEnabled = true;
			this.comboBoxTargetXX.Location = new System.Drawing.Point(6, 19);
			this.comboBoxTargetXX.Name = "comboBoxTargetXX";
			this.comboBoxTargetXX.Size = new System.Drawing.Size(198, 21);
			this.comboBoxTargetXX.Sorted = true;
			this.comboBoxTargetXX.TabIndex = 12;
			this.comboBoxTargetXX.SelectedIndexChanged += new System.EventHandler(this.comboBoxTargetXX_SelectedIndexChanged);
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 53);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(97, 13);
			this.label2.TabIndex = 2;
			this.label2.Text = "Target SVIEX units";
			// 
			// label3
			// 
			this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(219, 53);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(108, 13);
			this.label3.TabIndex = 4;
			this.label3.Text = "Targeted Submeshes";
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.checkBoxShowTargetNormals);
			this.groupBox1.Controls.Add(this.comboBoxTargetMeshes);
			this.groupBox1.Controls.Add(this.comboBoxTargetSVIEXunits);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Controls.Add(this.comboBoxTargetXX);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.textBoxTargetedSubmeshes);
			this.groupBox1.Location = new System.Drawing.Point(14, 12);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(375, 130);
			this.groupBox1.TabIndex = 10;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Selected Target Meshes";
			// 
			// checkBoxShowTargetNormals
			// 
			this.checkBoxShowTargetNormals.Appearance = System.Windows.Forms.Appearance.Button;
			this.checkBoxShowTargetNormals.AutoSize = true;
			this.checkBoxShowTargetNormals.Location = new System.Drawing.Point(45, 98);
			this.checkBoxShowTargetNormals.Name = "checkBoxShowTargetNormals";
			this.checkBoxShowTargetNormals.Size = new System.Drawing.Size(119, 23);
			this.checkBoxShowTargetNormals.TabIndex = 25;
			this.checkBoxShowTargetNormals.Text = "Show SVIEX Normals";
			this.toolTip1.SetToolTip(this.checkBoxShowTargetNormals, "1. Select Normals from \"Target SVIEX units\"\r\n2. Click \"Show SVIEX Normals\"\r\n3. Cl" +
        "ick \"Show SVIEX Normals\" again to revert");
			this.checkBoxShowTargetNormals.UseVisualStyleBackColor = true;
			this.checkBoxShowTargetNormals.Click += new System.EventHandler(this.checkBoxShowTargetNormals_Click);
			// 
			// comboBoxTargetMeshes
			// 
			this.comboBoxTargetMeshes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.comboBoxTargetMeshes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBoxTargetMeshes.FormattingEnabled = true;
			this.comboBoxTargetMeshes.Location = new System.Drawing.Point(222, 19);
			this.comboBoxTargetMeshes.Name = "comboBoxTargetMeshes";
			this.comboBoxTargetMeshes.Size = new System.Drawing.Size(147, 21);
			this.comboBoxTargetMeshes.Sorted = true;
			this.comboBoxTargetMeshes.TabIndex = 14;
			this.comboBoxTargetMeshes.SelectedIndexChanged += new System.EventHandler(this.comboBoxTargetMeshes_SelectedIndexChanged);
			// 
			// comboBoxTargetSVIEXunits
			// 
			this.comboBoxTargetSVIEXunits.BackColor = System.Drawing.SystemColors.InactiveCaptionText;
			this.comboBoxTargetSVIEXunits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBoxTargetSVIEXunits.FormattingEnabled = true;
			this.comboBoxTargetSVIEXunits.Location = new System.Drawing.Point(9, 71);
			this.comboBoxTargetSVIEXunits.Name = "comboBoxTargetSVIEXunits";
			this.comboBoxTargetSVIEXunits.Size = new System.Drawing.Size(195, 21);
			this.comboBoxTargetSVIEXunits.TabIndex = 16;
			this.toolTip1.SetToolTip(this.comboBoxTargetSVIEXunits, "Non existing units will be generated");
			// 
			// textBoxTargetedSubmeshes
			// 
			this.textBoxTargetedSubmeshes.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxTargetedSubmeshes.Location = new System.Drawing.Point(222, 71);
			this.textBoxTargetedSubmeshes.Name = "textBoxTargetedSubmeshes";
			this.textBoxTargetedSubmeshes.Size = new System.Drawing.Size(147, 20);
			this.textBoxTargetedSubmeshes.TabIndex = 18;
			this.toolTip1.SetToolTip(this.textBoxTargetedSubmeshes, "Submeshes to create normals for.\r\nProvide a comma seperated list or\r\n-1 for all s" +
        "ubmeshes.");
			this.textBoxTargetedSubmeshes.AfterEditTextChanged += new System.EventHandler(this.textBoxTargetedSubmeshes_AfterEditTextChanged);
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.checkBoxShowSourceNormals);
			this.groupBox2.Controls.Add(this.comboBoxCorrectlyLitMeshes);
			this.groupBox2.Controls.Add(this.comboBoxSourceSVIEXunits);
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Controls.Add(this.comboBoxCorrectlyLitXX);
			this.groupBox2.Controls.Add(this.label4);
			this.groupBox2.Controls.Add(this.textBoxSourceSubmeshes);
			this.groupBox2.Location = new System.Drawing.Point(14, 148);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(375, 130);
			this.groupBox2.TabIndex = 20;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Correctly Lit Meshes";
			// 
			// checkBoxShowSourceNormals
			// 
			this.checkBoxShowSourceNormals.Appearance = System.Windows.Forms.Appearance.Button;
			this.checkBoxShowSourceNormals.AutoSize = true;
			this.checkBoxShowSourceNormals.Location = new System.Drawing.Point(45, 98);
			this.checkBoxShowSourceNormals.Name = "checkBoxShowSourceNormals";
			this.checkBoxShowSourceNormals.Size = new System.Drawing.Size(119, 23);
			this.checkBoxShowSourceNormals.TabIndex = 35;
			this.checkBoxShowSourceNormals.Text = "Show SVIEX Normals";
			this.toolTip1.SetToolTip(this.checkBoxShowSourceNormals, "1. Select Normals from \"Source SVIEX units\"\r\n2. Click \"Show SVIEX Normals\"\r\n3. Cl" +
        "ick \"Show SVIEX Normals\" again to revert\r\n");
			this.checkBoxShowSourceNormals.UseVisualStyleBackColor = true;
			this.checkBoxShowSourceNormals.Click += new System.EventHandler(this.checkBoxShowSourceNormals_Click);
			// 
			// comboBoxCorrectlyLitMeshes
			// 
			this.comboBoxCorrectlyLitMeshes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.comboBoxCorrectlyLitMeshes.BackColor = System.Drawing.SystemColors.InactiveCaptionText;
			this.comboBoxCorrectlyLitMeshes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBoxCorrectlyLitMeshes.FormattingEnabled = true;
			this.comboBoxCorrectlyLitMeshes.Location = new System.Drawing.Point(222, 19);
			this.comboBoxCorrectlyLitMeshes.Name = "comboBoxCorrectlyLitMeshes";
			this.comboBoxCorrectlyLitMeshes.Size = new System.Drawing.Size(147, 21);
			this.comboBoxCorrectlyLitMeshes.Sorted = true;
			this.comboBoxCorrectlyLitMeshes.TabIndex = 24;
			this.toolTip1.SetToolTip(this.comboBoxCorrectlyLitMeshes, "Present in the Source SVIEX unit");
			this.comboBoxCorrectlyLitMeshes.SelectedIndexChanged += new System.EventHandler(this.comboBoxCorrectlyLitMeshes_SelectedIndexChanged);
			// 
			// comboBoxSourceSVIEXunits
			// 
			this.comboBoxSourceSVIEXunits.BackColor = System.Drawing.SystemColors.InactiveCaptionText;
			this.comboBoxSourceSVIEXunits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBoxSourceSVIEXunits.FormattingEnabled = true;
			this.comboBoxSourceSVIEXunits.Location = new System.Drawing.Point(9, 71);
			this.comboBoxSourceSVIEXunits.Name = "comboBoxSourceSVIEXunits";
			this.comboBoxSourceSVIEXunits.Size = new System.Drawing.Size(195, 21);
			this.comboBoxSourceSVIEXunits.TabIndex = 26;
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(219, 53);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(99, 13);
			this.label1.TabIndex = 4;
			this.label1.Text = "Source Submeshes";
			// 
			// comboBoxCorrectlyLitXX
			// 
			this.comboBoxCorrectlyLitXX.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBoxCorrectlyLitXX.FormattingEnabled = true;
			this.comboBoxCorrectlyLitXX.Location = new System.Drawing.Point(6, 19);
			this.comboBoxCorrectlyLitXX.Name = "comboBoxCorrectlyLitXX";
			this.comboBoxCorrectlyLitXX.Size = new System.Drawing.Size(198, 21);
			this.comboBoxCorrectlyLitXX.Sorted = true;
			this.comboBoxCorrectlyLitXX.TabIndex = 22;
			this.comboBoxCorrectlyLitXX.SelectedIndexChanged += new System.EventHandler(this.comboBoxCorrectlyLitXX_SelectedIndexChanged);
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(6, 53);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(100, 13);
			this.label4.TabIndex = 2;
			this.label4.Text = "Source SVIEX units";
			// 
			// textBoxSourceSubmeshes
			// 
			this.textBoxSourceSubmeshes.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxSourceSubmeshes.Location = new System.Drawing.Point(222, 71);
			this.textBoxSourceSubmeshes.Name = "textBoxSourceSubmeshes";
			this.textBoxSourceSubmeshes.ReadOnly = true;
			this.textBoxSourceSubmeshes.Size = new System.Drawing.Size(147, 20);
			this.textBoxSourceSubmeshes.TabIndex = 28;
			this.textBoxSourceSubmeshes.AfterEditTextChanged += new System.EventHandler(this.textBoxSourceSubmeshes_AfterEditTextChanged);
			// 
			// buttonApproximateNormals
			// 
			this.buttonApproximateNormals.Enabled = false;
			this.buttonApproximateNormals.Location = new System.Drawing.Point(122, 333);
			this.buttonApproximateNormals.Name = "buttonApproximateNormals";
			this.buttonApproximateNormals.Size = new System.Drawing.Size(159, 23);
			this.buttonApproximateNormals.TabIndex = 100;
			this.buttonApproximateNormals.Text = "Approximate Target Normals";
			this.toolTip1.SetToolTip(this.buttonApproximateNormals, "Normals are copied based on nearest vertices.");
			this.buttonApproximateNormals.UseVisualStyleBackColor = true;
			this.buttonApproximateNormals.Click += new System.EventHandler(this.buttonApproximateNormals_Click);
			// 
			// progressBarApproximation
			// 
			this.progressBarApproximation.Location = new System.Drawing.Point(14, 364);
			this.progressBarApproximation.Maximum = 1;
			this.progressBarApproximation.Name = "progressBarApproximation";
			this.progressBarApproximation.Size = new System.Drawing.Size(375, 25);
			this.progressBarApproximation.Step = 1;
			this.progressBarApproximation.TabIndex = 31;
			// 
			// numericUpDownNearVertexSqDist
			// 
			this.numericUpDownNearVertexSqDist.DecimalPlaces = 6;
			this.numericUpDownNearVertexSqDist.Increment = new decimal(new int[] {
            1,
            0,
            0,
            327680});
			this.numericUpDownNearVertexSqDist.Location = new System.Drawing.Point(129, 294);
			this.numericUpDownNearVertexSqDist.Name = "numericUpDownNearVertexSqDist";
			this.numericUpDownNearVertexSqDist.Size = new System.Drawing.Size(71, 20);
			this.numericUpDownNearVertexSqDist.TabIndex = 50;
			this.toolTip1.SetToolTip(this.numericUpDownNearVertexSqDist, "Source and destination vertices having a smaller distance²\r\nare considered to hav" +
        "e the same position.");
			this.numericUpDownNearVertexSqDist.Value = new decimal(new int[] {
            1,
            0,
            0,
            327680});
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(12, 296);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(111, 13);
			this.label5.TabIndex = 101;
			this.label5.Text = "Near Vertex Distance²";
			// 
			// checkBoxNearestNormal
			// 
			this.checkBoxNearestNormal.AutoSize = true;
			this.checkBoxNearestNormal.Checked = true;
			this.checkBoxNearestNormal.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBoxNearestNormal.Location = new System.Drawing.Point(206, 295);
			this.checkBoxNearestNormal.Name = "checkBoxNearestNormal";
			this.checkBoxNearestNormal.Size = new System.Drawing.Size(99, 17);
			this.checkBoxNearestNormal.TabIndex = 60;
			this.checkBoxNearestNormal.Text = "Nearest Normal";
			this.toolTip1.SetToolTip(this.checkBoxNearestNormal, "Either take the nearest normal or sum all normals of the same position.");
			this.checkBoxNearestNormal.UseVisualStyleBackColor = true;
			// 
			// checkBoxAutomatic
			// 
			this.checkBoxAutomatic.AutoSize = true;
			this.checkBoxAutomatic.Location = new System.Drawing.Point(311, 295);
			this.checkBoxAutomatic.Name = "checkBoxAutomatic";
			this.checkBoxAutomatic.Size = new System.Drawing.Size(73, 17);
			this.checkBoxAutomatic.TabIndex = 102;
			this.checkBoxAutomatic.Text = "Automatic";
			this.toolTip1.SetToolTip(this.checkBoxAutomatic, "Automatically chooses \"Nearest Normal\" for EACH vertex.\r\nThe decision is based on" +
        " another vertex with the same\r\nposition in the same destination submesh.");
			this.checkBoxAutomatic.UseVisualStyleBackColor = true;
			this.checkBoxAutomatic.Click += new System.EventHandler(this.checkBoxAutomatic_Click);
			// 
			// FormSVIEX
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(403, 401);
			this.Controls.Add(this.checkBoxAutomatic);
			this.Controls.Add(this.checkBoxNearestNormal);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.numericUpDownNearVertexSqDist);
			this.Controls.Add(this.progressBarApproximation);
			this.Controls.Add(this.buttonApproximateNormals);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.groupBox1);
			this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.Name = "FormSVIEX";
			this.Text = "FormSVIEX";
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownNearVertexSqDist)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ComboBox comboBoxTargetXX;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private SB3Utility.EditTextBox textBoxTargetedSubmeshes;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ComboBox comboBoxCorrectlyLitXX;
		private System.Windows.Forms.Label label4;
		private SB3Utility.EditTextBox textBoxSourceSubmeshes;
		private System.Windows.Forms.Button buttonApproximateNormals;
		private System.Windows.Forms.ProgressBar progressBarApproximation;
		private System.Windows.Forms.ComboBox comboBoxTargetSVIEXunits;
		private System.Windows.Forms.ComboBox comboBoxSourceSVIEXunits;
		private System.Windows.Forms.ToolTip toolTip1;
		private System.Windows.Forms.ComboBox comboBoxTargetMeshes;
		private System.Windows.Forms.ComboBox comboBoxCorrectlyLitMeshes;
		private System.Windows.Forms.CheckBox checkBoxShowTargetNormals;
		private System.Windows.Forms.CheckBox checkBoxShowSourceNormals;
		private System.Windows.Forms.NumericUpDown numericUpDownNearVertexSqDist;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.CheckBox checkBoxNearestNormal;
		private System.Windows.Forms.CheckBox checkBoxAutomatic;
	}
}