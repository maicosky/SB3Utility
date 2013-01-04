using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;

namespace SB3Utility
{
	[Plugin]
	public class xaEditor : IDisposable
	{
		public xaParser Parser { get; protected set; }

		public xaEditor(xaParser parser)
		{
			Parser = parser;
		}

		public void Dispose()
		{
			Parser = null;
		}

		[Plugin]
		public void SetMorphClipName(int position, string newName)
		{
			string oldName = Parser.MorphSection.ClipList[position].Name;
			xaMorphIndexSet set = xa.FindMorphIndexSet(oldName, Parser.MorphSection);
			set.Name = newName;
			Parser.MorphSection.ClipList[position].Name = newName;
		}

		[Plugin]
		public void SetMorphClipMesh(int position, string mesh)
		{
			Parser.MorphSection.ClipList[position].MeshName = mesh;
		}

		[Plugin]
		public void ReplaceMorph(WorkspaceMorph morph, string destMorphName, string newName, bool replaceNormals, double minSquaredDistance)
		{
			xa.ReplaceMorph(destMorphName, Parser, morph, newName, replaceNormals, (float)minSquaredDistance);
		}

		[Plugin]
		public void CalculateNormals(xxFrame meshFrame, string morphClip, string keyframe, double threshold)
		{
			xa.CalculateNormals(Parser, meshFrame, morphClip, keyframe, (float)threshold);
		}

