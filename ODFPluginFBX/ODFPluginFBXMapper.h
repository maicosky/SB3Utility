// ODFPluginFBXMapper.h

#pragma once

using namespace System;

namespace ODFPlugin {

	class W32
	{
	public:
		static String^ GetShortPathNameW(String^ longPath);
	};
}
