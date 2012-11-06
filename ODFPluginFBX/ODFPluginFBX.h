// ODFPluginFBX.h

#pragma once

#ifdef IOS_REF
	#undef  IOS_REF
	#define IOS_REF (*(pSdkManager->GetIOSettings()))
#endif

#define WITH_MARSHALLED_STRING(name,str,block)\
	{ \
		char* name; \
		try \
		{ \
			name = StringToCharArray(str); \
			block \
		} \
		finally \
		{ \
			Marshal::FreeHGlobal((IntPtr)name); \
		} \
	}

using namespace System;
using namespace System::Collections::Generic;
using namespace System::IO;
using namespace System::Runtime::InteropServices;
using namespace SlimDX;

using namespace SB3Utility;
using namespace ODFPlugin;

namespace ODFPlugin
{
	public ref class Fbx
	{
	public:
		static Vector3 QuaternionToEuler(Quaternion q);
		static Quaternion EulerToQuaternion(Vector3 v);

		ref class Exporter
		{
		public:
			static void Export(String^ path, odfParser^ parser, List<odfMesh^>^ meshes, String^ exportFormat, bool allFrames, bool skins, bool _8dot3);

		private:
			HashSet<int>^ frameIDs;
			HashSet<int>^ meshIDs;
			bool exportSkins;
			odfParser^ parser;

			char* cDest;
			char* cFormat;
			KFbxSdkManager* pSdkManager;
			KFbxScene* pScene;
			KFbxExporter* pExporter;
			KArrayTemplate<KFbxSurfacePhong*>* pMaterials;
			KArrayTemplate<KFbxFileTexture*>* pTextures;
			KArrayTemplate<KFbxNode*>* pMeshNodes;

			Exporter(String^ path, odfParser^ parser, List<odfMesh^>^ meshes, String^ exportFormat, bool allFrames, bool skins);
			~Exporter();
			!Exporter();
			void ExportFrame(KFbxNode* pParentNode, odfFrame^ frame, List<odfFrame^>^ meshFrames, KArrayTemplate<KFbxNode*>* pMeshNodes);
			void ExportMesh(KFbxNode* pFrameNode, odfFrame^ frame);
			KFbxFileTexture* ExportTexture(ObjectID^ matTexID, KFbxLayerElementTexture*& pLayerTexture, KFbxMesh* pMesh);
			void SetJoints();
			void SetJointsNode(KFbxNode* pNode, HashSet<int>^ boneIDs);
			KFbxNode* FindChild(int ID, KFbxNode* pNode);
		};

	private:
		static char* StringToCharArray(String^ s);
		static void Init(KFbxSdkManager** pSdkManager, KFbxScene** pScene);
	};
}
