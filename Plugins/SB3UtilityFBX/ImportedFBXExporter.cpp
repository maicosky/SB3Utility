#include <fbxsdk.h>
#include <fbxsdk/fileio/fbxiosettings.h>
#include "SB3UtilityFBX.h"

namespace SB3Utility
{
	void Fbx::Exporter::Export(String^ path, IImported^ imported, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, float filterPrecision, String^ exportFormat, bool allFrames, bool allBones, bool skins, bool compatibility)
	{
		FileInfo^ file = gcnew FileInfo(path);
		DirectoryInfo^ dir = file->Directory;
		if (!dir->Exists)
		{
			dir->Create();
		}
		String^ currentDir = Directory::GetCurrentDirectory();
		Directory::SetCurrentDirectory(dir->FullName);
		path = Path::GetFileName(path);

		Exporter^ exporter = gcnew Exporter(path, imported, exportFormat, allFrames, allBones, skins, compatibility);
		exporter->ExportAnimations(startKeyframe, endKeyframe, linear, EulerFilter, filterPrecision);
		exporter->pExporter->Export(exporter->pScene);

		Directory::SetCurrentDirectory(currentDir);
	}

	void Fbx::Exporter::ExportMorph(String^ path, IImported^ imported, String^ exportFormat, bool oneBlendShape, bool compatibility)
	{
		FileInfo^ file = gcnew FileInfo(path);
		DirectoryInfo^ dir = file->Directory;
		if (!dir->Exists)
		{
			dir->Create();
		}
		String^ currentDir = Directory::GetCurrentDirectory();
		Directory::SetCurrentDirectory(dir->FullName);
		path = Path::GetFileName(path);

		Exporter^ exporter = gcnew Exporter(path, imported, exportFormat, false, false, false, compatibility);
		exporter->ExportMorphs(imported, oneBlendShape);
		exporter->pExporter->Export(exporter->pScene);
		delete exporter;

		Directory::SetCurrentDirectory(currentDir);
	}

	Fbx::Exporter::Exporter(String^ path, IImported^ imported, String^ exportFormat, bool allFrames, bool allBones, bool skins, bool compatibility)
	{
		this->imported = imported;
		exportSkins = skins;

		frameNames = nullptr;
		if (!allFrames)
		{
			frameNames = SearchHierarchy();
		}

		cDest = NULL;
		cFormat = NULL;
		pSdkManager = NULL;
		pScene = NULL;
		pExporter = NULL;
		pMaterials = NULL;
		pTextures = NULL;
		pMeshNodes = NULL;

		pin_ptr<FbxManager*> pSdkManagerPin = &pSdkManager;
		pin_ptr<FbxScene*> pScenePin = &pScene;
		Init(pSdkManagerPin, pScenePin);

		cDest = Fbx::StringToCharArray(path);
		cFormat = Fbx::StringToCharArray(exportFormat);
		pExporter = FbxExporter::Create(pSdkManager, "");
		int lFormatIndex, lFormatCount = pSdkManager->GetIOPluginRegistry()->GetWriterFormatCount();
		for (lFormatIndex = 0; lFormatIndex < lFormatCount; lFormatIndex++)
		{
			FbxString lDesc = FbxString(pSdkManager->GetIOPluginRegistry()->GetWriterFormatDescription(lFormatIndex));
			if (lDesc.Find(cFormat) >= 0)
			{
				if (pSdkManager->GetIOPluginRegistry()->WriterIsFBX(lFormatIndex))
				{
					if (lDesc.Find("binary") >= 0)
					{
						if (!compatibility || lDesc.Find("6.") >= 0)
						{
							break;
						}
					}
				}
				else
				{
					break;
				}
			}
		}

		IOS_REF.SetBoolProp(EXP_FBX_MATERIAL, true);
		IOS_REF.SetBoolProp(EXP_FBX_TEXTURE, true);
		IOS_REF.SetBoolProp(EXP_FBX_EMBEDDED, false);
		IOS_REF.SetBoolProp(EXP_FBX_SHAPE, true);
		IOS_REF.SetBoolProp(EXP_FBX_GOBO, true);
		IOS_REF.SetBoolProp(EXP_FBX_ANIMATION, true);
		IOS_REF.SetBoolProp(EXP_FBX_GLOBAL_SETTINGS, true);

		FbxGlobalSettings& globalSettings = pScene->GetGlobalSettings();
		FbxTime::EMode pTimeMode = FbxTime::eFrames24;
		globalSettings.SetTimeMode(pTimeMode);

		if (!pExporter->Initialize(cDest, lFormatIndex, pSdkManager->GetIOSettings()))
		{
			throw gcnew Exception(gcnew String("Failed to initialize FbxExporter: ") + gcnew String(pExporter->GetStatus().GetErrorString()));
		}

		pMaterials = new FbxArray<FbxSurfacePhong*>();
		pTextures = new FbxArray<FbxFileTexture*>();
		pMaterials->Reserve(imported->MaterialList->Count);
		pTextures->Reserve(imported->TextureList->Count);

		pMeshNodes = new FbxArray<FbxNode*>(imported->MeshList->Count);
		ExportFrame(pScene->GetRootNode(), imported->FrameList[0]);

		SetJointsFromImportedMeshes(allBones);

		for (int i = 0; i < pMeshNodes->GetCount(); i++)
		{
			FbxNode* meshNode = pMeshNodes->GetAt(i);
			String^ meshPath = gcnew String(meshNode->GetName());
			FbxNode* rootNode = meshNode;
			while ((rootNode = rootNode->GetParent()) != pScene->GetRootNode())
			{
				meshPath = gcnew String(rootNode->GetName()) + "/" + meshPath;
			}
			ImportedMesh^ mesh = ImportedHelpers::FindMesh(meshPath, imported->MeshList);
			ExportMesh(meshNode, mesh);
		}
	}

