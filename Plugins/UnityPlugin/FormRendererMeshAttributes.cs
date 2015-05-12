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
	public partial class FormRendererMeshAttributes : Form
	{
		private SizeF startSize;
		private bool contentChanged = false;

		public FormRendererMeshAttributes(MeshRenderer meshR, int selectedSubmesh)
		{
			InitializeComponent();
			startSize = new SizeF(Width, Height);
			this.SaveDesignSizes();

			Text = meshR.classID1 + " " + meshR.m_GameObject.instance.m_Name + " Attributes";

			checkBoxRendererCastShadows.Checked = meshR.m_CastShadows;
			checkBoxRendererReceiveShadows.Checked = meshR.m_ReceiveShadows;
			editTextBoxRendererLightMap.Text = meshR.m_LightmapIndex.ToString();
			editTextBoxRendererTilingOffset.Text = "X:" + meshR.m_LightmapTilingOffset.X.ToFloatString() + ", Y:" + meshR.m_LightmapTilingOffset.Y.ToFloatString() + ", Z:" + meshR.m_LightmapTilingOffset.Z.ToFloatString() + ", W:" + meshR.m_LightmapTilingOffset.W.ToFloatString();
			checkBoxRendererSubsetIndices.Checked = meshR.m_SubsetIndices.Length > 0;
			editTextBoxRendererStaticBatchRoot.Text = meshR.m_StaticBatchRoot.instance != null ? meshR.m_StaticBatchRoot.instance.m_GameObject.instance.m_Name : String.Empty;
			checkBoxRendererUseLightProbes.Checked = meshR.m_UseLightProbes;
			editTextBoxRendererLightProbeAnchor.Text = meshR.m_LightProbeAnchor.instance != null ? meshR.m_LightProbeAnchor.instance.m_GameObject.instance.m_Name : String.Empty;
			editTextBoxRendererSortingLayerID.Text = meshR.m_SortingLayerID.ToString();
			editTextBoxRendererSortingOrder.Text = meshR.m_SortingOrder.ToString();

			if (meshR is SkinnedMeshRenderer)
			{
				SkinnedMeshRenderer smr= (SkinnedMeshRenderer)meshR;
				editTextBoxSkinnedMeshRendererQuality.Text = smr.m_Quality.ToString();
				checkBoxSkinnedMeshRendererUpdateWhenOffScreen.Checked = smr.m_UpdateWhenOffScreen;
				editTextBoxSkinnedMeshRendererBones.Text = smr.m_Bones.Count.ToString();
				editTextBoxSkinnedMeshRendererBlendShapeWeights.Text = smr.m_BlendShapeWeights.Count.ToString();
				editTextBoxSkinnedMeshRendererAABBCenter.Text = "X:" + smr.m_AABB.m_Center.X.ToFloatString() + ", Y:" + smr.m_AABB.m_Center.Y.ToFloatString() + ", Z:" + smr.m_AABB.m_Center.Z.ToFloatString();
				editTextBoxSkinnedMeshRendererAABBExtend.Text = "X:" + smr.m_AABB.m_Extend.X.ToFloatString() + ", Y:" + smr.m_AABB.m_Extend.Y.ToFloatString() + ", Z:" + smr.m_AABB.m_Extend.Z.ToFloatString();
				checkBoxSkinnedMeshRendererDirtyAABB.Checked = smr.m_DirtyAABB;
			}
			else
			{
				groupBoxSkinnedMeshRenderer.Enabled = false;
			}

			Mesh mesh = Operations.GetMesh(meshR);
			if (mesh != null)
			{
				editTextBoxMeshBlendShape.Text = mesh.m_Shapes.vertices.Count + "/" + mesh.m_Shapes.shapes.Count + "/" + mesh.m_Shapes.fullWeights.Count;
				editTextBoxMeshBindPose.Text = mesh.m_BindPose.Count.ToString();
				editTextBoxMeshBoneHashes.Text = mesh.m_BoneNameHashes.Count.ToString();
				checkBoxMeshCompression.Checked = mesh.m_MeshCompression > 0;
				checkBoxMeshStreamCompression.Checked = mesh.m_StreamCompression > 0;
				checkBoxMeshReadable.Checked = mesh.m_IsReadable;
				checkBoxMeshKeepVertices.Checked = mesh.m_KeepVertices;
				checkBoxMeshKeepIndices.Checked = mesh.m_KeepIndices;
				editTextBoxMeshInfluences.Text = mesh.m_Skin.Count.ToString();
				editTextBoxMeshUsageFlags.Text = mesh.m_MeshUsageFlags.ToString();
				editTextBoxMeshCenter.Text = "X:" + mesh.m_LocalAABB.m_Center.X.ToFloatString() + ", Y:" + mesh.m_LocalAABB.m_Center.Y.ToFloatString() + ", Z:" + mesh.m_LocalAABB.m_Center.Z.ToFloatString();
				editTextBoxMeshExtend.Text = "X:" + mesh.m_LocalAABB.m_Extend.X.ToFloatString() + ", Y:" + mesh.m_LocalAABB.m_Extend.Y.ToFloatString() + ", Z:" + mesh.m_LocalAABB.m_Extend.Z.ToFloatString();

				if (selectedSubmesh >= 0)
				{
					editTextBoxSubmeshCenter.Text = "X:" + mesh.m_SubMeshes[selectedSubmesh].localAABB.m_Center.X.ToFloatString() + ", Y:" + mesh.m_SubMeshes[selectedSubmesh].localAABB.m_Center.Y.ToFloatString() + ", Z:" + mesh.m_SubMeshes[selectedSubmesh].localAABB.m_Center.Z.ToFloatString();
					editTextBoxSubmeshExtend.Text = "X:" + mesh.m_SubMeshes[selectedSubmesh].localAABB.m_Extend.X.ToFloatString() + ", Y:" + mesh.m_SubMeshes[selectedSubmesh].localAABB.m_Extend.Y.ToFloatString() + ", Z:" + mesh.m_SubMeshes[selectedSubmesh].localAABB.m_Extend.Z.ToFloatString();
				}
			}
			else
			{
				groupBoxMesh.Enabled = false;
			}

			checkBoxRendererCastShadows.CheckedChanged += AttributeChanged;
			checkBoxRendererReceiveShadows.CheckedChanged += AttributeChanged;
			editTextBoxRendererLightMap.AfterEditTextChanged += AttributeChanged;
			editTextBoxRendererTilingOffset.AfterEditTextChanged += AttributeChanged;
			checkBoxRendererSubsetIndices.CheckedChanged += AttributeChanged;
			editTextBoxRendererStaticBatchRoot.AfterEditTextChanged += AttributeChanged;
			checkBoxRendererUseLightProbes.CheckedChanged += AttributeChanged;
			editTextBoxRendererSortingLayerID.AfterEditTextChanged += AttributeChanged;
			editTextBoxRendererSortingOrder.AfterEditTextChanged += AttributeChanged;
			editTextBoxRendererLightProbeAnchor.AfterEditTextChanged += AttributeChanged;
			editTextBoxSkinnedMeshRendererQuality.AfterEditTextChanged += AttributeChanged;
			checkBoxSkinnedMeshRendererUpdateWhenOffScreen.CheckedChanged += AttributeChanged;
			editTextBoxSkinnedMeshRendererAABBCenter.AfterEditTextChanged += AttributeChanged;
			editTextBoxSkinnedMeshRendererAABBExtend.AfterEditTextChanged += AttributeChanged;
			checkBoxSkinnedMeshRendererDirtyAABB.CheckedChanged += AttributeChanged;
			checkBoxMeshCompression.CheckedChanged += AttributeChanged;
			checkBoxMeshStreamCompression.CheckedChanged += AttributeChanged;
			checkBoxMeshReadable.CheckedChanged += AttributeChanged;
			checkBoxMeshKeepVertices.CheckedChanged += AttributeChanged;
			checkBoxMeshKeepIndices.CheckedChanged += AttributeChanged;
			editTextBoxMeshUsageFlags.AfterEditTextChanged += AttributeChanged;
			editTextBoxMeshCenter.AfterEditTextChanged += AttributeChanged;
			editTextBoxMeshExtend.AfterEditTextChanged += AttributeChanged;
		}

		private void FormRendererMeshAttributes_Shown(object sender, EventArgs e)
		{
			Size dialogSize = (Size)Properties.Settings.Default["DialogRendererMeshAttributesSize"];
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

		private void FormRendererMeshAttributes_Resize(object sender, EventArgs e)
		{
			this.ResizeControls(startSize);
		}

		private void FormRendererMeshAttributes_VisibleChanged(object sender, EventArgs e)
		{
			if (!Visible)
			{
				if (Width < (int)startSize.Width || Height < (int)startSize.Height)
				{
					Properties.Settings.Default["DialogRendererMeshAttributesSize"] = new Size(0, 0);
				}
				else
				{
					Properties.Settings.Default["DialogRendererMeshAttributesSize"] = this.Size;
				}
			}
		}

		private void AttributeChanged(object sender, EventArgs e)
		{
			contentChanged = true;
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
			this.DialogResult = contentChanged ? DialogResult.OK : DialogResult.Cancel;
		}
	}
}
