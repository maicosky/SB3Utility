using System;

using SB3Utility;

namespace AiDroidPlugin
{
	[Plugin]
	public class reaEditor : IDisposable
	{
		public reaParser Parser { get; protected set; }

		public reaEditor(reaParser parser)
		{
			Parser = parser;
		}

		public void Dispose()
		{
			Parser = null;
		}

		[Plugin]
		public void RenameTrack(string track, string newName)
		{
			reaAnimationTrack reaTrack = rea.FindTrack(new remId(track), Parser);
			reaTrack.boneFrame = new remId(newName);
		}

		[Plugin]
		public void RemoveTrack(string track)
		{
			reaAnimationTrack reaTrack = rea.FindTrack(new remId(track), Parser);
			Parser.ANIC.RemoveChild(reaTrack);
		}
	}
}
