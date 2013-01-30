#include <fbxsdk.h>
#include <fbxfilesdk/kfbxio/kfbxiosettings.h>
#include "SB3UtilityFBX.h"

namespace SB3Utility
{
	char* Fbx::StringToCharArray(String^ s)
	{
		return (char*)(void*)Marshal::StringToHGlobalAnsi(s);
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

#define ADD_KEY_VECTOR3(curveX, curveY, curveZ, time, vec, interpolationMethod) \
	{ \
		int keyIndex = curveX->KeyAdd(time); \
		curveX->KeySet(keyIndex, time, vec.X, interpolationMethod); \
		keyIndex = curveY->KeyAdd(time); \
		curveY->KeySet(keyIndex, time, vec.Y, interpolationMethod); \
		keyIndex = curveZ->KeyAdd(time); \
		curveZ->KeySet(keyIndex, time, vec.Z, interpolationMethod); \
	}

#define ADD_KEY_VECTOR4(curveX, curveY, curveZ, curveW, time, vec, interpolationMethod) \
	{ \
		int keyIndex = curveX->KeyAdd(time); \
		curveX->KeySet(keyIndex, time, vec.X, interpolationMethod); \
		keyIndex = curveY->KeyAdd(time); \
		curveY->KeySet(keyIndex, time, vec.Y, interpolationMethod); \
		keyIndex = curveZ->KeyAdd(time); \
		curveZ->KeySet(keyIndex, time, vec.Z, interpolationMethod); \
		keyIndex = curveW->KeyAdd(time); \
		curveW->KeySet(keyIndex, time, vec.W, interpolationMethod); \
	}

#define GET_CHANNEL_VALUE_VECTOR3(prop, time, vec) \
	{ \
		KFbxAnimCurveNode& val = pAnimEvaluator->GetPropertyValue(prop, time); \
		vec.X = (float)val.GetChannelValue<double>(0U, 0.0); \
		vec.Y = (float)val.GetChannelValue<double>(1U, 0.0); \
		vec.Z = (float)val.GetChannelValue<double>(2U, 0.0); \
	}

#define GET_CHANNEL_VALUE_VECTOR4(prop, time, vec) \
	{ \
		KFbxAnimCurveNode& val = pAnimEvaluator->GetPropertyValue(prop, time); \
		vec.X = (float)val.GetChannelValue<double>(0U, 0.0); \
		vec.Y = (float)val.GetChannelValue<double>(1U, 0.0); \
		vec.Z = (float)val.GetChannelValue<double>(2U, 0.0); \
		vec.W = (float)val.GetChannelValue<double>(3U, 0.0); \
	}

	void Fbx::InterpolateKeyframes(List<Tuple<ImportedAnimationTrack^, array<xaAnimationKeyframe^>^>^>^ extendedTrackList, int resampleCount)
	{
		KFbxSdkManager* pSdkManager = NULL;
		KFbxScene* pScene = NULL;
		pin_ptr<KFbxSdkManager*> pSdkManagerPin = &pSdkManager;
		pin_ptr<KFbxScene*> pScenePin = &pScene;
		Init(pSdkManagerPin, pScenePin);

		KFbxAnimStack* pAnimStack = KFbxAnimStack::Create(pScene, NULL);
		KFbxAnimLayer* pAnimLayer = KFbxAnimLayer::Create(pScene, NULL);
		pAnimStack->AddMember(pAnimLayer);

		KFbxAnimEvaluator* pAnimEvaluator = pScene->GetEvaluator();

		const KFbxAnimCurveDef::EInterpolationType interpolationMethod = KFbxAnimCurveDef::eINTERPOLATION_LINEAR; // eINTERPOLATION_CUBIC ?

		// S
		const char* pScaleName = "Scale";
		KFbxTypedProperty<fbxDouble3> scale = KFbxProperty::Create(pScene, DTDouble3, pScaleName);
		scale.ModifyFlag(KFbxProperty::eANIMATABLE, true);
		KFbxAnimCurveNode* pScaleCurveNode = scale.GetCurveNode(pAnimLayer, true);
		pAnimLayer->AddMember(pScaleCurveNode);
		scale.ConnectSrcObject(pScaleCurveNode);
		KFbxAnimCurve* pScaleCurveX = pScaleCurveNode->CreateCurve(pScaleName, 0U);
		KFbxAnimCurve* pScaleCurveY = pScaleCurveNode->CreateCurve(pScaleName, 1U);
		KFbxAnimCurve* pScaleCurveZ = pScaleCurveNode->CreateCurve(pScaleName, 2U);

		// R
		const char* pRotateName = "Rotate";
		KFbxTypedProperty<fbxDouble3> rotate = KFbxProperty::Create(pScene, DTDouble3, pRotateName);
		rotate.ModifyFlag(KFbxProperty::eANIMATABLE, true);
		KFbxAnimCurveNode* pRotateCurveNode = rotate.GetCurveNode(pAnimLayer, true);
		pAnimLayer->AddMember(pRotateCurveNode);
		rotate.ConnectSrcObject(pRotateCurveNode);
		KFbxAnimCurve* pRotateCurveX = pRotateCurveNode->CreateCurve(pRotateName, 0U);
		KFbxAnimCurve* pRotateCurveY = pRotateCurveNode->CreateCurve(pRotateName, 1U);
		KFbxAnimCurve* pRotateCurveZ = pRotateCurveNode->CreateCurve(pRotateName, 2U);

		// T
		const char* pTranslateName = "Translate";
		KFbxTypedProperty<fbxDouble3> translate = KFbxProperty::Create(pScene, DTDouble3, pTranslateName);
		translate.ModifyFlag(KFbxProperty::eANIMATABLE, true);
		KFbxAnimCurveNode* pTranslateCurveNode = translate.GetCurveNode(pAnimLayer, true);
		pAnimLayer->AddMember(pTranslateCurveNode);
		translate.ConnectSrcObject(pTranslateCurveNode);
		KFbxAnimCurve* pTranslateCurveX = pTranslateCurveNode->CreateCurve(pTranslateName, 0U);
		KFbxAnimCurve* pTranslateCurveY = pTranslateCurveNode->CreateCurve(pTranslateName, 1U);
		KFbxAnimCurve* pTranslateCurveZ = pTranslateCurveNode->CreateCurve(pTranslateName, 2U);

		KFbxAnimCurve* AllCurves[9] = { pScaleCurveX, pScaleCurveY, pScaleCurveZ, pRotateCurveX, pRotateCurveY, pRotateCurveZ, pTranslateCurveX, pTranslateCurveY, pTranslateCurveZ };

		KTime time;
		float animationLen = (float)(resampleCount - 1);
		for each (Tuple<ImportedAnimationTrack^, array<xaAnimationKeyframe^>^>^ tuple in extendedTrackList)
		{
			ImportedAnimationKeyframedTrack^ wsTrack = (ImportedAnimationKeyframedTrack^)tuple->Item1;
			for (int i = 0; i < wsTrack->Keyframes->Length; i++)
			{
				ImportedAnimationKeyframe^ wsKeyframe = wsTrack->Keyframes[i];
				if (wsKeyframe == nullptr)
					continue;

				float timePos = i * animationLen / (wsTrack->Keyframes->Length - 1);
				time.SetSecondDouble(timePos);

				Vector3 s = wsKeyframe->Scaling;
				ADD_KEY_VECTOR3(pScaleCurveX, pScaleCurveY, pScaleCurveZ, time, s, interpolationMethod);
				Vector3 r = QuaternionToEuler(wsKeyframe->Rotation);
				ADD_KEY_VECTOR3(pRotateCurveX, pRotateCurveY, pRotateCurveZ, time, r, interpolationMethod);
				Vector3 t = wsKeyframe->Translation;
				ADD_KEY_VECTOR3(pTranslateCurveX, pTranslateCurveY, pTranslateCurveZ, time, t, interpolationMethod);
			}

			array<xaAnimationKeyframe^>^ newKeyframes = tuple->Item2;
			for (int i = 0; i < newKeyframes->Length; i++)
			{
				newKeyframes[i] = gcnew xaAnimationKeyframe();
				newKeyframes[i]->Index = i;
				xa::CreateUnknowns(newKeyframes[i]);

				time.SetSecondDouble(i);
				Vector3 s, r, t;
				GET_CHANNEL_VALUE_VECTOR3(scale, time, s);
				newKeyframes[i]->Scaling = s;
				GET_CHANNEL_VALUE_VECTOR3(rotate, time, r);
				newKeyframes[i]->Rotation = EulerToQuaternion(r);
				GET_CHANNEL_VALUE_VECTOR3(translate, time, t);
				newKeyframes[i]->Translation = t;
			}

			for each (KFbxAnimCurve* pCurve in AllCurves)
			{
				pCurve->KeyClear();
			}
		}

		if (pScene != NULL)
		{
			pScene->Destroy();
		}
		if (pSdkManager != NULL)
		{
			pSdkManager->Destroy();
		}
	}

	void Fbx::InterpolateKeyframes(List<Tuple<ImportedAnimationTrack^, array<ImportedAnimationKeyframe^>^>^>^ extendedTrackList, int resampleCount, bool linear)
	{
		KFbxSdkManager* pSdkManager = NULL;
		KFbxScene* pScene = NULL;
		pin_ptr<KFbxSdkManager*> pSdkManagerPin = &pSdkManager;
		pin_ptr<KFbxScene*> pScenePin = &pScene;
		Init(pSdkManagerPin, pScenePin);

		KFbxAnimStack* pAnimStack = KFbxAnimStack::Create(pScene, NULL);
		KFbxAnimLayer* pAnimLayer = KFbxAnimLayer::Create(pScene, NULL);
		pAnimStack->AddMember(pAnimLayer);

		KFbxTypedProperty<fbxDouble3> scale = KFbxProperty::Create(pScene, DTDouble3, InterpolationHelper::pScaleName);
		KFbxTypedProperty<fbxDouble4> rotate = KFbxProperty::Create(pScene, DTDouble4, InterpolationHelper::pRotateName);
		KFbxTypedProperty<fbxDouble3> translate = KFbxProperty::Create(pScene, DTDouble3, InterpolationHelper::pTranslateName);
		InterpolationHelper^ interpolationHelper = gcnew InterpolationHelper(pScene, pAnimLayer, linear ? KFbxAnimCurveDef::eINTERPOLATION_LINEAR : KFbxAnimCurveDef::eINTERPOLATION_CUBIC, &scale, &rotate, &translate);

		KTime time;
		float animationLen = (float)(resampleCount - 1);
		for each (Tuple<ImportedAnimationTrack^, array<ImportedAnimationKeyframe^>^>^ tuple in extendedTrackList)
		{
			ImportedAnimationKeyframedTrack^ wsTrack = (ImportedAnimationKeyframedTrack^)tuple->Item1;
			array<ImportedAnimationKeyframe^>^ interpolatedKeyframes = interpolationHelper->InterpolateTrack(wsTrack->Keyframes, resampleCount);
			array<ImportedAnimationKeyframe^>^ newKeyframes = tuple->Item2;
			interpolatedKeyframes->CopyTo(newKeyframes, 0);
		}

		if (pScene != NULL)
		{
			pScene->Destroy();
		}
		if (pSdkManager != NULL)
		{
			pSdkManager->Destroy();
		}
	}

	void Fbx::InterpolateSampledTracks(List<Tuple<ImportedAnimationTrack^, ImportedAnimationSampledTrack^>^>^ extendedTrackList, int resampleCount, bool linear)
	{
		KFbxSdkManager* pSdkManager = NULL;
		KFbxScene* pScene = NULL;
		pin_ptr<KFbxSdkManager*> pSdkManagerPin = &pSdkManager;
		pin_ptr<KFbxScene*> pScenePin = &pScene;
		Init(pSdkManagerPin, pScenePin);

		KFbxAnimStack* pAnimStack = KFbxAnimStack::Create(pScene, NULL);
		KFbxAnimLayer* pAnimLayer = KFbxAnimLayer::Create(pScene, NULL);
		pAnimStack->AddMember(pAnimLayer);

		KFbxTypedProperty<fbxDouble3> scale = KFbxProperty::Create(pScene, DTDouble3, InterpolationHelper::pScaleName);
		KFbxTypedProperty<fbxDouble4> rotate = KFbxProperty::Create(pScene, DTDouble4, InterpolationHelper::pRotateName);
		KFbxTypedProperty<fbxDouble3> translate = KFbxProperty::Create(pScene, DTDouble3, InterpolationHelper::pTranslateName);
		InterpolationHelper^ interpolationHelper = gcnew InterpolationHelper(pScene, pAnimLayer, linear ? KFbxAnimCurveDef::eINTERPOLATION_LINEAR : KFbxAnimCurveDef::eINTERPOLATION_CUBIC, &scale, &rotate, &translate);

		KTime time;
		float animationLen = (float)(resampleCount - 1);
		for each (Tuple<ImportedAnimationTrack^, ImportedAnimationSampledTrack^>^ tuple in extendedTrackList)
		{
			ImportedAnimationSampledTrack^ wsTrack = (ImportedAnimationSampledTrack^)tuple->Item1;
			ImportedAnimationSampledTrack^ interpolatedSampledTrack = interpolationHelper->InterpolateTrack(wsTrack, resampleCount);
			ImportedAnimationSampledTrack^ newSampledTrack = tuple->Item2;
			newSampledTrack->Scalings = interpolatedSampledTrack->Scalings;
			newSampledTrack->Rotations = interpolatedSampledTrack->Rotations;
			newSampledTrack->Translations = interpolatedSampledTrack->Translations;
		}

		if (pScene != NULL)
		{
			pScene->Destroy();
		}
		if (pSdkManager != NULL)
		{
			pSdkManager->Destroy();
		}
	}

	Fbx::InterpolationHelper::InterpolationHelper(KFbxScene* scene, KFbxAnimLayer* layer, KFbxAnimCurveDef::EInterpolationType interpolationMethod,
			KFbxTypedProperty<fbxDouble3>* scale, KFbxTypedProperty<fbxDouble4>* rotate, KFbxTypedProperty<fbxDouble3>* translate)
	{
		pScene = scene;
		pAnimLayer = layer;

		pAnimEvaluator = pScene->GetEvaluator();

		this->interpolationMethod = interpolationMethod;

		// S
		this->scale = scale;
		scale->ModifyFlag(KFbxProperty::eANIMATABLE, true);
		KFbxAnimCurveNode* pScaleCurveNode = scale->GetCurveNode(pAnimLayer, true);
		pAnimLayer->AddMember(pScaleCurveNode);
		scale->ConnectSrcObject(pScaleCurveNode);
		pScaleCurveX = pScaleCurveNode->CreateCurve(pScaleName, 0U);
		pScaleCurveY = pScaleCurveNode->CreateCurve(pScaleName, 1U);
		pScaleCurveZ = pScaleCurveNode->CreateCurve(pScaleName, 2U);

		// R
		this->rotate = rotate;
		rotate->ModifyFlag(KFbxProperty::eANIMATABLE, true);
		KFbxAnimCurveNode* pRotateCurveNode = rotate->GetCurveNode(pAnimLayer, true);
		pAnimLayer->AddMember(pRotateCurveNode);
		rotate->ConnectSrcObject(pRotateCurveNode);
		pRotateCurveX = pRotateCurveNode->CreateCurve(pRotateName, 0U);
		pRotateCurveY = pRotateCurveNode->CreateCurve(pRotateName, 1U);
		pRotateCurveZ = pRotateCurveNode->CreateCurve(pRotateName, 2U);
		pRotateCurveW = pRotateCurveNode->CreateCurve(pRotateName, 3U);

		// T
		this->translate = translate;
		translate->ModifyFlag(KFbxProperty::eANIMATABLE, true);
		KFbxAnimCurveNode* pTranslateCurveNode = translate->GetCurveNode(pAnimLayer, true);
		pAnimLayer->AddMember(pTranslateCurveNode);
		translate->ConnectSrcObject(pTranslateCurveNode);
		pTranslateCurveX = pTranslateCurveNode->CreateCurve(pTranslateName, 0U);
		pTranslateCurveY = pTranslateCurveNode->CreateCurve(pTranslateName, 1U);
		pTranslateCurveZ = pTranslateCurveNode->CreateCurve(pTranslateName, 2U);

		allCurves = gcnew array<KFbxAnimCurve*>(10) { pScaleCurveX, pScaleCurveY, pScaleCurveZ, pRotateCurveX, pRotateCurveY, pRotateCurveZ, pRotateCurveW, pTranslateCurveX, pTranslateCurveY, pTranslateCurveZ };
	}

	List<xaAnimationKeyframe^>^ Fbx::InterpolationHelper::InterpolateTrack(List<xaAnimationKeyframe^>^ keyframes, int resampleCount)
	{
		KTime time;
		float animationLen = (float)(resampleCount - 1);

		int startIdx = keyframes[0]->Index;
		int endIdx = keyframes[keyframes->Count - 1]->Index;
		for (int i = 0; i < keyframes->Count; i++)
		{
			xaAnimationKeyframe^ keyframe = keyframes[i];
			float timePos = (float)(keyframe->Index - startIdx);
			time.SetSecondDouble(timePos);

			Vector3 s = keyframe->Scaling;
			ADD_KEY_VECTOR3(pScaleCurveX, pScaleCurveY, pScaleCurveZ, time, s, interpolationMethod);
			Vector3 r = QuaternionToEuler(keyframe->Rotation);
			ADD_KEY_VECTOR3(pRotateCurveX, pRotateCurveY, pRotateCurveZ, time, r, interpolationMethod);
			Vector3 t = keyframe->Translation;
			ADD_KEY_VECTOR3(pTranslateCurveX, pTranslateCurveY, pTranslateCurveZ, time, t, interpolationMethod);
		}

		List<xaAnimationKeyframe^>^ newKeyframes = gcnew List<xaAnimationKeyframe^>(resampleCount);
		for (int i = 0; i < resampleCount; i++)
		{
			xaAnimationKeyframe^ newKeyframe = gcnew xaAnimationKeyframe();
			newKeyframe->Index = i + startIdx;
			xa::CreateUnknowns(newKeyframe);

			time.SetSecondDouble(i * (endIdx - startIdx) / animationLen);
			Vector3 s, r, t;
			GET_CHANNEL_VALUE_VECTOR3(*scale, time, s);
			newKeyframe->Scaling = s;
			GET_CHANNEL_VALUE_VECTOR3(*rotate, time, r);
			newKeyframe->Rotation = EulerToQuaternion(r);
			GET_CHANNEL_VALUE_VECTOR3(*translate, time, t);
			newKeyframe->Translation = t;

			newKeyframes->Add(newKeyframe);
		}

		for each (KFbxAnimCurve* pCurve in allCurves)
		{
			pCurve->KeyClear();
		}

		return newKeyframes;
	}

	array<ImportedAnimationKeyframe^>^ Fbx::InterpolationHelper::InterpolateTrack(array<ImportedAnimationKeyframe^>^ keyframes, int resampleCount)
	{
		KTime time;
		float animationLen = (float)(resampleCount - 1);

		int endIdx = keyframes->Length - 1;
		for (int i = 0; i < keyframes->Length; i++)
		{
			ImportedAnimationKeyframe^ keyframe = keyframes[i];
			if (keyframe == nullptr)
				continue;

			time.SetSecondDouble(i);

			ADD_KEY_VECTOR3(pScaleCurveX, pScaleCurveY, pScaleCurveZ, time, keyframe->Scaling, interpolationMethod);
			ADD_KEY_VECTOR4(pRotateCurveX, pRotateCurveY, pRotateCurveZ, pRotateCurveW, time, keyframe->Rotation, interpolationMethod);
			ADD_KEY_VECTOR3(pTranslateCurveX, pTranslateCurveY, pTranslateCurveZ, time, keyframe->Translation, interpolationMethod);
		}

		array<ImportedAnimationKeyframe^>^ newKeyframes = gcnew array<ImportedAnimationKeyframe^>(resampleCount);
		for (int i = 0; i < resampleCount; i++)
		{
			ImportedAnimationKeyframe^ newKeyframe = gcnew ImportedAnimationKeyframe();

			time.SetSecondDouble(i * endIdx / animationLen);
			Vector3 s, t;
			GET_CHANNEL_VALUE_VECTOR3(*scale, time, s);
			newKeyframe->Scaling = s;
			Quaternion q;
			GET_CHANNEL_VALUE_VECTOR4(*rotate, time, q);
			newKeyframe->Rotation = q;
			GET_CHANNEL_VALUE_VECTOR3(*translate, time, t);
			newKeyframe->Translation = t;

			newKeyframes[i] = newKeyframe;
		}

		for each (KFbxAnimCurve* pCurve in allCurves)
		{
			pCurve->KeyClear();
		}

		return newKeyframes;
	}

	ImportedAnimationSampledTrack^ Fbx::InterpolationHelper::InterpolateTrack(ImportedAnimationSampledTrack^ track, int resampleCount)
	{
		KTime time;
		float animationLen = (float)(resampleCount - 1);

		array<Nullable<Vector3>>^ scalings = track->Scalings;
		int endIdx = scalings->Length - 1;
		for (int i = 0; i <= endIdx; i++)
		{
			if (!scalings[i].HasValue)
				continue;

			time.SetSecondDouble(i);

			Vector3 s = scalings[i].Value;
			ADD_KEY_VECTOR3(pScaleCurveX, pScaleCurveY, pScaleCurveZ, time, s, interpolationMethod);
		}

		array<Nullable<Quaternion>>^ rotations = track->Rotations;
		endIdx = rotations->Length - 1;
		Quaternion lastQ = Quaternion::Identity;
		for (int i = 0, lastUsed_keyIndex = -1; i <= endIdx; i++)
		{
			if (!rotations[i].HasValue)
				continue;

			time.SetSecondDouble(i);

			Quaternion q = rotations[i].Value;
//			if (negateQuaternionFlips)
			{
				if (lastUsed_keyIndex >= 0)
				{
					bool diffZ = Math::Sign(lastQ.Z) != Math::Sign(q.Z);
					bool diffW = Math::Sign(lastQ.W) != Math::Sign(q.W);
					if (diffZ && diffW)
					{
						q.X = -q.X;
						q.Y = -q.Y;
						q.Z = -q.Z;
						q.W = -q.W;
					}
				}
				lastQ = q;
				lastUsed_keyIndex = i;
			}
			ADD_KEY_VECTOR4(pRotateCurveX, pRotateCurveY, pRotateCurveZ, pRotateCurveW, time, q, interpolationMethod);
		}

		array<Nullable<Vector3>>^ translations = track->Translations;
		endIdx = translations->Length - 1;
		for (int i = 0; i <= endIdx; i++)
		{
			if (!translations[i].HasValue)
				continue;

			time.SetSecondDouble(i);

			Vector3 t = translations[i].Value;
			ADD_KEY_VECTOR3(pTranslateCurveX, pTranslateCurveY, pTranslateCurveZ, time, t, interpolationMethod);
		}

		ImportedAnimationSampledTrack^ newTrack = gcnew ImportedAnimationSampledTrack();
		newTrack->Scalings = gcnew array<Nullable<Vector3>>(resampleCount);
		newTrack->Rotations = gcnew array<Nullable<Quaternion>>(resampleCount);
		newTrack->Translations = gcnew array<Nullable<Vector3>>(resampleCount);
		for (int i = 0; i < resampleCount; i++)
		{
			time.SetSecondDouble(i * endIdx / animationLen);
			Vector3 s, t;
			GET_CHANNEL_VALUE_VECTOR3(*scale, time, s);
			newTrack->Scalings[i] = s;
			Quaternion q;
			GET_CHANNEL_VALUE_VECTOR4(*rotate, time, q);
			newTrack->Rotations[i] = q;
			GET_CHANNEL_VALUE_VECTOR3(*translate, time, t);
			newTrack->Translations[i] = t;
		}

		for each (KFbxAnimCurve* pCurve in allCurves)
		{
			pCurve->KeyClear();
		}

		return newTrack;
	}
}