	HashSet<String^>^ Fbx::Exporter::SearchHierarchy()
	{
		HashSet<String^>^ exportFrames = gcnew HashSet<String^>();
		SearchHierarchy(imported->FrameList[0], exportFrames);
		return exportFrames;
	}

	void Fbx::Exporter::SearchHierarchy(ImportedFrame^ frame, HashSet<String^>^ exportFrames)
	{
		ImportedMesh^ meshListSome = ImportedHelpers::FindMesh(frame, imported->MeshList);
		if (meshListSome != nullptr)
		{
			ImportedFrame^ parent = frame;
			while (parent != nullptr)
			{
				exportFrames->Add(parent->Name);
				parent = (ImportedFrame^)parent->Parent;
			}

			List<ImportedBone^>^ boneList = meshListSome->BoneList;
			if (boneList != nullptr)
			{
				for (int i = 0; i < boneList->Count; i++)
				{
					if (!exportFrames->Contains(boneList[i]->Name))
					{
						ImportedFrame^ boneParent = ImportedHelpers::FindFrame(boneList[i]->Name, imported->FrameList[0]);
						while (boneParent != nullptr)
						{
							exportFrames->Add(boneParent->Name);
							boneParent = (ImportedFrame^)boneParent->Parent;
						}
					}
				}
			}
		}

		for (int i = 0; i < frame->Count; i++)
		{
			SearchHierarchy(frame[i], exportFrames);
		}
	}

	void Fbx::Exporter::SetJointsFromImportedMeshes(bool allBones)
	{
		if (!exportSkins)
		{
			return;
		}
		HashSet<String^>^ boneNames = gcnew HashSet<String^>();
		for (int i = 0; i < imported->MeshList->Count; i++)
		{
			ImportedMesh^ meshList = imported->MeshList[i];
			List<ImportedBone^>^ boneList = meshList->BoneList;
			if (boneList != nullptr)
			{
				for (int j = 0; j < boneList->Count; j++)
				{
					ImportedBone^ bone = boneList[j];
					boneNames->Add(bone->Name);
				}
			}
		}

		SetJointsNode(pScene->GetRootNode()->GetChild(0), boneNames, allBones);
	}

	void Fbx::Exporter::ExportFrame(FbxNode* pParentNode, ImportedFrame^ frame)
	{
		String^ frameName = frame->Name;
		if ((frameNames == nullptr) || frameNames->Contains(frameName))
		{
			FbxNode* pFrameNode = NULL;
			char* pName = NULL;
			try
			{
				pName = StringToCharArray(frameName);
				pFrameNode = FbxNode::Create(pSdkManager, pName);
			}
			finally
			{
				Marshal::FreeHGlobal((IntPtr)pName);
			}

			Vector3 scale, translate;
			Quaternion rotate;
			frame->Matrix.Decompose(scale, rotate, translate);
			Vector3 rotateVector = Fbx::QuaternionToEuler(rotate);

			pFrameNode->LclScaling.Set(FbxVector4(scale.X , scale.Y, scale.Z));
			pFrameNode->LclRotation.Set(FbxVector4(FbxDouble3(rotateVector.X, rotateVector.Y, rotateVector.Z)));
			pFrameNode->LclTranslation.Set(FbxVector4(translate.X, translate.Y, translate.Z));
			pParentNode->AddChild(pFrameNode);

			if (ImportedHelpers::FindMesh(frame, imported->MeshList) != nullptr)
			{
				pMeshNodes->Add(pFrameNode);
			}

			for (int i = 0; i < frame->Count; i++)
			{
				ExportFrame(pFrameNode, frame[i]);
			}
		}
	}

