#include <fbxsdk.h>
#include <fbxsdk/fileio/fbxiosettings.h>
#include "SB3UtilityFBX.h"

using namespace System::Reflection;

namespace SB3Utility
{
	Fbx::Importer::Importer(String^ path, bool EulerFilter, float filterPrecision)
	{
		String^ currentDir;

		try
		{
			currentDir = Directory::GetCurrentDirectory();
			Directory::SetCurrentDirectory(Path::GetDirectoryName(path));
			path = Path::GetFileName(path);

			unnamedMeshCount = 0;
			FrameList = gcnew List<ImportedFrame^>();
			MeshList = gcnew List<ImportedMesh^>();
			MaterialList = gcnew List<ImportedMaterial^>();
			TextureList = gcnew List<ImportedTexture^>();
			AnimationList = gcnew List<ImportedAnimation^>();
			MorphList = gcnew List<ImportedMorph^>();

			cPath = NULL;
			pSdkManager = NULL;
			pScene = NULL;
			pImporter = NULL;
			pMaterials = NULL;
			pTextures = NULL;

			pin_ptr<FbxManager*> pSdkManagerPin = &pSdkManager;
			pin_ptr<FbxScene*> pScenePin = &pScene;
			Init(pSdkManagerPin, pScenePin);

			cPath = Fbx::StringToCharArray(path);
			pImporter = FbxImporter::Create(pSdkManager, "");

			IOS_REF.SetBoolProp(IMP_FBX_MATERIAL, true);
			IOS_REF.SetBoolProp(IMP_FBX_TEXTURE, true);
			IOS_REF.SetBoolProp(IMP_FBX_LINK, true);
			IOS_REF.SetBoolProp(IMP_FBX_SHAPE, true);
			IOS_REF.SetBoolProp(IMP_FBX_GOBO, true);
			IOS_REF.SetBoolProp(IMP_FBX_ANIMATION, true);
			IOS_REF.SetBoolProp(IMP_FBX_GLOBAL_SETTINGS, true);

			if (!pImporter->Initialize(cPath, -1, pSdkManager->GetIOSettings()))
			{
				throw gcnew Exception(gcnew String("Failed to initialize FbxImporter: ") + gcnew String(pImporter->GetStatus().GetErrorString()));
			}

			pImporter->Import(pScene);
			pMaterials = new FbxArray<FbxSurfacePhong*>();
			pTextures = new FbxArray<FbxTexture*>();

			FbxNode* pRootNode = pScene->GetRootNode();
			if (pRootNode != NULL)
			{
				ImportNode(nullptr, pRootNode);
			}

			this->EulerFilter = EulerFilter;
			this->filterPrecision = filterPrecision;
			ImportAnimation();
		}
		finally
		{
			if (pMaterials != NULL)
			{
				delete pMaterials;
			}
			if (pTextures != NULL)
			{
				delete pTextures;
			}
			if (pImporter != NULL)
			{
				pImporter->Destroy();
			}
			if (pScene != NULL)
			{
				pScene->Destroy();
			}
			if (pSdkManager != NULL)
			{
				pSdkManager->Destroy();
			}
			if (cPath != NULL)
			{
				Marshal::FreeHGlobal((IntPtr)cPath);
			}

			Directory::SetCurrentDirectory(currentDir);
		}
	}

	void Fbx::Importer::ImportNode(ImportedFrame^ parent, FbxNode* pNode)
	{
		FbxArray<FbxNode*>* pMeshArray = NULL;
		try
		{
			pMeshArray = new FbxArray<FbxNode*>();
			bool hasShapes = false;

			for (int i = 0; i < pNode->GetChildCount(); i++)
			{
				FbxNode* pNodeChild = pNode->GetChild(i);
				if (pNodeChild->GetNodeAttribute() == NULL)
				{
					ImportedFrame^ frame = ImportFrame(parent, pNodeChild);
					ImportNode(frame, pNodeChild);
				}
				else
				{
					FbxNodeAttribute::EType lAttributeType = pNodeChild->GetNodeAttribute()->GetAttributeType();

					switch (lAttributeType)
					{
						case FbxNodeAttribute::eNull:
						case FbxNodeAttribute::eSkeleton:
							{
								ImportedFrame^ frame = ImportFrame(parent, pNodeChild);
								ImportNode(frame, pNodeChild);
							}
							break;

						case FbxNodeAttribute::eMesh:
							if (pNodeChild->GetMesh()->GetShapeCount() > 0)
							{
								hasShapes = true;
							}
							pMeshArray->Add(pNodeChild);
							break;

						default:
							FbxString str = FbxString(lAttributeType);
							Report::ReportLog(gcnew String("Warning: ") + gcnew String(pNodeChild->GetName()) + gcnew String(" has unsupported node attribute type ") + gcnew String(str.Buffer()));
							break;
					}
				}
			}

			if (hasShapes)
			{
				ImportMorph(pMeshArray);
			}
			else
			{
				ImportMesh(parent, pMeshArray);
			}
		}
		finally
		{
			if (pMeshArray != NULL)
			{
				delete pMeshArray;
			}
		}
	}

	ImportedFrame^ Fbx::Importer::ImportFrame(ImportedFrame^ parent, FbxNode* pNode)
	{
		ImportedFrame^ frame = gcnew ImportedFrame();
		frame->InitChildren(pNode->GetChildCount());
		frame->Name = gcnew String(pNode->GetName());

		if (parent == nullptr)
		{
			FrameList->Add(frame);
		}
		else
		{
			parent->AddChild(frame);
		}

		FbxAMatrix lNodeMatrix = pScene->GetEvaluator()->GetNodeLocalTransform(pNode);
		Matrix matrix;
		for (int m = 0; m < 4; m++)
		{
			for (int n = 0; n < 4; n++)
			{
				matrix[m, n] = (float)lNodeMatrix[m][n];
			}
		}
		frame->Matrix = matrix;

		return frame;
	}