		[Plugin]
		public void CreateMorphKeyframeRef(string morphClip, int position, string keyframe)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					xaMorphKeyframeRef morphRef = new xaMorphKeyframeRef();
					xa.CreateUnknowns(morphRef);
					morphRef.Index = -1;
					morphRef.Name = keyframe;
					clip.KeyframeRefList.Insert(position, morphRef);
					return;
				}
			}
		}

		[Plugin]
		public void RemoveMorphKeyframeRef(string morphClip, int position)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					clip.KeyframeRefList.RemoveAt(position);
					return;
				}
			}
		}

		[Plugin]
		public void MoveMorphKeyframeRef(string morphClip, int fromPos, int toPos)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					xaMorphKeyframeRef morphRef = clip.KeyframeRefList[fromPos];
					clip.KeyframeRefList.RemoveAt(fromPos);
					clip.KeyframeRefList.Insert(toPos, morphRef);
					return;
				}
			}
		}

		[Plugin]
		public void RemoveMorphKeyframe(string name)
		{
			xaMorphKeyframe keyframe = xa.FindMorphKeyFrame(name, Parser.MorphSection);
			Parser.MorphSection.KeyframeList.Remove(keyframe);
		}

		[Plugin]
		public void SetMorphKeyframeRefKeyframe(string morphClip, int position, string keyframe)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					clip.KeyframeRefList[position].Name = keyframe;
					return;
				}
			}
		}

		[Plugin]
		public void SetMorphKeyframeRefIndex(string morphClip, int position, int id)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					clip.KeyframeRefList[position].Index = id;
					return;
				}
			}
		}

		[Plugin]
		public void RenameMorphKeyframe(int position, string newName)
		{
			xaMorphKeyframe keyframe = Parser.MorphSection.KeyframeList[position];
			string oldName = keyframe.Name;
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				foreach (xaMorphKeyframeRef morphRef in clip.KeyframeRefList)
				{
					if (morphRef.Name == oldName)
					{
						morphRef.Name = newName;
					}
				}
			}
			keyframe.Name = newName;
		}

		[Plugin]
		public void ReplaceAnimation(WorkspaceAnimation animation, int resampleCount, string method, int insertPos)
		{
			var replaceMethod = (ReplaceAnimationMethod)Enum.Parse(typeof(ReplaceAnimationMethod), method);
			ReplaceAnimation(animation, Parser, resampleCount, replaceMethod, insertPos);
		}

		public static void ReplaceAnimation(WorkspaceAnimation wsAnimation, xaParser parser, int resampleCount, ReplaceAnimationMethod replaceMethod, int insertPos)
		{
			if (parser.AnimationSection == null)
			{
				Report.ReportLog("The .xa file doesn't have an animation section. Skipping this animation");
				return;
			}

			Report.ReportLog("Replacing animation ...");
			List<KeyValuePair<string, xaAnimationKeyframe[]>> newTrackList = new List<KeyValuePair<string, xaAnimationKeyframe[]>>(wsAnimation.TrackList.Count);
			List<Tuple<ImportedAnimationTrack, xaAnimationKeyframe[]>> interpolateTracks = new List<Tuple<ImportedAnimationTrack,xaAnimationKeyframe[]>>();
			foreach (var wsTrack in wsAnimation.TrackList)
			{
				if (!wsAnimation.isTrackEnabled(wsTrack))
					continue;
				xaAnimationKeyframe[] newKeyframes = null;
				int wsTrackKeyframesLength = 0;
				for (int i = 0; i < wsTrack.Keyframes.Length; i++)
				{
					if (wsTrack.Keyframes[i] != null)
						wsTrackKeyframesLength++;
				}
				if (resampleCount < 0 || wsTrackKeyframesLength == resampleCount)
				{
					newKeyframes = new xaAnimationKeyframe[wsTrackKeyframesLength];
					int keyframeIdx = 0;
					for (int i = 0; i < wsTrack.Keyframes.Length; i++)
					{
						ImportedAnimationKeyframe keyframe = wsTrack.Keyframes[i];
						if (keyframe == null)
							continue;

						newKeyframes[keyframeIdx] = new xaAnimationKeyframe();
						newKeyframes[keyframeIdx].Index = i;
						newKeyframes[keyframeIdx].Rotation = keyframe.Rotation;
						xa.CreateUnknowns(newKeyframes[keyframeIdx]);
						newKeyframes[keyframeIdx].Translation = keyframe.Translation;
						newKeyframes[keyframeIdx].Scaling = keyframe.Scaling;
						keyframeIdx++;
					}
				}
				else
				{
					newKeyframes = new xaAnimationKeyframe[resampleCount];
					if (wsTrackKeyframesLength < 1)
					{
						xaAnimationKeyframe keyframe = new xaAnimationKeyframe();
						keyframe.Rotation = Quaternion.Identity;
						keyframe.Scaling = new Vector3(1, 1, 1);
						keyframe.Translation = new Vector3(0, 0, 0);
						xa.CreateUnknowns(keyframe);

						for (int i = 0; i < newKeyframes.Length; i++)
						{
							keyframe.Index = i;
							newKeyframes[i] = keyframe;
						}
					}
					else
					{
						interpolateTracks.Add(new Tuple<ImportedAnimationTrack, xaAnimationKeyframe[]>(wsTrack, newKeyframes));
					}
				}

				newTrackList.Add(new KeyValuePair<string, xaAnimationKeyframe[]>(wsTrack.Name, newKeyframes));
			}
			if (interpolateTracks.Count > 0)
			{
				Fbx.InterpolateKeyframes(interpolateTracks, resampleCount);
			}

			List<xaAnimationTrack> animationNodeList = parser.AnimationSection.TrackList;
			Dictionary<string, xaAnimationTrack> animationNodeDic = null;
			if (replaceMethod != ReplaceAnimationMethod.Replace)
			{
				animationNodeDic = new Dictionary<string, xaAnimationTrack>();
				foreach (xaAnimationTrack animationNode in animationNodeList)
				{
					animationNodeDic.Add(animationNode.Name, animationNode);
				}
			}

			if (replaceMethod == ReplaceAnimationMethod.Replace)
			{
				animationNodeList.Clear();
				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode = new xaAnimationTrack();
					animationNodeList.Add(animationNode);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(newTrack.Value);
					animationNode.Name = newTrack.Key;
					xa.CreateUnknowns(animationNode);
				}
			}
			else if (replaceMethod == ReplaceAnimationMethod.ReplacePresent)
			{
				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode = xa.animationGetOriginalKeyframes(animationNodeDic, newTrack.Key, animationNodeList);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(newTrack.Value);
				}
			}
			else if (replaceMethod == ReplaceAnimationMethod.Merge)
			{
				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode;
					xaAnimationKeyframe[] origKeyframes = xa.animationGetOriginalKeyframes(animationNodeDic, newTrack.Key, animationNodeList, out animationNode);
					xaAnimationKeyframe[] destKeyframes;
					int newEnd = insertPos + newTrack.Value.Length;
					if (origKeyframes.Length < insertPos)
					{
						destKeyframes = new xaAnimationKeyframe[newEnd];
						xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, origKeyframes.Length);
						xa.animationNormalizeTrack(origKeyframes, destKeyframes, insertPos);
					}
					else
					{
						if (origKeyframes.Length < newEnd)
						{
							destKeyframes = new xaAnimationKeyframe[newEnd];
						}
						else
						{
							destKeyframes = new xaAnimationKeyframe[origKeyframes.Length];
							xa.animationCopyKeyframeTransformArray(origKeyframes, newEnd, destKeyframes, newEnd, origKeyframes.Length - newEnd);
						}
						xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, insertPos);
					}

					xa.animationCopyKeyframeTransformArray(newTrack.Value, 0, destKeyframes, insertPos, newTrack.Value.Length);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(destKeyframes);
				}
			}
			else if (replaceMethod == ReplaceAnimationMethod.Insert)
			{
				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode;
					xaAnimationKeyframe[] origKeyframes = xa.animationGetOriginalKeyframes(animationNodeDic, newTrack.Key, animationNodeList, out animationNode); ;
					xaAnimationKeyframe[] destKeyframes;
					int newEnd = insertPos + newTrack.Value.Length;
					if (origKeyframes.Length < insertPos)
					{
						destKeyframes = new xaAnimationKeyframe[newEnd];
						xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, origKeyframes.Length);
						xa.animationNormalizeTrack(origKeyframes, destKeyframes, insertPos);
					}
					else
					{
						destKeyframes = new xaAnimationKeyframe[origKeyframes.Length + newTrack.Value.Length];
						xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, insertPos);
						xa.animationCopyKeyframeTransformArray(origKeyframes, insertPos, destKeyframes, newEnd, origKeyframes.Length - insertPos);
					}

					xa.animationCopyKeyframeTransformArray(newTrack.Value, 0, destKeyframes, insertPos, newTrack.Value.Length);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(destKeyframes);
				}
			}
			else if (replaceMethod == ReplaceAnimationMethod.Append)
			{
				int maxKeyframes = 0;
				foreach (xaAnimationTrack animationNode in animationNodeList)
				{
					int numKeyframes = animationNode.KeyframeList[animationNode.KeyframeList.Count - 1].Index;
					if (numKeyframes > maxKeyframes)
					{
						maxKeyframes = numKeyframes;
					}
				}

				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode;
					xaAnimationKeyframe[] origKeyframes = xa.animationGetOriginalKeyframes(animationNodeDic, newTrack.Key, animationNodeList, out animationNode);
					xaAnimationKeyframe[] destKeyframes = new xaAnimationKeyframe[maxKeyframes + insertPos + newTrack.Value[newTrack.Value.Length - 1].Index + 1];
					xa.animationCopyKeyframeTransformArray(origKeyframes, destKeyframes, 0);
					if (origKeyframes.Length > 0 && origKeyframes.Length == origKeyframes[origKeyframes.Length - 1].Index + 1)
					{
						xa.animationNormalizeTrack(origKeyframes, destKeyframes, origKeyframes.Length + insertPos);
					}
					xa.animationCopyKeyframeTransformArray(newTrack.Value, destKeyframes, maxKeyframes + insertPos);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(origKeyframes.Length + insertPos + newTrack.Value.Length);
					for (int i = 0; i < destKeyframes.Length; i++)
					{
						if (destKeyframes[i] == null)
							continue;

						animationNode.KeyframeList.Add(destKeyframes[i]);
					}
				}
			}
			else
			{
				Report.ReportLog("Error: Unexpected animation replace method " + replaceMethod + ". Skipping this animation");
				return;
			}
		}

		[Plugin]
		public void SetAnimationClip(xaAnimationClip clip, string name, int start, int end, int next, double speed)
		{
			clip.Name = name;
			clip.Start = start;
			clip.End = end;
			clip.Next = next;
			clip.Speed = (float)speed;
		}

		[Plugin]
		public void MoveAnimationClip(xaAnimationClip clip, int position)
		{
			Parser.AnimationSection.ClipList.Remove(clip);
			Parser.AnimationSection.ClipList.Insert(position, clip);
		}

		[Plugin]
		public void CopyAnimationClip(xaAnimationClip clip, int position)
		{
			xaAnimationClip newClip = Parser.AnimationSection.ClipList[position];
			newClip.Name = clip.Name;
			newClip.Start = clip.Start;
			newClip.End = clip.End;
			newClip.Next = clip.Next;
			newClip.Speed = clip.Speed;
			xa.CopyUnknowns(clip, newClip);
		}

		[Plugin]
		public void DeleteAnimationClip(xaAnimationClip clip)
		{
			clip.Name = String.Empty;
			clip.Start = 0;
			clip.End = 0;
			clip.Next = 0;
			clip.Speed = 0;
			xa.CreateUnknowns(clip);
		}
	}
}