	void Fbx::Exporter::ExportMesh(FbxNode* pFrameNode, ImportedMesh^ meshList)
	{
		int lastSlash = meshList->Name->LastIndexOf('/');
		String^ frameName = lastSlash < 0 ? meshList->Name : meshList->Name->Substring(lastSlash + 1);
		List<ImportedBone^>^ boneList = meshList->BoneList;
		bool hasBones;
		if (exportSkins && boneList != nullptr)
		{
			hasBones = boneList->Count > 0;
		}
		else
		{
			hasBones = false;
		}

		FbxArray<FbxNode*>* pBoneNodeList = NULL;
		try
		{
			if (hasBones)
			{
				pBoneNodeList = new FbxArray<FbxNode*>();
				pBoneNodeList->Reserve(boneList->Count);
				for (int i = 0; i < boneList->Count; i++)
				{
					ImportedBone^ bone = boneList[i];
					String^ boneName = bone->Name;
					char* pBoneName = NULL;
					try
					{
						pBoneName = StringToCharArray(boneName);
						FbxNode* foundNode = pScene->GetRootNode()->FindChild(pBoneName);
						if (foundNode == NULL)
						{
							throw gcnew Exception(gcnew String("Couldn't find frame ") + boneName + gcnew String(" used by the bone"));
						}
						pBoneNodeList->Add(foundNode);
					}
					finally
					{
						Marshal::FreeHGlobal((IntPtr)pBoneName);
					}
				}
			}

			for (int i = 0; i < meshList->SubmeshList->Count; i++)
			{
				char* pName = NULL;
				FbxArray<FbxCluster*>* pClusterArray = NULL;
				try
				{
					pName = StringToCharArray(frameName + "_" + i);
					FbxMesh* pMesh = FbxMesh::Create(pSdkManager, "");

					if (hasBones)
					{
						pClusterArray = new FbxArray<FbxCluster*>();
						pClusterArray->Reserve(boneList->Count);

						for (int i = 0; i < boneList->Count; i++)
						{
							FbxNode* pNode = pBoneNodeList->GetAt(i);
							FbxString lClusterName = pNode->GetNameOnly() + FbxString("Cluster");
							FbxCluster* pCluster = FbxCluster::Create(pSdkManager, lClusterName.Buffer());
							pCluster->SetLink(pNode);
							pCluster->SetLinkMode(FbxCluster::eTotalOne);
							pClusterArray->Add(pCluster);
						}
					}

					ImportedSubmesh^ meshObj = meshList->SubmeshList[i];
					List<ImportedFace^>^ faceList = meshObj->FaceList;
					List<ImportedVertex^>^ vertexList = meshObj->VertexList;

					FbxLayer* pLayer = pMesh->GetLayer(0);
					if (pLayer == NULL)
					{
						pMesh->CreateLayer();
						pLayer = pMesh->GetLayer(0);
					}

					pMesh->InitControlPoints(vertexList->Count);
					FbxVector4* pControlPoints = pMesh->GetControlPoints();

					FbxLayerElementNormal* pLayerElementNormal = FbxLayerElementNormal::Create(pMesh, "");
					pLayerElementNormal->SetMappingMode(FbxLayerElement::eByControlPoint);
					pLayerElementNormal->SetReferenceMode(FbxLayerElement::eDirect);
					pLayer->SetNormals(pLayerElementNormal);

					FbxLayerElementUV* pUVLayer = FbxLayerElementUV::Create(pMesh, "");
					pUVLayer->SetMappingMode(FbxLayerElement::eByControlPoint);
					pUVLayer->SetReferenceMode(FbxLayerElement::eDirect);
					pLayer->SetUVs(pUVLayer, FbxLayerElement::eTextureDiffuse);

					FbxLayerElementTangent* pLayerElementTangent = FbxLayerElementTangent::Create(pMesh, "");
					pLayerElementTangent->SetMappingMode(FbxLayerElement::eByControlPoint);
					pLayerElementTangent->SetReferenceMode(FbxLayerElement::eDirect);
					pLayer->SetTangents(pLayerElementTangent);

					FbxNode* pMeshNode = FbxNode::Create(pSdkManager, pName);
					pMeshNode->SetNodeAttribute(pMesh);
					pFrameNode->AddChild(pMeshNode);

					ImportedMaterial^ mat = ImportedHelpers::FindMaterial(meshObj->Material, imported->MaterialList);
					if (mat != nullptr)
					{
						FbxLayerElementMaterial* pMaterialLayer = FbxLayerElementMaterial::Create(pMesh, "");
						pMaterialLayer->SetMappingMode(FbxLayerElement::eAllSame);
						pMaterialLayer->SetReferenceMode(FbxLayerElement::eIndexToDirect);
						pMaterialLayer->GetIndexArray().Add(0);
						pLayer->SetMaterials(pMaterialLayer);

						char* pMatName = NULL;
						try
						{
							pMatName = StringToCharArray(mat->Name);
							int foundMat = -1;
							for (int j = 0; j < pMaterials->GetCount(); j++)
							{
								FbxSurfacePhong* pMatTemp = pMaterials->GetAt(j);
								if (strcmp(pMatTemp->GetName(), pMatName) == 0)
								{
									foundMat = j;
									break;
								}
							}

							FbxSurfacePhong* pMat;
							if (foundMat >= 0)
							{
								pMat = pMaterials->GetAt(foundMat);
							}
							else
							{
								FbxString lShadingName  = "Phong";
								Color4 diffuse = mat->Diffuse;
								Color4 ambient = mat->Ambient;
								Color4 emissive = mat->Emissive;
								Color4 specular = mat->Specular;
								float specularPower = mat->Power;
								pMat = FbxSurfacePhong::Create(pSdkManager, pMatName);
								pMat->Diffuse.Set(FbxDouble3(diffuse.Red, diffuse.Green, diffuse.Blue));
								pMat->DiffuseFactor.Set(FbxDouble(diffuse.Alpha));
								pMat->Ambient.Set(FbxDouble3(ambient.Red, ambient.Green, ambient.Blue));
								pMat->AmbientFactor.Set(FbxDouble(ambient.Alpha));
								pMat->Emissive.Set(FbxDouble3(emissive.Red, emissive.Green, emissive.Blue));
								pMat->EmissiveFactor.Set(FbxDouble(emissive.Alpha));
								pMat->Specular.Set(FbxDouble3(specular.Red, specular.Green, specular.Blue));
								pMat->SpecularFactor.Set(FbxDouble(specular.Alpha));
								pMat->Shininess.Set(specularPower);
								pMat->ShadingModel.Set(lShadingName);

								foundMat = pMaterials->GetCount();
								pMaterials->Add(pMat);
							}
							pMeshNode->AddMaterial(pMat);

							bool hasTexture = false;
							FbxLayerElementTexture* pTextureLayerDiffuse = NULL;
							FbxFileTexture* pTextureDiffuse = ExportTexture(ImportedHelpers::FindTexture((String^)mat->Textures[0], imported->TextureList), pTextureLayerDiffuse, pMesh);
							if (pTextureDiffuse != NULL)
							{
								pLayer->SetTextures(FbxLayerElement::eTextureDiffuse, pTextureLayerDiffuse);
								pMat->Diffuse.ConnectSrcObject(pTextureDiffuse);
								hasTexture = true;
							}

							FbxLayerElementTexture* pTextureLayerAmbient = NULL;
							FbxFileTexture* pTextureAmbient = ExportTexture(ImportedHelpers::FindTexture((String^)mat->Textures[1], imported->TextureList), pTextureLayerAmbient, pMesh);
							if (pTextureAmbient != NULL)
							{
								pLayer->SetTextures(FbxLayerElement::eTextureAmbient, pTextureLayerAmbient);
								pMat->Ambient.ConnectSrcObject(pTextureAmbient);
								hasTexture = true;
							}

							FbxLayerElementTexture* pTextureLayerEmissive = NULL;
							FbxFileTexture* pTextureEmissive = ExportTexture(ImportedHelpers::FindTexture((String^)mat->Textures[2], imported->TextureList), pTextureLayerEmissive, pMesh);
							if (pTextureEmissive != NULL)
							{
								pLayer->SetTextures(FbxLayerElement::eTextureEmissive, pTextureLayerEmissive);
								pMat->Emissive.ConnectSrcObject(pTextureEmissive);
								hasTexture = true;
							}

							FbxLayerElementTexture* pTextureLayerSpecular = NULL;
							FbxFileTexture* pTextureSpecular = ExportTexture(ImportedHelpers::FindTexture((String^)mat->Textures[3], imported->TextureList), pTextureLayerSpecular, pMesh);
							if (pTextureSpecular != NULL)
							{
								pLayer->SetTextures(FbxLayerElement::eTextureSpecular, pTextureLayerSpecular);
								pMat->Specular.ConnectSrcObject(pTextureSpecular);
								hasTexture = true;
							}

							if (hasTexture)
							{
								pMeshNode->SetShadingMode(FbxNode::eTextureShading);
							}
						}
						finally
						{
							Marshal::FreeHGlobal((IntPtr)pMatName);
						}
					}

					for (int j = 0; j < vertexList->Count; j++)
					{
						ImportedVertex^ vertex = vertexList[j];
						Vector3 coords = vertex->Position;
						pControlPoints[j] = FbxVector4(coords.X, coords.Y, coords.Z);
						Vector3 normal = vertex->Normal;
						pLayerElementNormal->GetDirectArray().Add(FbxVector4(normal.X, normal.Y, normal.Z));
						array<float>^ uv = vertex->UV;
						pUVLayer->GetDirectArray().Add(FbxVector2(uv[0], -uv[1]));
						Vector4 tangent = vertex->Tangent;
						pLayerElementTangent->GetDirectArray().Add(FbxVector4(tangent.X, tangent.Y, tangent.Z, tangent.W));

						if (hasBones)
						{
							array<unsigned char>^ boneIndices = vertex->BoneIndices;
							array<float>^ weights4 = vertex->Weights;
							for (int k = 0; k < weights4->Length; k++)
							{
								if (boneIndices[k] < boneList->Count && weights4[k] > 0)
								{
									FbxCluster* pCluster = pClusterArray->GetAt(boneIndices[k]);
									pCluster->AddControlPointIndex(j, weights4[k]);
								}
							}
						}
					}

					for (int j = 0; j < faceList->Count; j++)
					{
						ImportedFace^ face = faceList[j];
						unsigned short v1 = (unsigned short)face->VertexIndices[0];
						unsigned short v2 = (unsigned short)face->VertexIndices[1];
						unsigned short v3 = (unsigned short)face->VertexIndices[2];
						pMesh->BeginPolygon(false);
						pMesh->AddPolygon(v1);
						pMesh->AddPolygon(v2);
						pMesh->AddPolygon(v3);
						pMesh->EndPolygon();
					}

					if (hasBones)
					{
						FbxSkin* pSkin = FbxSkin::Create(pSdkManager, "");
						for (int j = 0; j < boneList->Count; j++)
						{
							FbxCluster* pCluster = pClusterArray->GetAt(j);
							if (pCluster->GetControlPointIndicesCount() > 0)
							{
								FbxNode* pBoneNode = pBoneNodeList->GetAt(j);
								Matrix boneMatrix = boneList[j]->Matrix;
								FbxAMatrix lBoneMatrix;
								for (int m = 0; m < 4; m++)
								{
									for (int n = 0; n < 4; n++)
									{
										lBoneMatrix.mData[m][n] = boneMatrix[m, n];
									}
								}

								FbxAMatrix lMeshMatrix = pMeshNode->EvaluateGlobalTransform();

								pCluster->SetTransformMatrix(lMeshMatrix);
								pCluster->SetTransformLinkMatrix(lMeshMatrix * lBoneMatrix.Inverse());

								pSkin->AddCluster(pCluster);
							}
						}

						if (pSkin->GetClusterCount() > 0)
						{
							pMesh->AddDeformer(pSkin);
						}
					}
				}
				finally
				{
					if (pClusterArray != NULL)
					{
						delete pClusterArray;
					}
					Marshal::FreeHGlobal((IntPtr)pName);
				}
			}
		}
		finally
		{
			if (pBoneNodeList != NULL)
			{
				delete pBoneNodeList;
			}
		}
	}

