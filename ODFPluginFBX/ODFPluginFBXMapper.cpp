#include <windows.h>
#include <wchar.h>

using namespace System::Runtime::InteropServices;

#include "ODFPluginFBXMapper.h"

namespace ODFPluginOld
{
	String^ W32::GetShortPathNameW(String^ path)
	{
		WCHAR* longPath = (WCHAR*)Marshal::StringToHGlobalUni(path).ToPointer();
		WCHAR* shortPath = (WCHAR*)Marshal::AllocHGlobal(1000).ToPointer();
		try
		{
			DWORD len = ::GetShortPathNameW(longPath, shortPath, 1000 / sizeof(TCHAR));
			return gcnew String(shortPath);
		}
		finally
		{
			Marshal::FreeHGlobal((IntPtr)shortPath);
			Marshal::FreeHGlobal((IntPtr)longPath);
		}
	}
}