	void Fbx::Importer::ImportMesh(ImportedFrame^ parent, FbxArray<FbxNode*>* pMeshArray)
	{
		if (pMeshArray->GetCount() > 0)
		{
			ImportedMesh^ meshList = gcnew ImportedMesh();
			meshList->SubmeshList = gcnew List<ImportedSubmesh^>();
			MeshList->Add(meshList);

			if (parent == nullptr)
			{
				meshList->Name = gcnew String("no_name") + unnamedMeshCount;
				unnamedMeshCount++;
			}
			else
			{
				meshList->Name = parent->Name;
			}

			bool skinned = false;
			for (int i = 0; i < pMeshArray->GetCount(); i++)
			{
				FbxNode* pMeshNode = pMeshArray->GetAt(i);
				FbxMesh* pMesh = pMeshNode->GetMesh();
				if (pMesh->GetDeformerCount(FbxDeformer::eSkin) > 0)
				{
					skinned = true;
					break;
				}
			}

			SortedDictionary<String^, int>^ boneDic = gcnew SortedDictionary<String^, int>();
			List<ImportedBone^>^ boneList = gcnew List<ImportedBone^>(255);
			for (int i = 0; i < pMeshArray->GetCount(); i++)
			{
				ImportedSubmesh^ submesh = gcnew ImportedSubmesh();
				meshList->SubmeshList->Add(submesh);
				submesh->Index = i;

				FbxNode* pMeshNode = pMeshArray->GetAt(i);
				FbxMesh* pMesh = pMeshNode->GetMesh();

				String^ submeshName = gcnew String(pMeshNode->GetName());
				int idx = submeshName->LastIndexOf('_');
				if (idx >= 0)
				{
					idx++;
					int submeshIdx;
					if (Int32::TryParse(submeshName->Substring(idx, submeshName->Length - idx), submeshIdx))
					{
						submesh->Index = submeshIdx;
					}
				}

				FbxLayer* pLayerNormal = pMesh->GetLayer(0, FbxLayerElement::eNormal);
				FbxLayerElementNormal* pLayerElementNormal = NULL;
				if (pLayerNormal != NULL)
				{
					pLayerElementNormal = pLayerNormal->GetNormals();
				}

				FbxLayer* pLayerUV = pMesh->GetLayer(0, FbxLayerElement::eUV);
				FbxLayerElementUV* pLayerElementUV = NULL;
				if (pLayerUV != NULL)
				{
					pLayerElementUV = pLayerUV->GetUVs();
				}

				int numVerts = pMesh->GetControlPointsCount();
				array<List<Vertex^>^>^ vertMap = gcnew array<List<Vertex^>^>(numVerts);
				for (int j = 0; j < numVerts; j++)
				{
					vertMap[j] = gcnew List<Vertex^>();
				}

				int vertCount = 0;
				int numFaces = pMesh->GetPolygonCount();
				array<array<Vertex^>^>^ faceMap = gcnew array<array<Vertex^>^>(numFaces);
				for (int j = 0; j < numFaces; j++)
				{
					faceMap[j] = gcnew array<Vertex^>(3);

					int polySize = pMesh->GetPolygonSize(j);
					if (polySize != 3)
					{
						throw gcnew Exception(gcnew String("Mesh ") + gcnew String(pMeshNode->GetName()) + " needs to be triangulated");
					}
					int polyVertIdxStart = pMesh->GetPolygonVertexIndex(j);
					for (int k = 0; k < polySize; k++)
					{
						int controlPointIdx = pMesh->GetPolygonVertices()[polyVertIdxStart + k];
						Vertex^ vert = gcnew Vertex();

						FbxVector4 pos = pMesh->GetControlPointAt(controlPointIdx);
						vert->position = gcnew array<float>(3) { (float)pos[0], (float)pos[1], (float)pos[2] };

						if (pLayerElementNormal != NULL)
						{
							FbxVector4 norm;
							GetVector(pLayerElementNormal, norm, controlPointIdx, vertCount);
							vert->normal = gcnew array<float>(3) { (float)norm[0], (float)norm[1], (float)norm[2] };
						}

						if (pLayerElementUV != NULL)
						{
							FbxVector2 uv;
							GetVector(pLayerElementUV, uv, controlPointIdx, vertCount);
							vert->uv = gcnew array<float>(2) { (float)uv[0], -(float)uv[1] };
						}

						List<Vertex^>^ vertMapList = vertMap[controlPointIdx];
						Vertex^ foundVert = nullptr;
						Vertex^ sameUV = nullptr;
						for (int m = 0; m < vertMapList->Count; m++)
						{
							if (sameUV == nullptr && vertMapList[m]->uv[0] == vert->uv[0] && vertMapList[m]->uv[1] == vert->uv[1])
							{
								sameUV = vertMapList[m];
							}
							if (vertMapList[m]->Equals(vert))
							{
								foundVert = sameUV == nullptr ? vertMapList[m] : sameUV;
								break;
							}
						}

						if (foundVert == nullptr)
						{
							bool newUV = true;
							for each (Vertex^ v in vertMapList)
							{
								if (v->uv[0] == vert->uv[0] && v->uv[1] == vert->uv[1])
								{
									newUV = false;
									break;
								}
							}
							if (newUV)
							{
								vert->index = -1;
								vertMapList->Insert(0, vert);
								foundVert = vert;
							}
							else
							{
								foundVert = sameUV;
								vertMapList->Add(vert);
							}
						}
						faceMap[j][k] = foundVert;

						vertCount++;
					}
				}

				for (int j = 0; j < vertMap->Length; j++)
				{
					List<Vertex^>^ vertMapList = vertMap[j];
					int numNormals = vertMapList->Count;
					if (numNormals > 0)
					{
						array<float>^ normal = gcnew array<float>(3);
						for (int k = 0; k < vertMapList->Count; k++)
						{
							Vertex^ v = vertMapList[k];
							array<float>^ addNormal = v->normal;
							normal[0] += addNormal[0];
							normal[1] += addNormal[1];
							normal[2] += addNormal[2];
							if (v->index == 0)
							{
								vertMapList->RemoveAt(k);
								k--;
							}
						}
						normal[0] /= numNormals;
						normal[1] /= numNormals;
						normal[2] /= numNormals;
						for each (Vertex^ v in vertMapList)
						{
							v->normal[0] = normal[0];
							v->normal[1] = normal[1];
							v->normal[2] = normal[2];
						}
					}
				}

				FbxSkin* pSkin = (FbxSkin*)pMesh->GetDeformer(0, FbxDeformer::eSkin);
				if (pSkin != NULL)
				{
					if (pMesh->GetDeformerCount(FbxDeformer::eSkin) > 1)
					{
						Report::ReportLog(gcnew String("Warning: Mesh ") + gcnew String(pMeshNode->GetName()) + " has more than 1 skin. Only the first will be used");
					}

					int numClusters = pSkin->GetClusterCount();
					for (int j = 0; j < numClusters; j++)
					{
						FbxCluster* pCluster = pSkin->GetCluster(j);
						if (pCluster->GetLinkMode() == FbxCluster::eAdditive)
						{
							throw gcnew Exception(gcnew String("Mesh ") + gcnew String(pMeshNode->GetName()) + " has additive weights and aren't supported");
						}

#if 1
						FbxAMatrix lMatrix;
						pCluster->GetTransformLinkMatrix(lMatrix);
						lMatrix = lMatrix.Inverse();
#else
						FbxAMatrix lMatrix, lMeshMatrix;
						pCluster->GetTransformMatrix(lMeshMatrix);
						/*FbxAMatrix geomMatrix = pMeshNode->GetScene()->GetEvaluator()->GetNodeLocalTransform(pMeshNode);
						lMeshMatrix *= geomMatrix;*/
						pCluster->GetTransformLinkMatrix(lMatrix);
						lMatrix = (lMeshMatrix.Inverse() * lMatrix).Inverse();
#endif
						Matrix boneMatrix;
						for (int m = 0; m < 4; m++)
						{
							for (int n = 0; n < 4; n++)
							{
								boneMatrix[m, n] = (float)lMatrix.mData[m][n];
							}
						}

						FbxNode* pLinkNode = pCluster->GetLink();
						String^ boneName = gcnew String(pLinkNode->GetName());
						int boneIdx;
						if (!boneDic->TryGetValue(boneName, boneIdx))
						{
							ImportedBone^ boneInfo = gcnew ImportedBone();
							boneList->Add(boneInfo);
							boneInfo->Name = boneName;
							boneInfo->Matrix = boneMatrix;

							boneIdx = boneDic->Count;
							boneDic->Add(boneName, boneIdx);
						}

						int* lIndices = pCluster->GetControlPointIndices();
						double* lWeights = pCluster->GetControlPointWeights();
						int numIndices = pCluster->GetControlPointIndicesCount();
						for (int k = 0; k < numIndices; k++)
						{
							List<Vertex^>^ vert = vertMap[lIndices[k]];
							for (int m = 0; m < vert->Count; m++)
							{
								vert[m]->boneIndices->Add(boneIdx);
								vert[m]->weights->Add((float)lWeights[k]);
							}
						}
					}
				}

				int vertIdx = 0;
				List<ImportedVertex^>^ vertList = gcnew List<ImportedVertex^>(vertMap->Length);
				submesh->VertexList = vertList;
				for (int j = 0; j < vertMap->Length; j++)
				{
					for (int k = 0; k < vertMap[j]->Count; k++)
					{
						Vertex^ vert = vertMap[j][k];
						vert->index = vertIdx;

						ImportedVertex^ vertInfo = gcnew ImportedVertex();
						vertList->Add(vertInfo);
						vertInfo->Position = Vector3(vert->position[0], vert->position[1], vert->position[2]);

						if (skinned)
						{
							int numBones = vert->boneIndices->Count;
							if (numBones > 4)
							{
								throw gcnew Exception(gcnew String("Mesh ") + gcnew String(pMeshNode->GetName()) + " has vertices with more than 4 weights");
							}

							array<Byte>^ boneIndices = gcnew array<Byte>(4);
							array<float>^ weights4 = gcnew array<float>(4);
							float weightSum = 0;
							for (int m = 0; m < numBones; m++)
							{
								boneIndices[m] = vert->boneIndices[m];
								weightSum += vert->weights[m];
							}
							for (int m = 0; m < numBones; m++)
							{
								weights4[m] = vert->weights[m] / weightSum;
							}

							for (int m = numBones; m < 4; m++)
							{
								boneIndices[m] = 0xFF;
							}

							vertInfo->BoneIndices = boneIndices;
							vertInfo->Weights = weights4;
						}
						else
						{
							vertInfo->BoneIndices = gcnew array<Byte>(4);
							vertInfo->Weights = gcnew array<float>(4);
						}

						vertInfo->Normal = Vector3(vert->normal[0], vert->normal[1], vert->normal[2]);
						vertInfo->UV = gcnew array<float>(2) { vert->uv[0], vert->uv[1] };

						vertIdx++;
					}
				}

				List<ImportedFace^>^ faceList = gcnew List<ImportedFace^>(numFaces);
				submesh->FaceList = faceList;
				for (int j = 0; j < numFaces; j++)
				{
					ImportedFace^ face = gcnew ImportedFace();
					faceList->Add(face);
					face->VertexIndices = gcnew array<int>(3);
					face->VertexIndices[0] = faceMap[j][0]->index;
					face->VertexIndices[1] = faceMap[j][1]->index;
					face->VertexIndices[2] = faceMap[j][2]->index;
				}

				ImportedMaterial^ matInfo = ImportMaterial(pMesh);
				if (matInfo != nullptr)
				{
					submesh->Material = matInfo->Name;
				}
			}

			boneList->TrimExcess();
			meshList->BoneList = boneList;
		}
	}