	FbxFileTexture* Fbx::Exporter::ExportTexture(ImportedTexture^ matTex, FbxLayerElementTexture*& pTextureLayer, FbxMesh* pMesh)
	{
		FbxFileTexture* pTex = NULL;

		if (matTex != nullptr)
		{
			String^ matTexName = matTex->Name;

			pTextureLayer = FbxLayerElementTexture::Create(pMesh, "");
			pTextureLayer->SetMappingMode(FbxLayerElement::eAllSame);
			pTextureLayer->SetReferenceMode(FbxLayerElement::eDirect);

			char* pTexName = NULL;
			try
			{
				pTexName = StringToCharArray(matTexName);
				int foundTex = -1;
				for (int i = 0; i < pTextures->GetCount(); i++)
				{
					FbxFileTexture* pTexTemp = pTextures->GetAt(i);
					if (strcmp(pTexTemp->GetName(), pTexName) == 0)
					{
						foundTex = i;
						break;
					}
				}

				if (foundTex >= 0)
				{
					pTex = pTextures->GetAt(foundTex);
				}
				else
				{
					pTex = FbxFileTexture::Create(pSdkManager, pTexName);
					pTex->SetFileName(pTexName);
					pTex->SetTextureUse(FbxTexture::eStandard);
					pTex->SetMappingType(FbxTexture::eUV);
					pTex->SetMaterialUse(FbxFileTexture::eModelMaterial);
					pTex->SetSwapUV(false);
					pTex->SetTranslation(0.0, 0.0);
					pTex->SetScale(1.0, 1.0);
					pTex->SetRotation(0.0, 0.0);
					pTextures->Add(pTex);

					String^ path = Path::GetDirectoryName(gcnew String(pExporter->GetFileName().Buffer()));
					if (path == String::Empty)
					{
						path = ".";
					}
					FileInfo^ file = gcnew FileInfo(path + Path::DirectorySeparatorChar + Path::GetFileName(matTex->Name));
					DirectoryInfo^ dir = file->Directory;
					if (!dir->Exists)
					{
						dir->Create();
					}
					BinaryWriter^ writer = gcnew BinaryWriter(file->Create());
					writer->Write(matTex->Data);
					writer->Close();
				}
				
				pTextureLayer->GetDirectArray().Add(pTex);
			}
			finally
			{
				Marshal::FreeHGlobal((IntPtr)pTexName);
			}
		}

		return pTex;
	}

