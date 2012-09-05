#include <fbxsdk.h>
#include <fbxfilesdk/kfbxio/kfbxiosettings.h>
#include "ODFPluginFBX.h"

using namespace System::Globalization;

namespace ODFPluginOld
{
	Fbx::Importer::Importer(ImporterBase^ base) : ImporterBase(base)
	{
		unnamedMeshCount = 0;
		FrameList = gcnew List<ImportedFrame^>();
		MeshList = gcnew List<ImportedMesh^>();
		MaterialList = gcnew List<ImportedMaterial^>();
		TextureList = gcnew List<ImportedTexture^>();
//		AnimationList = gcnew List<ImportedAnimation^>();
//		MorphList = gcnew List<ImportedMorph^>();

		pMaterials = NULL;
		pTextures = NULL;
	}

	Fbx::Importer::~Importer()
	{
		Fbx::Importer::!Importer();
		GC::SuppressFinalize(this);
	}

	Fbx::Importer::!Importer()
	{
		if (pMaterials != NULL)
		{
			delete pMaterials;
		}
		if (pTextures != NULL)
		{
			delete pTextures;
		}
	}

	void Fbx::Importer::Import()
	{
		pMaterials = new KArrayTemplate<KFbxSurfacePhong*>();
		pTextures = new KArrayTemplate<KFbxTexture*>();

		KFbxNode* pRootNode = pScene->GetRootNode();
		if (pRootNode != NULL)
		{
			ImportNode(nullptr, pRootNode);
//			KFbxNode* top = pRootNode->GetChild(0);
//			top->LclScaling.Set(KFbxVector4(top->LclScaling.Get()[0], top->LclScaling.Get()[1], -top->LclScaling.Get()[2]));
		}
	}