	ImportedMaterial^ Fbx::Importer::ImportMaterial(FbxMesh* pMesh)
	{
		ImportedMaterial^ matInfo = nullptr;

		FbxLayer* pLayerMaterial = pMesh->GetLayer(0, FbxLayerElement::eMaterial);
		if (pLayerMaterial != NULL)
		{
			FbxLayerElementMaterial* pLayerElementMaterial = pLayerMaterial->GetMaterials();
			if (pLayerElementMaterial != NULL)
			{
				FbxSurfaceMaterial* pMaterial = NULL;
				switch (pLayerElementMaterial->GetReferenceMode())
				{
				case FbxLayerElement::eDirect:
					pMaterial = pMesh->GetNode()->GetMaterial(0);
					break;

				case FbxLayerElement::eIndexToDirect:
					pMaterial = pMesh->GetNode()->GetMaterial(pLayerElementMaterial->GetIndexArray().GetAt(0));
					break;

				default:
					{
						int mode = (int)pLayerElementMaterial->GetReferenceMode();
						Report::ReportLog(gcnew String("Warning: Material ") + gcnew String(pMaterial->GetName()) + " has unsupported reference mode " + mode + " and will be skipped");
					}
					break;
				}

				if (pMaterial != NULL)
				{
					if (pMaterial->GetClassId().Is(FbxSurfacePhong::ClassId))
					{
						FbxSurfacePhong* pPhong = (FbxSurfacePhong*)pMaterial;
						int matIdx = pMaterials->Find(pPhong);
						if (matIdx >= 0)
						{
							matInfo = MaterialList[matIdx];
						}
						else
						{
							matInfo = gcnew ImportedMaterial();
							matInfo->Name = gcnew String(pPhong->GetName());
							
							FbxDouble3 lDiffuse = pPhong->Diffuse.Get();
							FbxDouble lDiffuseFactor = pPhong->DiffuseFactor.Get();
							matInfo->Diffuse = Color4((float)lDiffuseFactor, (float)lDiffuse[0], (float)lDiffuse[1], (float)lDiffuse[2]);

							FbxDouble3 lAmbient = pPhong->Ambient.Get();
							FbxDouble lAmbientFactor = pPhong->AmbientFactor.Get();
							matInfo->Ambient = Color4((float)lAmbientFactor, (float)lAmbient[0], (float)lAmbient[1], (float)lAmbient[2]);

							FbxDouble3 lEmissive = pPhong->Emissive.Get();
							FbxDouble lEmissiveFactor = pPhong->EmissiveFactor.Get();
							matInfo->Emissive = Color4((float)lEmissiveFactor, (float)lEmissive[0], (float)lEmissive[1], (float)lEmissive[2]);

							FbxDouble3 lSpecular = pPhong->Specular.Get();
							FbxDouble lSpecularFactor = pPhong->SpecularFactor.Get();
							matInfo->Specular = Color4((float)lSpecularFactor, (float)lSpecular[0], (float)lSpecular[1], (float)lSpecular[2]);
							matInfo->Power = (float)pPhong->Shininess.Get();

							array<String^>^ texNames = gcnew array<String^>(4);
							texNames[0] = ImportTexture(pPhong->Diffuse.GetSrcObject<FbxFileTexture>());
							texNames[1] = ImportTexture(pPhong->Ambient.GetSrcObject<FbxFileTexture>());
							texNames[2] = ImportTexture(pPhong->Emissive.GetSrcObject<FbxFileTexture>());
							texNames[3] = ImportTexture(pPhong->Specular.GetSrcObject<FbxFileTexture>());
							matInfo->Textures = texNames;

							pMaterials->Add(pPhong);
							MaterialList->Add(matInfo);
						}
					}
					else
					{
						Report::ReportLog(gcnew String("Warning: Material ") + gcnew String(pMaterial->GetName()) + " isn't a Phong material and will be skipped");
					}
				}
			}
		}

		return matInfo;
	}