	void Fbx::Exporter::ExportAnimations(int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, float filterPrecision)
	{
		List<ImportedAnimation^>^ importedAnimationList = imported->AnimationList;
		if (importedAnimationList == nullptr)
		{
			return;
		}

		List<String^>^ pNotFound = gcnew List<String^>();

		FbxPropertyT<FbxDouble3> scale = FbxProperty::Create(pScene, FbxDouble3DT, InterpolationHelper::pScaleName);
		FbxPropertyT<FbxDouble4> rotate = FbxProperty::Create(pScene, FbxDouble4DT, InterpolationHelper::pRotateName);
		FbxPropertyT<FbxDouble3> translate = FbxProperty::Create(pScene, FbxDouble3DT, InterpolationHelper::pTranslateName);

		FbxAnimCurveFilterUnroll* lFilter = EulerFilter ? new FbxAnimCurveFilterUnroll() : NULL;

		for (int i = 0; i < importedAnimationList->Count; i++)
		{
			FbxString kTakeName = FbxString("Take") + FbxString(i);
			bool keyframed = dynamic_cast<ImportedKeyframedAnimation^>(importedAnimationList[i]) != nullptr;
			if (keyframed)
			{
				ImportedKeyframedAnimation^ parser = (ImportedKeyframedAnimation^)importedAnimationList[i];
				ExportKeyframedAnimation(parser, kTakeName, startKeyframe, endKeyframe, linear, lFilter, filterPrecision, scale, rotate, translate, pNotFound);
			}
			else
			{
				ImportedSampledAnimation^ parser = (ImportedSampledAnimation^)importedAnimationList[i];
				ExportSampledAnimation(parser, kTakeName, startKeyframe, endKeyframe, linear, lFilter, filterPrecision, scale, rotate, translate, pNotFound);
			}
		}

		if (pNotFound->Count > 0)
		{
			String^ pNotFoundString = gcnew String("Warning: Animations weren't exported for the following missing frames: ");
			for (int i = 0; i < pNotFound->Count; i++)
			{
				pNotFoundString += pNotFound[i] + ", ";
			}
			Report::ReportLog(pNotFoundString->Substring(0, pNotFoundString->Length - 2));
		}
	}

