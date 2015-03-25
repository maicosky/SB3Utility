// SB3UtilityFBX.h

#pragma once

#ifdef IOS_REF
	#undef  IOS_REF
	#define IOS_REF (*(pSdkManager->GetIOSettings()))
#endif

using namespace System;
using namespace System::Collections::Generic;
using namespace System::IO;
using namespace System::Runtime::InteropServices;
using namespace SlimDX;

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

namespace SB3Utility {

	public ref class Fbx
	{
	public:
		static Vector3 QuaternionToEuler(Quaternion q);
		static Quaternion EulerToQuaternion(Vector3 v);
		static void InterpolateKeyframes(List<Tuple<ImportedAnimationTrack^, array<xaAnimationKeyframe^>^>^>^ extendedTrackList, int resampleCount);
		static void InterpolateKeyframes(List<Tuple<ImportedAnimationTrack^, array<ImportedAnimationKeyframe^>^>^>^ extendedTrackList, int resampleCount, bool linear);
		static void InterpolateSampledTracks(List<Tuple<ImportedAnimationTrack^, ImportedAnimationSampledTrack^>^>^ extendedTrackList, int resampleCount, bool linear);

		ref class Importer : IImported
		{
		public:
			virtual property List<ImportedFrame^>^ FrameList;
			virtual property List<ImportedMesh^>^ MeshList;
			virtual property List<ImportedMaterial^>^ MaterialList;
			virtual property List<ImportedTexture^>^ TextureList;
			virtual property List<ImportedAnimation^>^ AnimationList;
			virtual property List<ImportedMorph^>^ MorphList;

			Importer(String^ path, bool negateQuaternionFlips);

		private:
			FbxArray<FbxSurfacePhong*>* pMaterials;
			FbxArray<FbxTexture*>* pTextures;
			int unnamedMeshCount;

			char* cPath;
			FbxManager* pSdkManager;
			FbxScene* pScene;
			FbxImporter* pImporter;

			bool negateQuaternionFlips;
			bool generatingTangentsReported;

			void ImportNode(ImportedFrame^ parent, FbxNode* pNode);
			ImportedFrame^ ImportFrame(ImportedFrame^ parent, FbxNode* pNode);
			void ImportMesh(ImportedFrame^ parent, FbxArray<FbxNode*>* pMeshArray);
			ImportedMaterial^ ImportMaterial(FbxMesh* pMesh);
			String^ ImportTexture(FbxFileTexture* pTexture);
			void ImportAnimation();
			void ImportAnimation(FbxAnimLayer* pAnimLayer, FbxNode* pNode, ImportedKeyframedAnimation^ wsAnimation);
			void ImportAnimation(FbxAnimLayer* pAnimLayer, FbxNode* pNode, ImportedSampledAnimation^ wsAnimation);
			Type^ GetAnimationType(FbxAnimLayer* pAnimLayer, FbxNode* pNode);
			template <class T> void GetVector(FbxLayerElementTemplate<T>* pLayerElement, T& pVector, int controlPointIdx, int vertexIdx);
			FbxColor Fbx::Importer::GetFBXColor(FbxMesh *pMesh, int polyIndex, int polyPointIndex);
			void ImportMorph(FbxArray<FbxNode*>* pMeshArray);

			ref class Vertex
			{
			public:
				int index;
				array<float>^ position;
				array<float>^ normal;
				array<float>^ uv;
				List<Byte>^ boneIndices;
				List<float>^ weights;
				array<float>^ tangent;

				bool Equals(Vertex^ vertex);

				Vertex();
			};
		};

		ref class Exporter
		{
		public:
			static void Export(String^ path, xxParser^ xxParser, List<xxFrame^>^ meshParents, List<xaParser^>^ xaSubfileList, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, float filterPrecision, String^ exportFormat, bool allFrames, bool skins, bool embedMedia, bool compatibility);
			static void ExportMorph(String^ path, xxParser^ xxParser, xxFrame^ meshFrame, xaMorphClip^ morphClip, xaParser^ xaparser, String^ exportFormat, bool oneBlendShape, bool embedMedia, bool compatibility);

			static void Export(String^ path, IImported^ imported, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, float filterPrecision, String^ exportFormat, bool allFrames, bool allBones, bool skins, bool compatibility);
			static void ExportMorph(String^ path, IImported^ imported, String^ exportFormat, bool oneBlendShape, bool compatibility);

		private:
			HashSet<String^>^ frameNames;
			HashSet<String^>^ meshNames;
			List<xxFrame^>^ meshFrames;
			bool EulerFilter;
			float filterPrecision;
			bool exportSkins;
			bool embedMedia;
			xxParser^ xxparser;

			IImported^ imported;

			char* cDest;
			char* cFormat;
			FbxManager* pSdkManager;
			FbxScene* pScene;
			FbxExporter* pExporter;
			FbxArray<FbxSurfacePhong*>* pMaterials;
			FbxArray<FbxFileTexture*>* pTextures;
			FbxArray<FbxNode*>* pMeshNodes;

			Exporter(String^ path, xxParser^ xxparser, List<xxFrame^>^ meshParents, String^ exportFormat, bool allFrames, bool skins, bool embedMedia, bool compatibility);
			~Exporter();
			!Exporter();
			void ExportFrame(FbxNode* pParentNode, xxFrame^ frame);
			void ExportMesh(FbxNode* pFrameNode, xxFrame^ frame);
			FbxFileTexture* ExportTexture(xxMaterialTexture^ matTex, FbxLayerElementTexture*& pLayerTexture, FbxMesh* pMesh);
			void ExportAnimations(List<xaParser^>^ xaSubfileList, int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, float filterPrecision);
			void SetJoints();
			void SetJointsNode(FbxNode* pNode, HashSet<String^>^ boneNames, bool allBones);
			void ExportMorphs(xxFrame^ baseFrame, xaMorphClip^ morphClip, xaParser^ xaparser, bool oneBlendShape);

			Exporter(String^ path, IImported^ imported, String^ exportFormat, bool allFrames, bool allBones, bool skins, bool compatibility);
			HashSet<String^>^ SearchHierarchy();
			void SearchHierarchy(ImportedFrame^ frame, HashSet<String^>^ exportFrames);
			void SetJointsFromImportedMeshes(bool allBones);
			void ExportFrame(FbxNode* pParentNode, ImportedFrame^ frame);
			void ExportMesh(FbxNode* pFrameNode, ImportedMesh^ meshList);
			FbxFileTexture* ExportTexture(ImportedTexture^ matTex, FbxLayerElementTexture*& pTextureLayer, FbxMesh* pMesh);
			void ExportAnimations(int startKeyframe, int endKeyframe, bool linear, bool EulerFilter, float filterValue);
			void ExportKeyframedAnimation(ImportedKeyframedAnimation^ parser, FbxString& kTakeName, int startKeyframe, int endKeyframe, bool linear, FbxAnimCurveFilterUnroll* EulerFilter, float filterPrecision,
					FbxPropertyT<FbxDouble3>& scale, FbxPropertyT<FbxDouble4>& rotate, FbxPropertyT<FbxDouble3>& translate, List<String^>^ pNotFound);
			void ExportSampledAnimation(ImportedSampledAnimation^ parser, FbxString& kTakeName, int startKeyframe, int endKeyframe, bool linear, FbxAnimCurveFilterUnroll* EulerFilter, float filterPrecision,
					FbxPropertyT<FbxDouble3>& scale, FbxPropertyT<FbxDouble4>& rotate, FbxPropertyT<FbxDouble3>& translate, List<String^>^ pNotFound);
			void ExportMorphs(IImported^ imported, bool oneBlendShape);
		};

	private:
		ref class InterpolationHelper
		{
		private:
			FbxScene* pScene;
			FbxAnimLayer* pAnimLayer;
			FbxAnimEvaluator* pAnimEvaluator;

			FbxAnimCurveDef::EInterpolationType interpolationMethod;

			FbxPropertyT<FbxDouble3>* scale, * translate;
			FbxPropertyT<FbxDouble4>* rotate;
			FbxAnimCurve* pScaleCurveX, * pScaleCurveY, * pScaleCurveZ,
				* pRotateCurveX, * pRotateCurveY, * pRotateCurveZ, * pRotateCurveW,
				* pTranslateCurveX, * pTranslateCurveY, * pTranslateCurveZ;

			array<FbxAnimCurve*>^ allCurves;

		public:
			static const char* pScaleName = "Scale";
			static const char* pRotateName = "Rotate";
			static const char* pTranslateName = "Translate";

			InterpolationHelper(FbxScene* scene, FbxAnimLayer* layer, FbxAnimCurveDef::EInterpolationType interpolationMethod,
				FbxPropertyT<FbxDouble3>* scale, FbxPropertyT<FbxDouble4>* rotate, FbxPropertyT<FbxDouble3>* translate);
			List<xaAnimationKeyframe^>^ InterpolateTrack(List<xaAnimationKeyframe^>^ keyframes, int resampleCount);
			array<ImportedAnimationKeyframe^>^ InterpolateTrack(array<ImportedAnimationKeyframe^>^ keyframes, int resampleCount);
			ImportedAnimationSampledTrack^ InterpolateTrack(ImportedAnimationSampledTrack^ track, int resampleCount);
		};

		static char* StringToCharArray(String^ s);
		static void Init(FbxManager** pSdkManager, FbxScene** pScene);
	};
}