	String^ Fbx::Importer::ImportTexture(FbxFileTexture* pTexture)
	{
		using namespace System::IO;

		String^ texName = String::Empty;

		if (pTexture != NULL)
		{
			int pTexIdx = pTextures->Find(pTexture);
			if (pTexIdx < 0)
			{
				pTextures->Add(pTexture);

				texName = Path::GetFileName(gcnew String(pTexture->GetName()));
				String^ texPath = Path::GetDirectoryName(gcnew String(cPath)) + Path::DirectorySeparatorChar + texName;
				if (!File::Exists(texPath))
				{
					texName = Path::GetFileName(gcnew String(pTexture->GetFileName()));
					texPath = gcnew String(pTexture->GetFileName());
				}
				try
				{
					ImportedTexture^ tex = gcnew ImportedTexture(texPath);
					TextureList->Add(tex);
				}
				catch (Exception^)
				{
					Report::ReportLog("Import of texture " + texPath + " failed.");
					texName = String::Empty;
				}
			}
			else
			{
				String^ name = gcnew String(pTexture->GetName());
				if (ImportedHelpers::FindTexture(name, TextureList) != nullptr)
				{
					texName = Path::GetFileName(name);
				}
			}
		}

		return texName;
	}

	void Fbx::Importer::ImportAnimation()
	{
		if (EulerFilter)
		{
			lFilter = new FbxAnimCurveFilterUnroll();
		}

		for (int i = 0; i < pScene->GetSrcObjectCount<FbxAnimStack>(); i++)
		{
			FbxAnimStack* pAnimStack = FbxCast<FbxAnimStack>(pScene->GetSrcObject<FbxAnimStack>(i));

			int numLayers = pAnimStack->GetMemberCount<FbxAnimLayer>();
			if (numLayers > 1)
			{
				Report::ReportLog(gcnew String("Warning: Only the first layer of animation ") + gcnew String(pAnimStack->GetName()) + " will be imported");
			}
			if (numLayers > 0)
			{
				Type^ animType = GetAnimationType(pAnimStack->GetMember<FbxAnimLayer>(0), pScene->GetRootNode());
				ConstructorInfo^ ctor = animType->GetConstructor(Type::EmptyTypes);
				if (animType == ImportedKeyframedAnimation::typeid)
				{
					ImportedKeyframedAnimation^ wsAnimation = (ImportedKeyframedAnimation^)ctor->Invoke(Type::EmptyTypes);
					wsAnimation->TrackList = gcnew List<ImportedAnimationKeyframedTrack^>(pScene->GetNodeCount());
					ImportAnimation(pAnimStack->GetMember<FbxAnimLayer>(0), pScene->GetRootNode(), wsAnimation);
					if (wsAnimation->TrackList->Count > 0)
					{
						AnimationList->Add(wsAnimation);
					}
				}
				else
				{
					ImportedSampledAnimation^ wsAnimation = (ImportedSampledAnimation^)ctor->Invoke(Type::EmptyTypes);
					wsAnimation->TrackList = gcnew List<ImportedAnimationSampledTrack^>(pScene->GetNodeCount());
					ImportAnimation(pAnimStack->GetMember<FbxAnimLayer>(0), pScene->GetRootNode(), wsAnimation);
					if (wsAnimation->TrackList->Count > 0)
					{
						AnimationList->Add(wsAnimation);
					}
				}
			}
		}
	}

