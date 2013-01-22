
namespace AiDroidPlugin
{
	public static partial class rea
	{
		public static reaAnimationTrack FindTrack(remId trackName, reaParser parser)
		{
			foreach (reaAnimationTrack track in parser.ANIC)
			{
				if (track.boneFrame == trackName)
				{
					return track;
				}
			}

			return null;
		}
	}
}