	void Fbx::Importer::ImportNode(ImportedFrame^ parent, KFbxNode* pNode)
	{
		KArrayTemplate<KFbxNode*>* pMeshArray = NULL;
		try
		{
			pMeshArray = new KArrayTemplate<KFbxNode*>();
			bool hasShapes = false;

			for (int i = 0; i < pNode->GetChildCount(); i++)
			{
				KFbxNode* pNodeChild = pNode->GetChild(i);
				if (pNodeChild->GetNodeAttribute() == NULL)
				{
					ImportedFrame^ frame = ImportFrame(parent, pNodeChild);
					ImportNode(frame, pNodeChild);
				}
				else
				{
					KFbxNodeAttribute::EAttributeType lAttributeType = pNodeChild->GetNodeAttribute()->GetAttributeType();

					switch (lAttributeType)
					{
						case KFbxNodeAttribute::eNULL:
						case KFbxNodeAttribute::eSKELETON:
							{
								ImportedFrame^ frame = ImportFrame(parent, pNodeChild);
								ImportNode(frame, pNodeChild);
							}
							break;

						case KFbxNodeAttribute::eMESH:
							if (pNodeChild->GetMesh()->GetShapeCount() > 0)
							{
								hasShapes = true;
							}
							pMeshArray->Add(pNodeChild);
							break;

						default:
							KString str = KString(lAttributeType);
							Report::ReportLog(gcnew String("Warning: ") + gcnew String(pNodeChild->GetName()) + gcnew String(" has unsupported node attribute type ") + gcnew String(str.Buffer()));
							break;
					}
				}
			}

			if (hasShapes)
			{
//				ImportMorph(pMeshArray);
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

	ImportedFrame^ Fbx::Importer::ImportFrame(ImportedFrame^ parent, KFbxNode* pNode)
	{
		ImportedFrame^ frame = gcnew ImportedFrame();
		frame->InitChildren(pNode->GetChildCount());
		frame->Name = gcnew String(pNode->GetName());
/*		KFbxProperty prop = pNode->FindProperty("ID");
		if (prop.IsValid())
		{
			KString idStr = NULL;
			KFbxGet(prop, idStr);
			try
			{
				frame->Id = gcnew String(idStr);
			}
			catch (Exception^)
			{
				throw gcnew Exception("Bad frame ID " + gcnew String(idStr) + (frame->Name != nullptr && frame->Name->Length > 0 ? " for frame " + frame->Name : ""));
			}
		}
		ObjectID^ meshID;
		prop = pNode->FindProperty("MeshID");
		if (prop.IsValid())
		{
			KString idStr = NULL;
			KFbxGet(prop, idStr);
			try
			{
				meshID = gcnew ObjectID(gcnew String(idStr));
			}
			catch (Exception^)
			{
				throw gcnew Exception("Bad mesh ID " + gcnew String(idStr) + (frame->Name != nullptr && frame->Name->Length > 0 ? " for frame " + frame->Name : ""));
			}
		}
		else
			meshID = ODF::ObjectID::INVALID;
		frame->meshId = meshID;*/

		if (parent == nullptr)
		{
			FrameList->Add(frame);
		}
		else
		{
			parent->AddChild(frame);
		}

		KFbxXMatrix lNodeMatrix = pScene->GetEvaluator()->GetNodeLocalTransform(pNode);
		for (int m = 0; m < 4; m++)
		{
			for (int n = 0; n < 4; n++)
			{
				frame->Matrix[m, n] = (float)lNodeMatrix[m][n];
			}
		}

		return frame;
	}

	void Fbx::Importer::ImportMesh(ImportedFrame^ parent, KArrayTemplate<KFbxNode*>* pMeshArray)
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

			for (int i = 0; i < pMeshArray->GetCount(); i++)
			{
				ImportedSubmesh^ submesh = gcnew ImportedSubmesh();
				meshList->SubmeshList->Add(submesh);
				submesh->Index = i;

				KFbxNode* pMeshNode = pMeshArray->GetAt(i);
				KFbxMesh* pMesh = pMeshNode->GetMesh();

				String^ submeshName = gcnew String(pMeshNode->GetName());
				submesh->Name = submeshName;
/*				submesh->TextureIds = gcnew array<String^>(4);
				for (int j = 0; j < submesh->TextureIds->Length; j++)
				{
					KFbxProperty prop;
					WITH_MARSHALLED_STRING(
						texName, "Texture" + (j + 1) + "ID", \
						prop = pMeshNode->FindProperty(texName);
					);
					if (prop.IsValid())
					{
						KString tex = NULL;
						KFbxGet(prop, tex);
						submesh->TextureIds[j] = gcnew String(tex);
					}
				}*/

				KFbxLayer* pLayerNormal = pMesh->GetLayer(0, KFbxLayerElement::eNORMAL);
				KFbxLayerElementNormal* pLayerElementNormal = NULL;
				if (pLayerNormal != NULL)
				{
					pLayerElementNormal = pLayerNormal->GetNormals();
				}

				KFbxLayer* pLayerUV = pMesh->GetLayer(0, KFbxLayerElement::eUV);
				KFbxLayerElementUV* pLayerElementUV = NULL;
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

						KFbxVector4 pos = pMesh->GetControlPointAt(controlPointIdx);
						vert->position = gcnew array<float>(3) { (float)pos[0], (float)pos[1], (float)pos[2] };

						if (pLayerElementNormal != NULL)
						{
							KFbxVector4 norm;
							GetVector(pLayerElementNormal, norm, controlPointIdx, vertCount);
							vert->normal = gcnew array<float>(3) { (float)norm[0], (float)norm[1], (float)norm[2] };
						}

						if (pLayerElementUV != NULL)
						{
							KFbxVector2 uv;
							GetVector(pLayerElementUV, uv, controlPointIdx, vertCount);
							vert->uv = gcnew array<float>(2) { (float)uv[0], -(float)uv[1] };
						}

						List<Vertex^>^ vertMapList = vertMap[controlPointIdx];
						Vertex^ foundVert = nullptr;
						for (int m = 0; m < vertMapList->Count; m++)
						{
							if (vertMapList[m]->Equals(vert))
							{
								foundVert = vertMapList[m];
								break;
							}
						}

						if (foundVert == nullptr)
						{
							vertMapList->Add(vert);
						}
						faceMap[j][k] = vertMapList[0];

						vertCount++;
					}
				}

				for (int j = 0; j < vertMap->Length; j++)
				{
					List<Vertex^>^ vertMapList = vertMap[j];
					int numNormals = vertMapList->Count;
					if (numNormals > 0)
					{
						Vertex^ vertNormal = vertMapList[0];
						while (vertMapList->Count > 1)
						{
							array<float>^ addNormal = vertMapList[1]->normal;
							vertNormal->normal[0] += addNormal[0];
							vertNormal->normal[1] += addNormal[1];
							vertNormal->normal[2] += addNormal[2];
							vertMapList->RemoveAt(1);
						}
						vertNormal->normal[0] /= numNormals;
						vertNormal->normal[1] /= numNormals;
						vertNormal->normal[2] /= numNormals;
					}
				}

				bool skinned = false;
				KFbxSkin* pSkin = (KFbxSkin*)pMesh->GetDeformer(0, KFbxDeformer::eSKIN);
				if (pSkin != NULL)
				{
					if (pMesh->GetDeformerCount(KFbxDeformer::eSKIN) > 1)
					{
						Report::ReportLog(gcnew String("Warning: Mesh ") + gcnew String(pMeshNode->GetName()) + " has more than 1 skin. Only the first will be used");
					}
					skinned = true;

					int numClusters = pSkin->GetClusterCount();
					List<ImportedBone^>^ boneList = gcnew List<ImportedBone^>(numClusters);
/*					ObjectID^ frameID;
					KFbxProperty prop = parent->FindProperty("ID");
					if (prop.IsValid())
					{
						KString idStr = NULL;
						KFbxGet(prop, idStr);
						frameID = gcnew ODF::ObjectID(gcnew String(idStr));
					}
					else
						frameID = ODF::ObjectID::INVALID;
					boneList->MeshFrameId = frameID;
					boneList->meshObjId = gcnew ODF::ObjectID(submesh->id);*/
					for (int j = 0; j < numClusters; j++)
					{
						KFbxCluster* pCluster = pSkin->GetCluster(j);
						if (pCluster->GetLinkMode() == KFbxCluster::eADDITIVE)
						{
							throw gcnew Exception(gcnew String("Mesh ") + gcnew String(pMeshNode->GetName()) + " has additive weights and aren't supported");
						}

						KFbxXMatrix lMatrix, lMeshMatrix;
						pCluster->GetTransformMatrix(lMeshMatrix);
						// http://www.gamedev.net/topic/619416-fbx-skeleton-animation-need-help/
						/*KFbxXMatrix geomMatrix = pMeshNode->GetScene()->GetEvaluator()->GetNodeLocalTransform(pMeshNode);
						lMeshMatrix *= geomMatrix;*/
						pCluster->GetTransformLinkMatrix(lMatrix);
						lMatrix = (lMeshMatrix.Inverse() * lMatrix).Inverse();
						Matrix boneMatrix;
						for (int m = 0; m < 4; m++)
						{
							for (int n = 0; n < 4; n++)
							{
								boneMatrix[m, n] = (float)lMatrix.mData[m][n];
							}
						}

						KFbxNode* pLinkNode = pCluster->GetLink();
						String^ boneName = gcnew String(pLinkNode->GetName());
						ImportedBone^ boneInfo = gcnew ImportedBone();
						boneList->Add(boneInfo);
						boneInfo->FrameName = boneName;
						boneInfo->Matrix = boneMatrix;

						int* lIndices = pCluster->GetControlPointIndices();
						double* lWeights = pCluster->GetControlPointWeights();
						int numIndices = pCluster->GetControlPointIndicesCount();
						array<int>^ vertexIndexArray = gcnew array<int>(numIndices);
						array<float>^ weightArray = gcnew array<float>(numIndices);
						for (int k = 0; k < numIndices; k++)
						{
							vertexIndexArray[k] = lIndices[k];
							weightArray[k] = (float)lWeights[k];
						}
						boneInfo->VertexIndexArray = vertexIndexArray;
						boneInfo->WeightArray = weightArray;
					}
					submesh->BoneList = boneList;
				}

				int vertIdx = 0;
				List<ImportedVertex^>^ vertList = gcnew List<ImportedVertex^>(vertMap->Length);
				for (int j = 0; j < vertMap->Length; j++)
				{
					for (int k = 0; k < vertMap[j]->Count; k++)
					{
						Vertex^ vert = vertMap[j][k];
						vert->index = vertIdx;

						ImportedVertex^ vertInfo = gcnew ImportedVertex();
						vertList->Add(vertInfo);
						vertInfo->Position = Vector3(vert->position[0], vert->position[1], vert->position[2]);

						vertInfo->Normal = Vector3(vert->normal[0], vert->normal[1], vert->normal[2]);
						vertInfo->UV = Vector2(vert->uv[0], vert->uv[1]);

						vertIdx++;
					}
				}
				submesh->VertexList = vertList;

				List<ImportedFace^>^ faceList = gcnew List<ImportedFace^>(numFaces);
				submesh->FaceList = faceList;
				for (int j = 0; j < numFaces; j++)
				{
					ImportedFace^ face = gcnew ImportedFace();
					faceList->Add(face);
					face->VertexIndices = gcnew array<unsigned short>(3);
					face->VertexIndices[0] = faceMap[j][0]->index;
					face->VertexIndices[1] = faceMap[j][1]->index;
					face->VertexIndices[2] = faceMap[j][2]->index;
				}

				submesh->Textures = gcnew array<String^>(4);
				ImportedMaterial^ matInfo = ImportMaterial(pMesh, submesh->Textures);
				if (matInfo != nullptr)
				{
					submesh->Material = matInfo->Name;
				}
			}
		}
	}

	ImportedMaterial^ Fbx::Importer::ImportMaterial(KFbxMesh* pMesh, array<String^>^ texNames)
	{
		ImportedMaterial^ matInfo = nullptr;

		KFbxLayer* pLayerMaterial = pMesh->GetLayer(0, KFbxLayerElement::eMATERIAL);
		if (pLayerMaterial != NULL)
		{
			KFbxLayerElementMaterial* pLayerElementMaterial = pLayerMaterial->GetMaterials();
			if (pLayerElementMaterial != NULL)
			{
				KFbxSurfaceMaterial* pMaterial = NULL;
				switch (pLayerElementMaterial->GetReferenceMode())
				{
				case KFbxLayerElement::eDIRECT:
					pMaterial = pMesh->GetNode()->GetMaterial(0);
					break;

				case KFbxLayerElement::eINDEX_TO_DIRECT:
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
					if (pMaterial->GetClassId().Is(KFbxSurfacePhong::ClassId))
					{
						KFbxSurfacePhong* pPhong = (KFbxSurfacePhong*)pMaterial;
						int matIdx = pMaterials->Find(pPhong);
						if (matIdx >= 0)
						{
							matInfo = MaterialList[matIdx];
						}
						else
						{
							matInfo = gcnew ImportedMaterial();
							matInfo->Name = gcnew String(pPhong->GetName());
							
							fbxDouble3 lDiffuse = pPhong->Diffuse.Get();
							fbxDouble1 lDiffuseFactor = pPhong->DiffuseFactor.Get();
							matInfo->Diffuse = Color4((float)lDiffuseFactor, (float)lDiffuse[0], (float)lDiffuse[1], (float)lDiffuse[2]);

							fbxDouble3 lAmbient = pPhong->Ambient.Get();
							fbxDouble1 lAmbientFactor = pPhong->AmbientFactor.Get();
							matInfo->Ambient = Color4((float)lAmbientFactor, (float)lAmbient[0], (float)lAmbient[1], (float)lAmbient[2]);

							fbxDouble3 lEmissive = pPhong->Emissive.Get();
							fbxDouble1 lEmissiveFactor = pPhong->EmissiveFactor.Get();
							matInfo->Emissive = Color4((float)lEmissiveFactor, (float)lEmissive[0], (float)lEmissive[1], (float)lEmissive[2]);

							fbxDouble3 lSpecular = pPhong->Specular.Get();
							fbxDouble1 lSpecularFactor = pPhong->SpecularFactor.Get();
							matInfo->Specular = Color4((float)lSpecularFactor, (float)lSpecular[0], (float)lSpecular[1], (float)lSpecular[2]);
							matInfo->Power = (float)pPhong->Shininess.Get();

//							array<String^>^ texNames = gcnew array<String^>(4);
							texNames[0] = ImportTexture((KFbxFileTexture*)pPhong->Diffuse.GetSrcObject(KFbxFileTexture::ClassId));
							texNames[1] = ImportTexture((KFbxFileTexture*)pPhong->Ambient.GetSrcObject(KFbxFileTexture::ClassId));
							texNames[2] = ImportTexture((KFbxFileTexture*)pPhong->Emissive.GetSrcObject(KFbxFileTexture::ClassId));
							texNames[3] = ImportTexture((KFbxFileTexture*)pPhong->Specular.GetSrcObject(KFbxFileTexture::ClassId));
//							matInfo->Textures = texNames;

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

	String^ Fbx::Importer::ImportTexture(KFbxFileTexture* pTexture)
	{
		using namespace System::IO;

		String^ texName = String::Empty;

		if (pTexture != NULL)
		{
			texName = Path::GetFileName(gcnew String(pTexture->GetName()));

			int pTexIdx = pTextures->Find(pTexture);
			if (pTexIdx < 0)
			{
				pTextures->Add(pTexture);

				String^ texPath = gcnew String(pTexture->GetName());
				ImportedTexture^ tex = gcnew ImportedTexture(texPath);
				if (tex != nullptr)
				{
					TextureList->Add(tex);
				}
			}
		}

		return texName;
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

	template <class T> void Fbx::Importer::GetVector(KFbxLayerElementTemplate<T>* pLayerElement, T& pVector, int controlPointIdx, int vertexIdx)
	{
		switch (pLayerElement->GetMappingMode())
		{
		case KFbxLayerElement::eBY_CONTROL_POINT:
			switch (pLayerElement->GetReferenceMode())
			{
			case KFbxLayerElement::eDIRECT:
				pVector = pLayerElement->GetDirectArray().GetAt(controlPointIdx);
				break;
			case KFbxLayerElement::eINDEX:
			case KFbxLayerElement::eINDEX_TO_DIRECT:
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

		case KFbxLayerElement::eBY_POLYGON_VERTEX:
			switch (pLayerElement->GetReferenceMode())
			{
			case KFbxLayerElement::eDIRECT:
				pVector = pLayerElement->GetDirectArray().GetAt(vertexIdx);
				break;
			case KFbxLayerElement::eINDEX:
			case KFbxLayerElement::eINDEX_TO_DIRECT:
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
}