	void Fbx::Exporter::ExportKeyframedAnimation(ImportedKeyframedAnimation^ parser, FbxString& kTakeName, int startKeyframe, int endKeyframe, bool linear, FbxAnimCurveFilterUnroll* EulerFilter, float filterPrecision,
			FbxPropertyT<FbxDouble3>& scale, FbxPropertyT<FbxDouble4>& rotate, FbxPropertyT<FbxDouble3>& translate, List<String^>^ pNotFound)
	{
		List<ImportedAnimationKeyframedTrack^>^ pAnimationList = parser->TrackList;

		char* lTakeName = kTakeName.Buffer();

		FbxTime lTime;
		FbxAnimStack* lAnimStack = FbxAnimStack::Create(pScene, lTakeName);
		FbxAnimLayer* lAnimLayer = FbxAnimLayer::Create(pScene, "Base Layer");
		lAnimStack->AddMember(lAnimLayer);
		InterpolationHelper^ interpolationHelper;
		int resampleCount = 0;
		if (startKeyframe >= 0)
		{
			interpolationHelper = gcnew InterpolationHelper(pScene, lAnimLayer, linear ? FbxAnimCurveDef::eInterpolationLinear : FbxAnimCurveDef::eInterpolationCubic, &scale, &rotate, &translate);
			for each (ImportedAnimationKeyframedTrack^ track in pAnimationList)
			{
				if (track->Keyframes->Length > resampleCount)
				{
					resampleCount = track->Keyframes->Length;
				}
			}
		}

		for (int j = 0; j < pAnimationList->Count; j++)
		{
			ImportedAnimationKeyframedTrack^ keyframeList = pAnimationList[j];
			String^ name = keyframeList->Name;
			FbxNode* pNode = NULL;
			char* pName = NULL;
			try
			{
				pName = Fbx::StringToCharArray(name);
				pNode = pScene->GetRootNode()->FindChild(pName);
			}
			finally
			{
				Marshal::FreeHGlobal((IntPtr)pName);
			}

			if (pNode == NULL)
			{
				if (!pNotFound->Contains(name))
				{
					pNotFound->Add(name);
				}
			}
			else
			{
				FbxAnimCurve* lCurveSX = pNode->LclScaling.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_X, true);
				FbxAnimCurve* lCurveSY = pNode->LclScaling.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y, true);
				FbxAnimCurve* lCurveSZ = pNode->LclScaling.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z, true);
				FbxAnimCurve* lCurveRX = pNode->LclRotation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_X, true);
				FbxAnimCurve* lCurveRY = pNode->LclRotation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y, true);
				FbxAnimCurve* lCurveRZ = pNode->LclRotation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z, true);
				FbxAnimCurve* lCurveTX = pNode->LclTranslation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_X, true);
				FbxAnimCurve* lCurveTY = pNode->LclTranslation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y, true);
				FbxAnimCurve* lCurveTZ = pNode->LclTranslation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z, true);

				lCurveSX->KeyModifyBegin();
				lCurveSY->KeyModifyBegin();
				lCurveSZ->KeyModifyBegin();
				lCurveRX->KeyModifyBegin();
				lCurveRY->KeyModifyBegin();
				lCurveRZ->KeyModifyBegin();
				lCurveTX->KeyModifyBegin();
				lCurveTY->KeyModifyBegin();
				lCurveTZ->KeyModifyBegin();

				array<ImportedAnimationKeyframe^>^ keyframes = keyframeList->Keyframes;

				double fps = 1.0 / 24;
				int startAt, endAt;
				if (startKeyframe >= 0)
				{
					bool resample = false;
					if (keyframes->Length < resampleCount)
					{
						resample = true;
					}
					else
					{
						for (int k = 0; k < resampleCount; k++)
						{
							if (keyframes[k] == nullptr)
							{
								resample = true;
								break;
							}
						}
					}
					if (resample)
					{
						keyframes = interpolationHelper->InterpolateTrack(keyframes, resampleCount);
					}

					startAt = startKeyframe;
					endAt = endKeyframe < resampleCount ? endKeyframe : resampleCount - 1;
				}
				else
				{
					startAt = 0;
					endAt = keyframes->Length - 1;
				}

				for (int k = startAt, keySetIndex = 0; k <= endAt; k++)
				{
					if (keyframes[k] == nullptr)
						continue;

					lTime.SetSecondDouble(fps * (k - startAt));

					lCurveSX->KeyAdd(lTime);
					lCurveSY->KeyAdd(lTime);
					lCurveSZ->KeyAdd(lTime);
					lCurveRX->KeyAdd(lTime);
					lCurveRY->KeyAdd(lTime);
					lCurveRZ->KeyAdd(lTime);
					lCurveTX->KeyAdd(lTime);
					lCurveTY->KeyAdd(lTime);
					lCurveTZ->KeyAdd(lTime);

					Vector3 rotation = Fbx::QuaternionToEuler(keyframes[k]->Rotation);
					lCurveSX->KeySet(keySetIndex, lTime, keyframes[k]->Scaling.X);
					lCurveSY->KeySet(keySetIndex, lTime, keyframes[k]->Scaling.Y);
					lCurveSZ->KeySet(keySetIndex, lTime, keyframes[k]->Scaling.Z);
					lCurveRX->KeySet(keySetIndex, lTime, rotation.X);
					lCurveRY->KeySet(keySetIndex, lTime, rotation.Y);
					lCurveRZ->KeySet(keySetIndex, lTime, rotation.Z);
					lCurveTX->KeySet(keySetIndex, lTime, keyframes[k]->Translation.X);
					lCurveTY->KeySet(keySetIndex, lTime, keyframes[k]->Translation.Y);
					lCurveTZ->KeySet(keySetIndex, lTime, keyframes[k]->Translation.Z);
					keySetIndex++;
				}
				lCurveSX->KeyModifyEnd();
				lCurveSY->KeyModifyEnd();
				lCurveSZ->KeyModifyEnd();
				lCurveRX->KeyModifyEnd();
				lCurveRY->KeyModifyEnd();
				lCurveRZ->KeyModifyEnd();
				lCurveTX->KeyModifyEnd();
				lCurveTY->KeyModifyEnd();
				lCurveTZ->KeyModifyEnd();

				if (EulerFilter)
				{
					FbxAnimCurve* lCurve [3];
					lCurve[0] = lCurveRX;
					lCurve[1] = lCurveRY;
					lCurve[2] = lCurveRZ;
					EulerFilter->Reset();
					EulerFilter->SetTestForPath(true);
					EulerFilter->SetQualityTolerance(filterPrecision);
					EulerFilter->Apply((FbxAnimCurve**)lCurve, 3);
				}
			}
		}
	}

	void Fbx::Exporter::ExportSampledAnimation(ImportedSampledAnimation^ parser, FbxString& kTakeName, int startKeyframe, int endKeyframe, bool linear, FbxAnimCurveFilterUnroll* EulerFilter, float filterPrecision,
			FbxPropertyT<FbxDouble3>& scale, FbxPropertyT<FbxDouble4>& rotate, FbxPropertyT<FbxDouble3>& translate, List<String^>^ pNotFound)
	{
		List<ImportedAnimationSampledTrack^>^ pAnimationList = parser->TrackList;

		char* lTakeName = kTakeName.Buffer();

		FbxTime lTime;
		FbxAnimStack* lAnimStack = FbxAnimStack::Create(pScene, lTakeName);
		FbxAnimLayer* lAnimLayer = FbxAnimLayer::Create(pScene, "Base Layer");
		lAnimStack->AddMember(lAnimLayer);
		InterpolationHelper^ interpolationHelper;
		int resampleCount = 0;
		if (startKeyframe >= 0)
		{
			interpolationHelper = gcnew InterpolationHelper(pScene, lAnimLayer, linear ? FbxAnimCurveDef::eInterpolationLinear : FbxAnimCurveDef::eInterpolationCubic, &scale, &rotate, &translate);
			for each (ImportedAnimationSampledTrack^ track in pAnimationList)
			{
				if (track->Scalings->Length > resampleCount)
				{
					resampleCount = track->Scalings->Length;
				}
				if (track->Rotations->Length > resampleCount)
				{
					resampleCount = track->Rotations->Length;
				}
				if (track->Translations->Length > resampleCount)
				{
					resampleCount = track->Translations->Length;
				}
			}
		}

		for (int j = 0; j < pAnimationList->Count; j++)
		{
			ImportedAnimationSampledTrack^ sampleList = pAnimationList[j];
			String^ name = sampleList->Name;
			FbxNode* pNode = NULL;
			char* pName = NULL;
			try
			{
				pName = Fbx::StringToCharArray(name);
				pNode = pScene->GetRootNode()->FindChild(pName);
			}
			finally
			{
				Marshal::FreeHGlobal((IntPtr)pName);
			}

			if (pNode == NULL)
			{
				if (!pNotFound->Contains(name))
				{
					pNotFound->Add(name);
				}
			}
			else
			{
				FbxAnimCurve* lCurveSX = pNode->LclScaling.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_X, true);
				FbxAnimCurve* lCurveSY = pNode->LclScaling.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y, true);
				FbxAnimCurve* lCurveSZ = pNode->LclScaling.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z, true);
				FbxAnimCurve* lCurveRX = pNode->LclRotation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_X, true);
				FbxAnimCurve* lCurveRY = pNode->LclRotation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y, true);
				FbxAnimCurve* lCurveRZ = pNode->LclRotation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z, true);
				FbxAnimCurve* lCurveTX = pNode->LclTranslation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_X, true);
				FbxAnimCurve* lCurveTY = pNode->LclTranslation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y, true);
				FbxAnimCurve* lCurveTZ = pNode->LclTranslation.GetCurve(lAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z, true);

				lCurveSX->KeyModifyBegin();
				lCurveSY->KeyModifyBegin();
				lCurveSZ->KeyModifyBegin();
				lCurveRX->KeyModifyBegin();
				lCurveRY->KeyModifyBegin();
				lCurveRZ->KeyModifyBegin();
				lCurveTX->KeyModifyBegin();
				lCurveTY->KeyModifyBegin();
				lCurveTZ->KeyModifyBegin();

				double fps = 1.0 / 24;
				int startAt, endAt;
				if (startKeyframe >= 0)
				{
/*					bool resample = false;
					if (keyframes->Length < resampleCount)
					{
						resample = true;
					}
					else
					{
						for (int k = 0; k < resampleCount; k++)
						{
							if (keyframes[k] == nullptr)
							{
								resample = true;
								break;
							}
						}
					}
					if (resample)
					{
						keyframes = interpolationHelper->InterpolateTrack(keyframes, resampleCount);
					}*/

					startAt = startKeyframe;
					endAt = endKeyframe < resampleCount ? endKeyframe : resampleCount - 1;
				}
				else
				{
					startAt = 0;
					endAt = sampleList->Scalings->Length - 1;
				}

				for (int k = startAt, keySetIndex = 0; k <= endAt; k++)
				{
					if (!sampleList->Scalings[k].HasValue)
						continue;

					lTime.SetSecondDouble(fps * (k - startAt));

					lCurveSX->KeyAdd(lTime);
					lCurveSY->KeyAdd(lTime);
					lCurveSZ->KeyAdd(lTime);

					lCurveSX->KeySet(keySetIndex, lTime, sampleList->Scalings[k].Value.X);
					lCurveSY->KeySet(keySetIndex, lTime, sampleList->Scalings[k].Value.Y);
					lCurveSZ->KeySet(keySetIndex, lTime, sampleList->Scalings[k].Value.Z);
					keySetIndex++;
				}
				for (int k = startAt, keySetIndex = 0; k <= endAt; k++)
				{
					if (!sampleList->Rotations[k].HasValue)
						continue;

					lTime.SetSecondDouble(fps * (k - startAt));

					lCurveRX->KeyAdd(lTime);
					lCurveRY->KeyAdd(lTime);
					lCurveRZ->KeyAdd(lTime);

					Vector3 rotation = Fbx::QuaternionToEuler(sampleList->Rotations[k].Value);
					lCurveRX->KeySet(keySetIndex, lTime, rotation.X);
					lCurveRY->KeySet(keySetIndex, lTime, rotation.Y);
					lCurveRZ->KeySet(keySetIndex, lTime, rotation.Z);
					keySetIndex++;
				}
				for (int k = startAt, keySetIndex = 0; k <= endAt; k++)
				{
					if (!sampleList->Translations[k].HasValue)
						continue;

					lTime.SetSecondDouble(fps * (k - startAt));

					lCurveTX->KeyAdd(lTime);
					lCurveTY->KeyAdd(lTime);
					lCurveTZ->KeyAdd(lTime);

					lCurveTX->KeySet(keySetIndex, lTime, sampleList->Translations[k].Value.X);
					lCurveTY->KeySet(keySetIndex, lTime, sampleList->Translations[k].Value.Y);
					lCurveTZ->KeySet(keySetIndex, lTime, sampleList->Translations[k].Value.Z);
					keySetIndex++;
				}
				lCurveSX->KeyModifyEnd();
				lCurveSY->KeyModifyEnd();
				lCurveSZ->KeyModifyEnd();
				lCurveRX->KeyModifyEnd();
				lCurveRY->KeyModifyEnd();
				lCurveRZ->KeyModifyEnd();
				lCurveTX->KeyModifyEnd();
				lCurveTY->KeyModifyEnd();
				lCurveTZ->KeyModifyEnd();

				if (EulerFilter)
				{
					FbxAnimCurve* lCurve [3];
					lCurve[0] = lCurveRX;
					lCurve[1] = lCurveRY;
					lCurve[2] = lCurveRZ;
					EulerFilter->Reset();
					EulerFilter->SetTestForPath(true);
					EulerFilter->SetQualityTolerance(filterPrecision);
					EulerFilter->Apply((FbxAnimCurve**)lCurve, 3);
				}
			}
		}
	}

	void Fbx::Exporter::ExportMorphs(IImported^ imported, bool oneBlendShape)
	{
		for (int meshIdx = 0; meshIdx < imported->MeshList->Count; meshIdx++)
		{
			ImportedMesh^ meshList = imported->MeshList[meshIdx];

			ImportedMorph^ morph = nullptr;
			for each (ImportedMorph^ m in imported->MorphList)
			{
				if (m->Name == meshList->Name)
				{
					morph = m;
					break;
				}
			}
			if (morph == nullptr)
			{
				continue;
			}

			FbxNode* pBaseNode = NULL;
			for (int nodeIdx = 0; nodeIdx < pMeshNodes->GetCount(); nodeIdx++)
			{
				FbxNode* pMeshNode = pMeshNodes->GetAt(nodeIdx);
				String^ framePath = gcnew String(pMeshNode->GetName());
				FbxNode* rootNode = pMeshNode;
				while ((rootNode = rootNode->GetParent()) != pScene->GetRootNode())
				{
					framePath = gcnew String(rootNode->GetName()) + "/" + framePath;
				}
				if (framePath == meshList->Name)
				{
					pBaseNode = pMeshNode;
					break;
				}
			}
			if (pBaseNode == NULL)
			{
				continue;
			}

			int meshVertexIndex = 0;
			for (int meshObjIdx = 0; meshObjIdx < meshList->SubmeshList->Count; meshObjIdx++)
			{
				List<ImportedVertex^>^ vertList = meshList->SubmeshList[meshObjIdx]->VertexList;
				FbxNode* pBaseMeshNode = pBaseNode->GetChild(meshObjIdx);
				FbxMesh* pBaseMesh = pBaseMeshNode->GetMesh();

				char* pMorphClipName = NULL;
				try
				{
					String^ morphClipName = gcnew String(pBaseMeshNode->GetName()) + "_morph_" + morph->ClipName;
					pMorphClipName = StringToCharArray(morphClipName);
					pBaseMeshNode->SetName(pMorphClipName);
				}
				finally
				{
					Marshal::FreeHGlobal((IntPtr)pMorphClipName);
				}

				FbxBlendShape* lBlendShape;
				if (oneBlendShape)
				{
					String^ morphName;
					int slashPos;
					WITH_MARSHALLED_STRING
					(
						pShapeName,
						(
							slashPos = morph->Name->LastIndexOf('/'),
							morphName = morph->Name->Substring(slashPos + 1),
							morphName + "_BlendShape"
						),
						lBlendShape = FbxBlendShape::Create(pScene, pShapeName);
					);
					pBaseMesh->AddDeformer(lBlendShape);
				}
				List<ImportedMorphKeyframe^>^ keyframes = morph->KeyframeList;
				for (int i = 0; i < keyframes->Count; i++)
				{
					ImportedMorphKeyframe^ keyframe = keyframes[i];

					if (!oneBlendShape)
					{
						WITH_MARSHALLED_STRING
						(
							pShapeName, morph->Name + "_BlendShape",
							lBlendShape = FbxBlendShape::Create(pScene, pShapeName);
						);
						pBaseMesh->AddDeformer(lBlendShape);
					}
					FbxBlendShapeChannel* lBlendShapeChannel = FbxBlendShapeChannel::Create(pScene, "");
					FbxShape* pShape;
					WITH_MARSHALLED_STRING
					(
						pMorphShapeName, keyframe->Name, \
						pShape = FbxShape::Create(pScene, pMorphShapeName);
					);
					lBlendShapeChannel->AddTargetShape(pShape);
					lBlendShape->AddBlendShapeChannel(lBlendShapeChannel);

					pShape->InitControlPoints(vertList->Count);
					FbxVector4* pControlPoints = pShape->GetControlPoints();

					FbxLayer* pLayer = pShape->GetLayer(0);
					if (pLayer == NULL)
					{
						pShape->CreateLayer();
						pLayer = pShape->GetLayer(0);
					}

					for (int j = 0; j < vertList->Count; j++)
					{
						ImportedVertex^ vertex = vertList[j];
						Vector3 coords = vertex->Position;
						pControlPoints[j] = FbxVector4(coords.X, coords.Y, coords.Z);
					}
					List<unsigned short>^ meshIndices = keyframe->MorphedVertexIndices;
					for (int j = 0; j < meshIndices->Count; j++)
					{
						int controlPointIndex = meshIndices[j] - meshVertexIndex;
						if (controlPointIndex >= 0 && controlPointIndex < vertList->Count)
						{
							Vector3 coords = keyframe->VertexList[j]->Position;
							pControlPoints[controlPointIndex] = FbxVector4(coords.X, coords.Y, coords.Z);
						}
					}

					FbxLayerElementVertexColor* pVertexColorLayer;
					WITH_MARSHALLED_STRING
					(
						pColourLayerName, morph->KeyframeList[i]->Name,
						pVertexColorLayer = FbxLayerElementVertexColor::Create(pBaseMesh, pColourLayerName);
					);
					pVertexColorLayer->SetMappingMode(FbxLayerElement::eByControlPoint);
					pVertexColorLayer->SetReferenceMode(FbxLayerElement::eDirect);
					for (int j = 0; j < vertList->Count; j++)
					{
						pVertexColorLayer->GetDirectArray().Add(FbxColor(1, 1, 1));
					}
					for (int j = 0; j < meshIndices->Count; j++)
					{
						int controlPointIndex = meshIndices[j] - meshVertexIndex;
						if (controlPointIndex >= 0 && controlPointIndex < vertList->Count)
						{
							pVertexColorLayer->GetDirectArray().SetAt(controlPointIndex, FbxColor(0, 0, 1));
						}
					}
					pBaseMesh->CreateLayer();
					pLayer = pBaseMesh->GetLayer(pBaseMesh->GetLayerCount() - 1);
					pLayer->SetVertexColors(pVertexColorLayer);
				}
				meshVertexIndex += meshList->SubmeshList[meshObjIdx]->VertexList->Count;
			}
		}
	}
}
