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

namespace ODFPluginOld
{
	public interface class ICanImport
	{
		void Import();
	};

	public ref class Fbx
	{
	public:
		static Vector3 QuaternionToEuler(Quaternion q);
		static Quaternion EulerToQuaternion(Vector3 v);

		ref class ImporterBase : IDisposable
		{
		private:
			bool disposed;

		protected:
			KFbxSdkManager* pSdkManager;
			KFbxScene* pScene;
			KFbxImporter* pImporter;

			String^ currentDirectory;

			ImporterBase(String^ path);
			ImporterBase(ImporterBase^ copy);

		public:
			static ICanImport^ TestFormat(String^ path);
			void Load();

			~ImporterBase();
			!ImporterBase();
		};

		ref class Importer : public ImporterBase, public ICanImport //, public IImported
		{
		public:
			virtual property List<ImportedFrame^>^ FrameList;
			virtual property List<ImportedMesh^>^ MeshList;
			virtual property List<ImportedMaterial^>^ MaterialList;
			virtual property List<ImportedTexture^>^ TextureList;
//			virtual property List<ImportedAnimation^>^ AnimationList;
//			virtual property List<ImportedMorph^>^ MorphList;

			Importer(ImporterBase^ base);
			~Importer();
			!Importer();

			virtual void Import();

		private:
			KArrayTemplate<KFbxSurfacePhong*>* pMaterials;
			KArrayTemplate<KFbxTexture*>* pTextures;
			int unnamedMeshCount;

			void ImportNode(ImportedFrame^ parent, KFbxNode* pNode);
			ImportedFrame^ ImportFrame(ImportedFrame^ parent, KFbxNode* pNode);
			void ImportMesh(ImportedFrame^ parent, KArrayTemplate<KFbxNode*>* pMeshArray);
			ImportedMaterial^ ImportMaterial(KFbxMesh* pMesh, array<String^>^ textures);
			String^ ImportTexture(KFbxFileTexture* pTexture);
			template <class T> void GetVector(KFbxLayerElementTemplate<T>* pLayerElement, T& pVector, int controlPointIdx, int vertexIdx);

			ref class Vertex
			{
			public:
				int index;
				array<float>^ position;
				array<float>^ normal;
				array<float>^ uv;
				List<Byte>^ boneIndices;
				List<float>^ weights;

				bool Equals(Vertex^ vertex);

				Vertex();
			};
		};

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
