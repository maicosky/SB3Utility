#include <fbxsdk.h>
#include <fbxfilesdk/kfbxio/kfbxiosettings.h>
#include "ODFPluginFBX.h"
#include "ODFPluginFBXMapper.h"

namespace ODFPlugin
{
	void Fbx::Exporter::Export(String^ path, odfParser^ parser, List<odfMesh^>^ meshes, String^ exportFormat, bool allFrames, bool skins, bool _8dot3)
	{
		FileInfo^ file = gcnew FileInfo(path);
		DirectoryInfo^ dir = file->Directory;
		if (!dir->Exists)
		{
			dir->Create();
		}
		String^ currentDir = Directory::GetCurrentDirectory();
		Directory::SetCurrentDirectory(dir->FullName);

		String^ exportPath = _8dot3 ? W32::GetShortPathNameW(Path::GetDirectoryName(path)) + Path::DirectorySeparatorChar + Path::GetFileName(path) : path;
		Exporter^ exporter = gcnew Exporter(exportPath, parser, meshes, exportFormat, allFrames, skins);
		exporter->pExporter->Export(exporter->pScene);

		Directory::SetCurrentDirectory(currentDir);
	}

	Fbx::Exporter::Exporter(String^ path, odfParser^ parser, List<odfMesh^>^ meshes, String^ exportFormat, bool allFrames, bool skins)
	{
		this->parser = parser;
		exportSkins = skins;
		meshIDs = gcnew HashSet<int>();
		for (int i = 0; i < meshes->Count; i++)
		{
			meshIDs->Add((int)meshes[i]->Id);
		}

		frameIDs = nullptr;
		if (!allFrames)
		{
			frameIDs = odf::SearchHierarchy(parser, meshIDs);
		}

		cDest = NULL;
		cFormat = NULL;
		pSdkManager = NULL;
		pScene = NULL;
		pExporter = NULL;
		pMaterials = NULL;
		pTextures = NULL;
		pMeshNodes = NULL;

		pin_ptr<KFbxSdkManager*> pSdkManagerPin = &pSdkManager;
		pin_ptr<KFbxScene*> pScenePin = &pScene;
		Init(pSdkManagerPin, pScenePin);

		cDest = Fbx::StringToCharArray(path);
		cFormat = Fbx::StringToCharArray(exportFormat);
		pExporter = KFbxExporter::Create(pSdkManager, "");
		int lFormatIndex, lFormatCount = pSdkManager->GetIOPluginRegistry()->GetWriterFormatCount();
		for (lFormatIndex = 0; lFormatIndex < lFormatCount; lFormatIndex++)
		{
			KString lDesc = KString(pSdkManager->GetIOPluginRegistry()->GetWriterFormatDescription(lFormatIndex));
			if (lDesc.Find(cFormat) >= 0)
			{
				if (pSdkManager->GetIOPluginRegistry()->WriterIsFBX(lFormatIndex))
				{
					if (lDesc.Find("binary") >= 0)
					{
						break;
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

		if (!pExporter->Initialize(cDest, lFormatIndex, pSdkManager->GetIOSettings()))
		{
			throw gcnew Exception(gcnew String("Failed to initialize KFbxExporter: ") + gcnew String(pExporter->GetLastErrorString()));
		}

		if (parser != nullptr)
		{
			pMaterials = new KArrayTemplate<KFbxSurfacePhong*>();
			pTextures = new KArrayTemplate<KFbxFileTexture*>();
			pMaterials->Reserve(parser->MaterialSection->Count);
			pTextures->Reserve(parser->TextureSection->Count);

			List<odfFrame^>^ meshFrames = gcnew List<odfFrame^>(parser->MeshSection->Count);
			pMeshNodes = new KArrayTemplate<KFbxNode*>();
			ExportFrame(pScene->GetRootNode(), parser->FrameSection->RootFrame, meshFrames, pMeshNodes);
//			KFbxNode* top = pScene->GetRootNode()->GetChild(0);
//			top->LclScaling.Set(KFbxVector4(top->LclScaling.Get()[0], top->LclScaling.Get()[1], -top->LclScaling.Get()[2]));

			SetJoints();

			for (int i = 0; i < meshFrames->Count; i++)
			{
				ExportMesh(pMeshNodes->GetAt(i), meshFrames[i]);
			}

			KFbxDisplayLayer* layer = KFbxDisplayLayer::Create(pScene, "ODF");
			layer->AddMember(pScene->GetRootNode()->GetChild(0));
		}

		pExporter->Export(pScene);
		Report::ReportLog(gcnew String("Finished exporting to ") + path);
	}

	Fbx::Exporter::~Exporter()
	{
		this->!Exporter();
	}

	Fbx::Exporter::!Exporter()
	{
		if (pMeshNodes != NULL)
		{
			delete pMeshNodes;
		}
		if (pMaterials != NULL)
		{
			delete pMaterials;
		}
		if (pTextures != NULL)
		{
			delete pTextures;
		}
		if (pExporter != NULL)
		{
			pExporter->Destroy();
		}
		if (pScene != NULL)
		{
			pScene->Destroy();
		}
		if (pSdkManager != NULL)
		{
			pSdkManager->Destroy();
		}
		if (cFormat != NULL)
		{
			Marshal::FreeHGlobal((IntPtr)cFormat);
		}
		if (cDest != NULL)
		{
			Marshal::FreeHGlobal((IntPtr)cDest);
		}
	}

	void Fbx::Exporter::SetJoints()
	{
		HashSet<int>^ boneIDs = gcnew HashSet<int>();
		if (parser->EnvelopeSection != nullptr)
		{
			for (int i = 0; i < parser->EnvelopeSection->Count; i++)
			{
				odfBoneList^ boneList = parser->EnvelopeSection[i];
				for (int j = 0; j < boneList->Count; j++)
				{
					odfBone^ bone = boneList[j];
					boneIDs->Add((int)bone->FrameId);
				}
			}
		}

		SetJointsNode(pScene->GetRootNode()->GetChild(0), boneIDs);
	}

	void Fbx::Exporter::SetJointsNode(KFbxNode* pNode, HashSet<int>^ boneIDs)
	{
		KFbxProperty idProp = pNode->FindProperty("ID");
		KString id = NULL;
		KFbxGet(idProp, id);
		ObjectID^ nodeID = gcnew ObjectID(gcnew String(id));
		if (boneIDs->Contains((int)nodeID))
		{
			KFbxSkeleton* pJoint = KFbxSkeleton::Create(pSdkManager, "");
			pJoint->SetSkeletonType(KFbxSkeleton::eLIMB_NODE);
			pNode->SetNodeAttribute(pJoint);
		}
		else
		{
			KFbxNull* pNull = KFbxNull::Create(pSdkManager, "");
			if (pNode->GetChildCount() > 0)
			{
				pNull->Look.Set(KFbxNull::eNONE);
			}

			pNode->SetNodeAttribute(pNull);
		}

		for (int i = 0; i < pNode->GetChildCount(); i++)
		{
			SetJointsNode(pNode->GetChild(i), boneIDs);
		}
	}

	void Fbx::Exporter::ExportFrame(KFbxNode* pParentNode, odfFrame^ frame, List<odfFrame^>^ meshFrames, KArrayTemplate<KFbxNode*>* pMeshNodes)
	{
		ObjectID^ frameID = frame->Id;
		if ((frameIDs == nullptr) || frameIDs->Contains((int)frameID))
		{
			KFbxNode* pFrameNode = NULL;
			WITH_MARSHALLED_STRING
			(
				frameName, frame->Name, \
				pFrameNode = KFbxNode::Create(pSdkManager, frameName);
			);
			WITH_MARSHALLED_STRING
			(
				id, frameID->ToString(), \
				KFbxProperty idProp = KFbxProperty::Create(pFrameNode, DTString, "ID");
				idProp.ModifyFlag(FbxPropertyFlags::eUSER, true);
				KFbxSet<fbxString>(idProp, id, false);
			);
/*			WITH_MARSHALLED_STRING
			(
				unknowns, String::Format(CultureInfo::InvariantCulture, "({0:X}, {1:G}, {2:G}, {3:G}, {4:G}, {5:G} / {6:G}, {7:G}, {8:G} ... {9:G}) {10:G}, {11:G}", \
					frame->unknown1, frame->unknown2[0], frame->unknown2[1], frame->unknown2[2], frame->unknown2[3], frame->unknown2[4], frame->unknown2[5], frame->unknown2[6], frame->unknown2[7], frame->unknown3, frame->unknown4, frame->unknown5), \
				KFbxProperty prop = KFbxProperty::Create(pFrameNode, DTString, "Unknowns");
				prop.ModifyFlag(FbxPropertyFlags::eUSER, true);
				KFbxSet<fbxString>(prop, unknowns, false);
			);*/

			Vector3 scale, translate;
			Quaternion rotate;
			frame->Matrix.Decompose(scale, rotate, translate);
			Vector3 rotateVector = Fbx::QuaternionToEuler(rotate);

			pFrameNode->LclScaling.Set(KFbxVector4(scale.X , scale.Y, scale.Z));
			pFrameNode->LclRotation.Set(KFbxVector4(fbxDouble3(rotateVector.X, rotateVector.Y, rotateVector.Z)));
			pFrameNode->LclTranslation.Set(KFbxVector4(translate.X, translate.Y, translate.Z));
			pParentNode->AddChild(pFrameNode);

			if ((int)frame->MeshId != 0 && meshIDs->Contains((int)frame->MeshId))
			{
				meshFrames->Add(frame);
				pMeshNodes->Add(pFrameNode);
			}

			for (int i = 0; i < frame->Count; i++)
			{
				ExportFrame(pFrameNode, frame[i], meshFrames, pMeshNodes);
			}
		}
	}

	KFbxNode* Fbx::Exporter::FindChild(int ID, KFbxNode* pNode)
	{
		KFbxProperty idProp = pNode->FindProperty("ID");
		if (idProp.IsValid())
		{
			KString id = NULL;
			KFbxGet(idProp, id);
			ObjectID^ nodeID = gcnew ObjectID(gcnew String(id));
			if (ID == (int)nodeID)
				return pNode;
		}

		for (int i = 0; i < pNode->GetChildCount(); i++)
		{
			KFbxNode* pFound = FindChild(ID, pNode->GetChild(i));
			if (pFound)
				return pFound;
		}

		return NULL;
	}

	void Fbx::Exporter::ExportMesh(KFbxNode* pFrameNode, odfFrame^ frame)
	{
		odfMesh^ meshList = odf::FindMeshListSome(frame->MeshId, parser->MeshSection);
/*		WITH_MARSHALLED_STRING
		(
			meshID, meshList->id->ToString(), \
			KFbxProperty idProp = KFbxProperty::Create(pFrameNode, DTString, "MeshID");
			idProp.ModifyFlag(FbxPropertyFlags::eUSER, true);
			KFbxSet<fbxString>(idProp, meshID, false);
		);
		WITH_MARSHALLED_STRING
		(
			meshName, meshList->name, \
			KFbxProperty idProp = KFbxProperty::Create(pFrameNode, DTString, "MeshName");
			idProp.ModifyFlag(FbxPropertyFlags::eUSER, true);
			KFbxSet<fbxString>(idProp, meshName, false);
		);
		WITH_MARSHALLED_STRING
		(
			meshInfo, meshList->info, \
			KFbxProperty prop = KFbxProperty::Create(pFrameNode, DTString, "MeshInfo");
			prop.ModifyFlag(FbxPropertyFlags::eUSER, true);
			KFbxSet<fbxString>(prop, meshInfo, false);
		);*/
		String^ frameName = frame->Name;

		bool numberAtEnd = false;
		for (int i = 0; i < meshList->Count; i++)
		{
			String^ meshObjName = meshList[i]->Name;
			int pos = meshObjName->LastIndexOf('_') + 1;
			int number;
			if (pos >= 1 && Int32::TryParse(meshObjName->Substring(pos, meshObjName->Length - pos), number))
			{
				numberAtEnd = true;
				break;
			}
		}

		KArrayTemplate<KFbxNode*>* pBoneNodeList = NULL;
		for (int i = 0; i < meshList->Count; i++)
		{
			KArrayTemplate<KFbxCluster*>* pClusterArray = NULL;
			try
			{
				odfSubmesh^ meshObj = meshList[i];
				KFbxMesh* pMesh = KFbxMesh::Create(pSdkManager, "");

				List<odfFace^>^ faceList = meshObj->FaceList;
				List<odfVertex^>^ vertexList = meshObj->VertexList;

				KFbxLayer* pLayer = pMesh->GetLayer(0);
				if (pLayer == NULL)
				{
					pMesh->CreateLayer();
					pLayer = pMesh->GetLayer(0);
				}

				pMesh->InitControlPoints(vertexList->Count);
				KFbxVector4* pControlPoints = pMesh->GetControlPoints();

				KFbxLayerElementNormal* pLayerElementNormal = KFbxLayerElementNormal::Create(pMesh, "");
				pLayerElementNormal->SetMappingMode(KFbxLayerElement::eBY_CONTROL_POINT);
				pLayerElementNormal->SetReferenceMode(KFbxLayerElement::eDIRECT);
				pLayer->SetNormals(pLayerElementNormal);

				KFbxLayerElementUV* pUVLayer = KFbxLayerElementUV::Create(pMesh, "");
				pUVLayer->SetMappingMode(KFbxLayerElement::eBY_CONTROL_POINT);
				pUVLayer->SetReferenceMode(KFbxLayerElement::eDIRECT);
				pLayer->SetUVs(pUVLayer, KFbxLayerElement::eDIFFUSE_TEXTURES);

				KFbxNode* pMeshNode;
				WITH_MARSHALLED_STRING
				(
					objName, numberAtEnd ? meshObj->Name + "_" + i : meshObj->Name, \
					pMeshNode = KFbxNode::Create(pSdkManager, objName);
				);
				pMeshNode->SetNodeAttribute(pMesh);
/*				WITH_MARSHALLED_STRING
				(
					id, meshObj->Id->ToString(), \
					KFbxProperty idProp = KFbxProperty::Create(pMeshNode, DTString, "ID"); \
					idProp.ModifyFlag(FbxPropertyFlags::eUSER, true); \
					KFbxSet<fbxString>(idProp, id, false);
				);
				WITH_MARSHALLED_STRING
				(
					meshObjInfo, meshObj->info, \
					KFbxProperty prop = KFbxProperty::Create(pMeshNode, DTString, "MeshObjInfo"); \
					prop.ModifyFlag(FbxPropertyFlags::eUSER, true); \
					KFbxSet<fbxString>(prop, meshObjInfo, false);
				);
				for (int j = 0; j < 4; j++)
				{
					ODF::ObjectID^ texID = meshObj->getTextureId(j);
					if ((int)texID != 0)
					{
						WITH_MARSHALLED_STRING
						(
							texIDm, texID->ToString(), \
							WITH_MARSHALLED_STRING \
							( \
								propName, "Texture" + (j + 1) + "ID", \
								KFbxProperty idProp = KFbxProperty::Create(pMeshNode, DTString, propName); \
								idProp.ModifyFlag(FbxPropertyFlags::eUSER, true); \
								KFbxSet<fbxString>(idProp, texIDm, false); \
							);
						);
					}
				}
				WITH_MARSHALLED_STRING
				(
					unknowns, String::Format(CultureInfo::InvariantCulture, "({0:X}, {1:X}) {2:X2} ({3:X}, {4:X}, {5:X4}, {6:G})", \
					meshObj->unknown1, meshObj->unknown2, meshObj->unknown3, meshObj->unknown4, meshObj->unknown5, meshObj->unknown6, Utility::DecryptFloat(meshObj->unknown7)), \
					KFbxProperty prop = KFbxProperty::Create(pMeshNode, DTString, "Unknowns"); \
					prop.ModifyFlag(FbxPropertyFlags::eUSER, true); \
					KFbxSet<fbxString>(prop, unknowns, false);
				);*/
				pFrameNode->AddChild(pMeshNode);

				odfMaterialSection^ pMatSection = parser->MaterialSection;
				odfMaterial^ mat = odf::FindMaterialInfo(meshObj->MaterialId, pMatSection);
				if (mat != nullptr)
				{
					KFbxLayerElementMaterial* pMaterialLayer = KFbxLayerElementMaterial::Create(pMesh, "");
					pMaterialLayer->SetMappingMode(KFbxLayerElement::eALL_SAME);
					pMaterialLayer->SetReferenceMode(KFbxLayerElement::eINDEX_TO_DIRECT);
					pMaterialLayer->GetIndexArray().Add(0);
					pLayer->SetMaterials(pMaterialLayer);

					int foundMat = -1;
					for (int j = 0; j < pMaterials->GetCount(); j++)
					{
						KFbxSurfacePhong* pMatTemp = pMaterials->GetAt(j);
						KFbxProperty idProp = pMatTemp->FindProperty("ID");
						if (idProp.IsValid())
						{
							KString id = NULL;
							KFbxGet(idProp, id);
							ObjectID^ tempID = gcnew ObjectID(gcnew String(id));
							if ((int)tempID == (int)mat->Id)
							{
								foundMat = j;
								break;
							}
						}
					}

					KFbxSurfacePhong* pMat;
					if (foundMat >= 0)
					{
						pMat = pMaterials->GetAt(foundMat);
					}
					else
					{
						KString lShadingName  = "Phong";
						Color4 diffuse = mat->Diffuse;
						Color4 ambient = mat->Ambient;
						Color4 emissive = mat->Emissive;
						Color4 specular = mat->Specular;
						float specularPower = mat->SpecularPower;
						WITH_MARSHALLED_STRING
						(
							pMatName, mat->Name, \
							pMat = KFbxSurfacePhong::Create(pSdkManager, pMatName);
						);

						pMat->Diffuse.Set(fbxDouble3(diffuse.Red, diffuse.Green, diffuse.Blue));
						pMat->DiffuseFactor.Set(fbxDouble1(diffuse.Alpha));
						pMat->Ambient.Set(fbxDouble3(ambient.Red, ambient.Green, ambient.Blue));
						pMat->AmbientFactor.Set(fbxDouble1(ambient.Alpha));
						pMat->Emissive.Set(fbxDouble3(emissive.Red, emissive.Green, emissive.Blue));
						pMat->EmissiveFactor.Set(fbxDouble1(emissive.Alpha));
						pMat->Specular.Set(fbxDouble3(specular.Red, specular.Green, specular.Blue));
						pMat->SpecularFactor.Set(fbxDouble1(specular.Alpha));
						pMat->Shininess.Set(specularPower);
						pMat->ShadingModel.Set(lShadingName);

						WITH_MARSHALLED_STRING
						(
							id, mat->Id->ToString(), \
							KFbxProperty idProp = KFbxProperty::Create(pMat, DTString, "ID"); \
							idProp.ModifyFlag(FbxPropertyFlags::eUSER, true); \
							KFbxSet<fbxString>(idProp, id, false);
						);
/*						WITH_MARSHALLED_STRING
						(
							unknown, String::Format(CultureInfo::InvariantCulture, "{0:G}", mat->unknown1), \
							KFbxProperty prop = KFbxProperty::Create(pMat, DTString, "Unknown"); \
							prop.ModifyFlag(FbxPropertyFlags::eUSER, true); \
							KFbxSet<fbxString>(prop, unknown, false);
						);*/
						pMaterials->Add(pMat);
					}
					pMeshNode->AddMaterial(pMat);

					bool hasTexture = false;
					KFbxLayerElementTexture* pTextureLayerDiffuse = NULL;
					KFbxFileTexture* pTextureDiffuse = ExportTexture(meshObj->TextureIds[0], pTextureLayerDiffuse, pMesh);
					if (pTextureDiffuse != NULL)
					{
						pLayer->SetTextures(KFbxLayerElement::eDIFFUSE_TEXTURES, pTextureLayerDiffuse);
						pMat->Diffuse.ConnectSrcObject(pTextureDiffuse);
						hasTexture = true;
					}

					KFbxLayerElementTexture* pTextureLayerAmbient = NULL;
					KFbxFileTexture* pTextureAmbient = ExportTexture(meshObj->TextureIds[1], pTextureLayerAmbient, pMesh);
					if (pTextureAmbient != NULL)
					{
						pLayer->SetTextures(KFbxLayerElement::eAMBIENT_TEXTURES, pTextureLayerAmbient);
						pMat->Ambient.ConnectSrcObject(pTextureAmbient);
						hasTexture = true;
					}

					KFbxLayerElementTexture* pTextureLayerEmissive = NULL;
					KFbxFileTexture* pTextureEmissive = ExportTexture(meshObj->TextureIds[2], pTextureLayerEmissive, pMesh);
					if (pTextureEmissive != NULL)
					{
						pLayer->SetTextures(KFbxLayerElement::eEMISSIVE_TEXTURES, pTextureLayerEmissive);
						pMat->Emissive.ConnectSrcObject(pTextureEmissive);
						hasTexture = true;
					}

					KFbxLayerElementTexture* pTextureLayerSpecular = NULL;
					KFbxFileTexture* pTextureSpecular = ExportTexture(meshObj->TextureIds[3], pTextureLayerSpecular, pMesh);
					if (pTextureSpecular != NULL)
					{
						pLayer->SetTextures(KFbxLayerElement::eSPECULAR_TEXTURES, pTextureLayerSpecular);
						pMat->Specular.ConnectSrcObject(pTextureSpecular);
						hasTexture = true;
					}

					if (hasTexture)
					{
						pMeshNode->SetShadingMode(KFbxNode::eTEXTURE_SHADING);
					}
				}

				for (int j = 0; j < vertexList->Count; j++)
				{
					odfVertex^ vertex = vertexList[j];
					Vector3^ coords = vertex->Position;
					pControlPoints[j] = KFbxVector4(coords[0], coords[1], coords[2]);
					Vector3^ normal = vertex->Normal;
					pLayerElementNormal->GetDirectArray().Add(KFbxVector4(normal[0], normal[1], normal[2]));
					Vector2^ uv = vertex->UV;
					pUVLayer->GetDirectArray().Add(KFbxVector2(uv[0], -uv[1]));
				}

				for (int j = 0; j < faceList->Count; j++)
				{
					odfFace^ face = faceList[j];
					unsigned short v1 = face->VertexIndices[0];
					unsigned short v2 = face->VertexIndices[1];
					unsigned short v3 = face->VertexIndices[2];
					pMesh->BeginPolygon(false);
					pMesh->AddPolygon(v1);
					pMesh->AddPolygon(v2);
					pMesh->AddPolygon(v3);
					pMesh->EndPolygon();
				}

				odfBoneList^ boneList = odf::FindBoneList(meshObj->Id, parser->EnvelopeSection);
				if (exportSkins && boneList != nullptr && boneList->Count > 0)
				{
					KFbxSkin* pSkin = KFbxSkin::Create(pSdkManager, "");
					for (int j = 0; j < boneList->Count; j++)
					{
						odfBone^ bone = boneList[j];
						if (bone->NumberIndices <= 0)
							continue;
						KFbxNode* pNode = FindChild((int)bone->FrameId, pScene->GetRootNode());
						if (pNode == NULL)
						{
							throw gcnew Exception(gcnew String("Couldn't find frame ") + bone->FrameId->ToString() + gcnew String(" used by the bone"));
						}
						KString lClusterName = pNode->GetNameOnly() + KString("Cluster");
						KFbxCluster* pCluster = KFbxCluster::Create(pSdkManager, lClusterName.Buffer());
						pCluster->SetLink(pNode);
						pCluster->SetLinkMode(KFbxCluster::eTOTAL1);

						Matrix boneMatrix = bone->Matrix;
						KFbxXMatrix lBoneMatrix;
						for (int m = 0; m < 4; m++)
						{
							for (int n = 0; n < 4; n++)
							{
								lBoneMatrix.mData[m][n] = boneMatrix[m, n];
							}
						}

						KFbxXMatrix lMeshMatrix = pScene->GetEvaluator()->GetNodeGlobalTransform(pMeshNode);
						pCluster->SetTransformMatrix(lMeshMatrix);
						pCluster->SetTransformLinkMatrix(lMeshMatrix * lBoneMatrix.Inverse());

						for (int idx = 0; idx < bone->NumberIndices; idx++)
							pCluster->AddControlPointIndex(bone->VertexIndexArray[idx], bone->WeightArray[idx]);

						pSkin->AddCluster(pCluster);
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
			}
		}
	}

	KFbxFileTexture* Fbx::Exporter::ExportTexture(ObjectID^ matTexID, KFbxLayerElementTexture*& pTextureLayer, KFbxMesh* pMesh)
	{
		KFbxFileTexture* pTex = NULL;

		odfTexture^ matTex = odf::FindTextureInfo(matTexID, parser->TextureSection);
		if (matTex != nullptr)
		{
			pTextureLayer = KFbxLayerElementTexture::Create(pMesh, "");
			pTextureLayer->SetMappingMode(KFbxLayerElement::eALL_SAME);
			pTextureLayer->SetReferenceMode(KFbxLayerElement::eDIRECT);

			int foundTex = -1;
/*			for (int i = 0; i < pTextures->GetCount(); i++)
			{
				KFbxTexture* pTexTemp = pTextures->GetAt(i);
				KFbxProperty idProp = pTexTemp->FindProperty("ID");
				if (idProp.IsValid())
				{
					KString id = NULL;
					KFbxGet(idProp, id);
					ODF::ObjectID^ tempID = gcnew ODF::ObjectID(Utility::StringToReversedBytes(gcnew String(id)));
					if ((int)tempID == (int)matTex->id)
					{
						foundTex = i;
						break;
					}
				}
			}*/
			WITH_MARSHALLED_STRING
			(
				pTexName, matTex->Name, \
				for (int i = 0; i < pTextures->GetCount(); i++)
				{
					KFbxFileTexture* pTexTemp = pTextures->GetAt(i);
					if (strcmp(pTexTemp->GetName(), pTexName) == 0)
					{
						foundTex = i;
						break;
					}
				}
			);

			if (foundTex >= 0)
			{
				pTex = pTextures->GetAt(foundTex);
			}
			else
			{
				WITH_MARSHALLED_STRING
				(
					pTexName, matTex->Name, \
					pTex = KFbxFileTexture::Create(pSdkManager, pTexName);
					pTex->SetFileName(pTexName);
				);
/*				WITH_MARSHALLED_STRING
				(
					id, matTex->id->ToString(), \
					KFbxProperty idProp = KFbxProperty::Create(pTex, DTString, "ID"); \
					idProp.ModifyFlag(FbxPropertyFlags::eUSER, true); \
					KFbxSet<fbxString>(idProp, id, false);
				);*/
				pTex->SetTextureUse(KFbxTexture::eSTANDARD);
				pTex->SetMappingType(KFbxTexture::eUV);
				pTex->SetMaterialUse(KFbxFileTexture::eMODEL_MATERIAL);
				pTex->SetSwapUV(false);
				pTex->SetTranslation(0.0, 0.0);
				pTex->SetScale(1.0, 1.0);
				pTex->SetRotation(0.0, 0.0);
				pTextures->Add(pTex);

				String^ texFilePath = Path::GetDirectoryName(parser->ODFPath) + Path::DirectorySeparatorChar + matTex->TextureFile;
				odfTextureFile^ odfTex = gcnew odfTextureFile(matTex->Name, texFilePath);
				odf::ExportTexture(odfTex, Path::GetDirectoryName(gcnew String(pExporter->GetFileName().Buffer())) + Path::DirectorySeparatorChar + Path::GetFileName(matTex->TextureFile->Name));
			}

			pTextureLayer->GetDirectArray().Add(pTex);
		}

		return pTex;
	}
}
