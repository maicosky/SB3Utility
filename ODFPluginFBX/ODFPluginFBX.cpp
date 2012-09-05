#include <fbxsdk.h>
#include <fbxfilesdk/kfbxio/kfbxiosettings.h>
#include "ODFPluginFBX.h"

namespace ODFPluginOld
{
	char* Fbx::StringToCharArray(String^ s)
	{
		return (char*)(void*)Marshal::StringToHGlobalAnsi(s);
	}

	ICanImport^ Fbx::ImporterBase::TestFormat(System::String ^path)
	{
		ImporterBase^ base = gcnew ImporterBase(path);
		base->Load();
		KFbxDisplayLayer* layer = base->pScene->FindMember(FBX_TYPE(KFbxDisplayLayer), "ODF");
		bool symmetrical = base->pScene->FindMember(FBX_TYPE(KFbxDisplayLayer), "SYMMETRICAL") != NULL;
		return layer ? (ICanImport^)gcnew Fbx::Importer(base) : nullptr;
	}

	Fbx::ImporterBase::ImporterBase(String^ path)
	{
		pin_ptr<KFbxSdkManager*> pSdkManagerPin = &pSdkManager;
		pin_ptr<KFbxScene*> pScenePin = &pScene;
		Init(pSdkManagerPin, pScenePin);

		pImporter = KFbxImporter::Create(pSdkManager, "");

		IOS_REF.SetBoolProp(IMP_FBX_MATERIAL, true);
		IOS_REF.SetBoolProp(IMP_FBX_TEXTURE, true);
		IOS_REF.SetBoolProp(IMP_FBX_LINK, true);
		IOS_REF.SetBoolProp(IMP_FBX_SHAPE, true);
		IOS_REF.SetBoolProp(IMP_FBX_GOBO, true);
		IOS_REF.SetBoolProp(IMP_FBX_ANIMATION, true);
		IOS_REF.SetBoolProp(IMP_FBX_GLOBAL_SETTINGS, true);

		WITH_MARSHALLED_STRING
		(
			cPath, path, \
			if (!pImporter->Initialize(cPath, -1, pSdkManager->GetIOSettings())) \
			{ \
				throw gcnew Exception("Failed to initialize KFbxImporter: " + gcnew String(pImporter->GetLastErrorString())); \
			}
		);

		currentDirectory = Directory::GetCurrentDirectory();
		Directory::SetCurrentDirectory(Path::GetDirectoryName(path));

		disposed = false;
	}

	Fbx::ImporterBase::ImporterBase(ImporterBase^ copy)
	{
		this->currentDirectory = copy->currentDirectory;
		copy->currentDirectory = nullptr;

		this->disposed = copy->disposed;
		copy->disposed = true;

		this->pImporter = copy->pImporter;
		copy->pImporter = NULL;

		this->pScene = copy->pScene;
		copy->pScene = NULL;

		this->pSdkManager = copy->pSdkManager;
		copy->pSdkManager = NULL;
	}

	Fbx::ImporterBase::~ImporterBase()
	{
		this->!ImporterBase();
		GC::SuppressFinalize(this);
	}

	Fbx::ImporterBase::!ImporterBase()
	{
		if (!this->disposed)
		{
			if (currentDirectory != nullptr)
			{
				Directory::SetCurrentDirectory(currentDirectory);
				currentDirectory = nullptr;
			}
			if (pImporter != NULL)
			{
				pImporter->Destroy();
				pImporter = NULL;
			}
			if (pScene != NULL)
			{
				pScene->Destroy();
				pScene = NULL;
			}
			if (pSdkManager != NULL)
			{
				pSdkManager->Destroy();
				pSdkManager = NULL;
			}
		}
		disposed = true;
	}

	void Fbx::ImporterBase::Load()
	{
		pImporter->Import(pScene);
	}

	void Fbx::Init(KFbxSdkManager** pSdkManager, KFbxScene** pScene)
	{
		*pSdkManager = KFbxSdkManager::Create();
		if (!pSdkManager)
		{
			throw gcnew Exception(gcnew String("Unable to create the FBX SDK manager"));
		}

		KFbxIOSettings* ios = KFbxIOSettings::Create(*pSdkManager, IOSROOT);
		(*pSdkManager)->SetIOSettings(ios);

		KString lPath = KFbxGetApplicationDirectory();
#if defined(KARCH_ENV_WIN)
		KString lExtension = "dll";
#elif defined(KARCH_ENV_MACOSX)
		KString lExtension = "dylib";
#elif defined(KARCH_ENV_LINUX)
		KString lExtension = "so";
#endif
		(*pSdkManager)->LoadPluginsDirectory(lPath.Buffer(), lExtension.Buffer());

		*pScene = KFbxScene::Create(*pSdkManager, "");
	}

	Vector3 Fbx::QuaternionToEuler(Quaternion q)
	{
		KFbxXMatrix lMatrixRot;
		lMatrixRot.SetQ(KFbxQuaternion(q.X, q.Y, q.Z, q.W));
		KFbxVector4 lEuler = lMatrixRot.GetR();
		return Vector3((float)lEuler[0], (float)lEuler[1], (float)lEuler[2]);
	}

	Quaternion Fbx::EulerToQuaternion(Vector3 v)
	{
		KFbxXMatrix lMatrixRot;
		lMatrixRot.SetR(KFbxVector4(v.X, v.Y, v.Z));
		KFbxQuaternion lQuaternion = lMatrixRot.GetQ();
		return Quaternion((float)lQuaternion[0], (float)lQuaternion[1], (float)lQuaternion[2], (float)lQuaternion[3]);
	}
}