	Type^ Fbx::Importer::GetAnimationType(FbxAnimLayer* pAnimLayer, FbxNode* pNode)
	{
		FbxAnimCurve* pAnimCurveTX = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveTY = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveTZ = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);
		FbxAnimCurve* pAnimCurveRX = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveRY = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveRZ = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);
		FbxAnimCurve* pAnimCurveSX = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveSY = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveSZ = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);

		if ((pAnimCurveTX != NULL) && (pAnimCurveTY != NULL) && (pAnimCurveTZ != NULL) &&
			(pAnimCurveRX != NULL) && (pAnimCurveRY != NULL) && (pAnimCurveRZ != NULL) &&
			(pAnimCurveSX != NULL) && (pAnimCurveSY != NULL) && (pAnimCurveSZ != NULL))
		{
			if (pAnimCurveSX->KeyGetCount() != pAnimCurveSY->KeyGetCount() || pAnimCurveSX->KeyGetCount() != pAnimCurveSZ->KeyGetCount())
			{
				throw gcnew Exception(gcnew String(pNode->GetName()) + "'s scaling needs to be resampled. It needs the same number of keys for X, Y and Z.");
			}
			if (pAnimCurveRX->KeyGetCount() != pAnimCurveRY->KeyGetCount() || pAnimCurveRX->KeyGetCount() != pAnimCurveRZ->KeyGetCount())
			{
				throw gcnew Exception(gcnew String(pNode->GetName()) + "'s rotation needs to be resampled. It needs the same number of keys for X, Y and Z.");
			}
			if (pAnimCurveTX->KeyGetCount() != pAnimCurveTY->KeyGetCount() || pAnimCurveTX->KeyGetCount() != pAnimCurveTZ->KeyGetCount())
			{
				throw gcnew Exception(gcnew String(pNode->GetName()) + "'s translation needs to be resampled. It needs the same number of keys for X, Y and Z.");
			}

			if (pAnimCurveSX->KeyGetCount() != pAnimCurveRX->KeyGetCount() || pAnimCurveSX->KeyGetCount() != pAnimCurveTX->KeyGetCount())
			{
				return ImportedSampledAnimation::typeid;
			}
		}

		for (int i = 0; i < pNode->GetChildCount(); i++)
		{
			Type^ animType = GetAnimationType(pAnimLayer, pNode->GetChild(i));
			if (animType != ImportedKeyframedAnimation::typeid)
			{
				return animType;
			}
		}

		return ImportedKeyframedAnimation::typeid;
	}

	void Fbx::Importer::ImportAnimation(FbxAnimLayer* pAnimLayer, FbxNode* pNode, ImportedKeyframedAnimation^ wsAnimation)
	{
		FbxAnimCurve* pAnimCurveTX = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveTY = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveTZ = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);
		FbxAnimCurve* pAnimCurveRX = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveRY = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveRZ = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);
		FbxAnimCurve* pAnimCurveSX = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveSY = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveSZ = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);

		if ((pAnimCurveTX != NULL) && (pAnimCurveTY != NULL) && (pAnimCurveTZ != NULL) &&
			(pAnimCurveRX != NULL) && (pAnimCurveRY != NULL) && (pAnimCurveRZ != NULL) &&
			(pAnimCurveSX != NULL) && (pAnimCurveSY != NULL) && (pAnimCurveSZ != NULL))
		{
			if (EulerFilter)
			{
				FbxAnimCurve* lCurve [3];
				lCurve[0] = pAnimCurveRX;
				lCurve[1] = pAnimCurveRY;
				lCurve[2] = pAnimCurveRZ;
				lFilter->Reset();
				lFilter->SetTestForPath(true);
				lFilter->SetQualityTolerance(filterPrecision);
				lFilter->Apply((FbxAnimCurve**)lCurve, 3);
			}

			int numKeys = pAnimCurveSX->KeyGetCount();
			FbxTime FbxTime = pAnimCurveSX->KeyGetTime(numKeys - 1);
			int keyIndex = (int)Math::Round((double)(FbxTime.GetSecondDouble() * 24.0), 0);
			array<ImportedAnimationKeyframe^>^ keyArray = gcnew array<ImportedAnimationKeyframe^>(keyIndex + 1);
			for (int i = 0; i < numKeys; i++)
			{
				FbxTime = pAnimCurveSX->KeyGetTime(i);
				keyIndex = (int)Math::Round((double)(FbxTime.GetSecondDouble() * 24.0), 0);
				keyArray[keyIndex] = gcnew ImportedAnimationKeyframe();
				keyArray[keyIndex]->Scaling = Vector3(pAnimCurveSX->KeyGetValue(i), pAnimCurveSY->KeyGetValue(i), pAnimCurveSZ->KeyGetValue(i));
				Vector3 rotation = Vector3(pAnimCurveRX->KeyGetValue(i), pAnimCurveRY->KeyGetValue(i), pAnimCurveRZ->KeyGetValue(i));
				keyArray[keyIndex]->Rotation = Fbx::EulerToQuaternion(rotation);
				keyArray[keyIndex]->Translation = Vector3(pAnimCurveTX->KeyGetValue(i), pAnimCurveTY->KeyGetValue(i), pAnimCurveTZ->KeyGetValue(i));
			}

			ImportedAnimationKeyframedTrack^ track = gcnew ImportedAnimationKeyframedTrack();
			wsAnimation->TrackList->Add(track);
			track->Name = gcnew String(pNode->GetName());
			track->Keyframes = keyArray;
		}

		for (int i = 0; i < pNode->GetChildCount(); i++)
		{
			ImportAnimation(pAnimLayer, pNode->GetChild(i), wsAnimation);
		}
	}

	void Fbx::Importer::ImportAnimation(FbxAnimLayer* pAnimLayer, FbxNode* pNode, ImportedSampledAnimation^ wsAnimation)
	{
		FbxAnimCurve* pAnimCurveTX = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveTY = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveTZ = pNode->LclTranslation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);
		FbxAnimCurve* pAnimCurveRX = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveRY = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveRZ = pNode->LclRotation.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);
		FbxAnimCurve* pAnimCurveSX = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_X);
		FbxAnimCurve* pAnimCurveSY = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Y);
		FbxAnimCurve* pAnimCurveSZ = pNode->LclScaling.GetCurve(pAnimLayer, FBXSDK_CURVENODE_COMPONENT_Z);

		if ((pAnimCurveTX != NULL) && (pAnimCurveTY != NULL) && (pAnimCurveTZ != NULL) &&
			(pAnimCurveRX != NULL) && (pAnimCurveRY != NULL) && (pAnimCurveRZ != NULL) &&
			(pAnimCurveSX != NULL) && (pAnimCurveSY != NULL) && (pAnimCurveSZ != NULL))
		{
			if (EulerFilter)
			{
				FbxAnimCurve* lCurve [3];
				lCurve[0] = pAnimCurveRX;
				lCurve[1] = pAnimCurveRY;
				lCurve[2] = pAnimCurveRZ;
				lFilter->Reset();
				lFilter->SetTestForPath(true);
				lFilter->SetQualityTolerance(filterPrecision);
				lFilter->Apply((FbxAnimCurve**)lCurve, 3);
			}

			int numKeys = pAnimCurveSX->KeyGetCount();
			FbxTime FbxTime = pAnimCurveSX->KeyGetTime(numKeys - 1);
			int keyIndex = (int)Math::Round((double)(FbxTime.GetSecondDouble() * 24.0), 0);
			array<Nullable<Vector3>>^ scalings = gcnew array<Nullable<Vector3>>(keyIndex + 1);
			for (int i = 0; i < numKeys; i++)
			{
				FbxTime = pAnimCurveSX->KeyGetTime(i);
				keyIndex = (int)Math::Round((double)(FbxTime.GetSecondDouble() * 24.0), 0);
				scalings[keyIndex] = Vector3(pAnimCurveSX->KeyGetValue(i), pAnimCurveSY->KeyGetValue(i), pAnimCurveSZ->KeyGetValue(i));
			}

			numKeys = pAnimCurveRX->KeyGetCount();
			FbxTime = pAnimCurveRX->KeyGetTime(numKeys - 1);
			keyIndex = (int)Math::Round((double)(FbxTime.GetSecondDouble() * 24.0), 0);
			array<Nullable<Quaternion>>^ rotations = gcnew array<Nullable<Quaternion>>(keyIndex + 1);
			for (int i = 0; i < numKeys; i++)
			{
				FbxTime = pAnimCurveRX->KeyGetTime(i);
				keyIndex = (int)Math::Round((double)(FbxTime.GetSecondDouble() * 24.0), 0);
				Vector3 rotation = Vector3(pAnimCurveRX->KeyGetValue(i), pAnimCurveRY->KeyGetValue(i), pAnimCurveRZ->KeyGetValue(i));
				rotations[keyIndex] = Fbx::EulerToQuaternion(rotation);
			}

			numKeys = pAnimCurveTX->KeyGetCount();
			FbxTime = pAnimCurveTX->KeyGetTime(numKeys - 1);
			keyIndex = (int)Math::Round((double)(FbxTime.GetSecondDouble() * 24.0), 0);
			array<Nullable<Vector3>>^ translations = gcnew array<Nullable<Vector3>>(keyIndex + 1);
			for (int i = 0; i < numKeys; i++)
			{
				FbxTime = pAnimCurveTX->KeyGetTime(i);
				keyIndex = (int)Math::Round((double)(FbxTime.GetSecondDouble() * 24.0), 0);
				translations[keyIndex] = Vector3(pAnimCurveTX->KeyGetValue(i), pAnimCurveTY->KeyGetValue(i), pAnimCurveTZ->KeyGetValue(i));
			}

			ImportedAnimationSampledTrack^ track = gcnew ImportedAnimationSampledTrack();
			wsAnimation->TrackList->Add(track);
			track->Name = gcnew String(pNode->GetName());
			track->Scalings = scalings;
			track->Rotations = rotations;
			track->Translations = translations;
		}

		for (int i = 0; i < pNode->GetChildCount(); i++)
		{
			ImportAnimation(pAnimLayer, pNode->GetChild(i), wsAnimation);
		}
	}

	Fbx::Importer::Vertex::Vertex()
	{
		position = gcnew array<float>(3);
		normal = gcnew array<float>(3);
		uv = gcnew array<float>(2);
		boneIndices = gcnew List<Byte>(4);
		weights = gcnew List<float>(4);
	}

	bool Fbx::Importer::Vertex::Equals(Vertex^ vertex)
	{
		bool equals = true;

		equals &= normal[0].Equals(vertex->normal[0]);
		equals &= normal[1].Equals(vertex->normal[1]);
		equals &= normal[2].Equals(vertex->normal[2]);

		equals &= uv[0].Equals(vertex->uv[0]);
		equals &= uv[1].Equals(vertex->uv[1]);

		return equals;
	}

	template <class T> void Fbx::Importer::GetVector(FbxLayerElementTemplate<T>* pLayerElement, T& pVector, int controlPointIdx, int vertexIdx)
	{
		switch (pLayerElement->GetMappingMode())
		{
		case FbxLayerElement::eByControlPoint:
			switch (pLayerElement->GetReferenceMode())
			{
			case FbxLayerElement::eDirect:
				pVector = pLayerElement->GetDirectArray().GetAt(controlPointIdx);
				break;
			case FbxLayerElement::eIndex:
			case FbxLayerElement::eIndexToDirect:
				{
					int idx = pLayerElement->GetIndexArray().GetAt(controlPointIdx);
					pVector = pLayerElement->GetDirectArray().GetAt(idx);
				}
				break;
			default:
				{
					int mode = (int)pLayerElement->GetReferenceMode();
					throw gcnew Exception(gcnew String("Unknown reference mode: ") + mode);
				}
				break;
			}
			break;

		case FbxLayerElement::eByPolygonVertex:
			switch (pLayerElement->GetReferenceMode())
			{
			case FbxLayerElement::eDirect:
				pVector = pLayerElement->GetDirectArray().GetAt(vertexIdx);
				break;
			case FbxLayerElement::eIndex:
			case FbxLayerElement::eIndexToDirect:
				{
					int idx = pLayerElement->GetIndexArray().GetAt(vertexIdx);
					pVector = pLayerElement->GetDirectArray().GetAt(idx);
				}
				break;
			default:
				{
					int mode = (int)pLayerElement->GetReferenceMode();
					throw gcnew Exception(gcnew String("Unknown reference mode: ") + mode);
				}
				break;
			}
			break;

		default:
			{
				int mode = (int)pLayerElement->GetMappingMode();
				throw gcnew Exception(gcnew String("Unknown mapping mode: ") + mode);
			}
			break;
		}
	}

	// from https://bitbucket.org/oc3/fxogrefbx/src/55d96c5d8ec4/Src/mesh.cpp?at=default
	FbxColor Fbx::Importer::GetFBXColor(FbxMesh *pMesh, int polyIndex, int polyPointIndex)
	{
		int lControlPointIndex = pMesh->GetPolygonVertex(polyIndex, polyPointIndex);
		int vertexId = polyIndex*3 + polyPointIndex;
		FbxColor color;
		for (int l = 0; l < pMesh->GetElementVertexColorCount(); l++)
		{
			FbxGeometryElementVertexColor* leVtxc = pMesh->GetElementVertexColor( l);

			switch (leVtxc->GetMappingMode())
			{
			case FbxGeometryElement::eByControlPoint:
				switch (leVtxc->GetReferenceMode())
				{
				case FbxGeometryElement::eDirect:
					color = leVtxc->GetDirectArray().GetAt(lControlPointIndex);
					break;
				case FbxGeometryElement::eIndexToDirect:
					{
						int id = leVtxc->GetIndexArray().GetAt(lControlPointIndex);
						color = leVtxc->GetDirectArray().GetAt(id);
					}
					break;
				default:
					break; // other reference modes not shown here!
				}
				break;

			case FbxGeometryElement::eByPolygonVertex:
				{
					switch (leVtxc->GetReferenceMode())
					{
					case FbxGeometryElement::eDirect:
						color = leVtxc->GetDirectArray().GetAt(vertexId);
						break;
					case FbxGeometryElement::eIndexToDirect:
						{
							int id = leVtxc->GetIndexArray().GetAt(vertexId);
							color = leVtxc->GetDirectArray().GetAt(id);
						}
						break;
					default:
						break; // other reference modes not shown here!
					}
				}
				break;

			case FbxGeometryElement::eByPolygon: // doesn't make much sense for UVs
			case FbxGeometryElement::eAllSame:   // doesn't make much sense for UVs
			case FbxGeometryElement::eNone:       // doesn't make much sense for UVs
				break;
			}
		}
		return color;
	}

	void Fbx::Importer::ImportMorph(FbxArray<FbxNode*>* pMeshArray)
	{
		for (int i = 0; i < pMeshArray->GetCount(); i++)
		{
			FbxNode* pNode = pMeshArray->GetAt(i);
			FbxMesh* pMesh = pNode->GetMesh();
			int numShapes = pMesh->GetDeformerCount(FbxDeformer::eBlendShape);
			bool channelOrganized = false;
			if (numShapes > 0)
			{
				FbxBlendShape* lBlendShape = NULL;
				if (numShapes == 1)
				{
					lBlendShape = (FbxBlendShape*)pMesh->GetDeformer(0, FbxDeformer::eBlendShape);
					if (lBlendShape->GetBlendShapeChannelCount() > 1)
					{
						numShapes = lBlendShape->GetBlendShapeChannelCount();
						channelOrganized = true;
					}
				}
				ImportedMorph^ morphList = gcnew ImportedMorph();
				morphList->KeyframeList = gcnew List<ImportedMorphKeyframe^>(numShapes);
				MorphList->Add(morphList);

				String^ clipName = gcnew String(pNode->GetName());
				int clipNameStartIdx = clipName->LastIndexOf("_morph_");
				if (clipNameStartIdx >= 0)
				{
					clipNameStartIdx += 7;
					morphList->Name = clipName->Substring(clipNameStartIdx, clipName->Length - clipNameStartIdx);
				}

				for (int j = 0; j < numShapes; j++)
				{
					FbxBlendShapeChannel* lChannel;
					if (channelOrganized)
					{
						lChannel = lBlendShape->GetBlendShapeChannel(j);
					}
					else
					{
						lBlendShape = (FbxBlendShape*)pMesh->GetDeformer(j, FbxDeformer::eBlendShape);
						if (lBlendShape->GetBlendShapeChannelCount() != 1)
						{
							Report::ReportLog("Warning! " + clipName + "'s blendShape " + j + " has " + lBlendShape->GetBlendShapeChannelCount() + " channels. Channels beyond the first are ignored.");
						}
						lChannel = lBlendShape->GetBlendShapeChannel(0);
					}
					if (lChannel->GetTargetShapeCount() != 1)
					{
						Report::ReportLog("Warning! " + clipName + "'s has a blendChannel with " + lChannel->GetTargetShapeCount() + " shapes. Shapes beyond the first are ignored.");
					}
					FbxShape* pShape = lChannel->GetTargetShape(0);
					ImportedMorphKeyframe^ morph = gcnew ImportedMorphKeyframe();
					morphList->KeyframeList->Add(morph);

					String^ shapeName = gcnew String(pShape->GetName());
					int shapeNameStartIdx = shapeName->LastIndexOf(".");
					if (shapeNameStartIdx >= 0)
					{
						shapeNameStartIdx += 1;
						shapeName = shapeName->Substring(shapeNameStartIdx, shapeName->Length - shapeNameStartIdx);
					}
					morph->Name = shapeName;
					
					FbxLayer* pLayerNormal = pShape->GetLayer(0, FbxLayerElement::eNormal);
					FbxLayerElementNormal* pLayerElementNormal = NULL;
					if (pLayerNormal != NULL)
					{
						pLayerElementNormal = pLayerNormal->GetNormals();
					}

					int numVerts = pShape->GetControlPointsCount();
					List<ImportedVertex^>^ vertList = gcnew List<ImportedVertex^>(numVerts);
					morph->VertexList = vertList;
					for (int k = 0; k < numVerts; k++)
					{
						ImportedVertex^ vertInfo = gcnew ImportedVertex();
						vertList->Add(vertInfo);
						vertInfo->BoneIndices = gcnew array<Byte>(4);
						vertInfo->Weights = gcnew array<float>(4);
						vertInfo->UV = gcnew array<float>(2);

						FbxVector4 lCoords = pShape->GetControlPointAt(k);
						vertInfo->Position = Vector3((float)lCoords[0], (float)lCoords[1], (float)lCoords[2]);

						if (pLayerElementNormal == NULL)
						{
							vertInfo->Normal = Vector3(0);
						}
						else
						{
							FbxVector4 lNorm;
							GetVector(pLayerElementNormal, lNorm, k, k);
							vertInfo->Normal = Vector3((float)lNorm[0], (float)lNorm[1], (float)lNorm[2]);
						}
					}
				}

				FbxLayer* pLayerVertexColor = pMesh->GetLayer(0, FbxLayerElement::eVertexColor);
				if (pLayerVertexColor != NULL)
				{
					FbxLayerElementVertexColor* pLayerElementVertexColor = pLayerVertexColor->GetVertexColors();
					List<unsigned short>^ morphedVertexIndices = gcnew List<unsigned short>(pMesh->GetControlPointsCount());
					for (int j = 0; j < pMesh->GetPolygonCount(); j++)
					{
						int polyVertIdxStart = pMesh->GetPolygonVertexIndex(j);
						for (int k = 0; k < pMesh->GetPolygonSize(j); k++)
						{
							unsigned short controlPointIdx = pMesh->GetPolygonVertices()[polyVertIdxStart + k];
							if (!morphedVertexIndices->Contains(controlPointIdx))
							{
								FbxColor c = GetFBXColor(pMesh, j, k);
								if (c.mRed < 0.1 && c.mGreen < 0.1 && c.mBlue > 0.9)
								{
									morphedVertexIndices->Add(controlPointIdx);
								}
							}
						}
					}
					morphList->MorphedVertexIndices = morphedVertexIndices;
				}
			}
		}
	}
}
