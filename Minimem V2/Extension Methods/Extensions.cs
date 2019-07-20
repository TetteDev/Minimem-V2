using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Minimem.Enumerations;

namespace Minimem.Extension_Methods
{
	public static class Extensions
	{
		public static Main _mainReference;
	}

	public static class StringExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (string left, string right) SplitAt(this string text, int index) =>
			(text.Substring(0, index), text.Substring(index));
	}
	public static class ProcessExtensions
	{
		public static List<ProcessModule> ProcessModules(this Process processObject)
		{
			return processObject.Modules.Cast<ProcessModule>().ToList();
		}

		public static bool Suspend(this Process processObject)
		{
			if (Extensions._mainReference == null) return false;
			IntPtr _handle = Extensions._mainReference.ProcessObject == processObject ? Extensions._mainReference.ProcessHandle 
				: Win32.PInvoke.OpenProcess(ProcessAccessFlags.Enumeration.All, false, processObject.Id);

			if (_handle == IntPtr.Zero) return false;
			foreach (ProcessThread processThread in processObject.Threads)
			{
				IntPtr threadHandle = Win32.PInvoke.OpenThread(Classes.ThreadAccess.SUSPEND_RESUME, false, (uint)processThread.Id);
				if (threadHandle == IntPtr.Zero)
					continue;
				Win32.PInvoke.SuspendThread(threadHandle);
				Win32.PInvoke.CloseHandle(threadHandle);
			}

			Win32.PInvoke.CloseHandle(_handle);
			return true;
		}

		public static bool Resume(this Process processObject)
		{
			if (Extensions._mainReference == null) return false;
			IntPtr _handle = Extensions._mainReference.ProcessObject == processObject ? Extensions._mainReference.ProcessHandle
				: Win32.PInvoke.OpenProcess(ProcessAccessFlags.Enumeration.All, false, processObject.Id);
			if (_handle == IntPtr.Zero) return false;

			foreach (ProcessThread processThread in processObject.Threads)
			{
				IntPtr threadHandle = Win32.PInvoke.OpenThread(Classes.ThreadAccess.SUSPEND_RESUME, false, (uint)processThread.Id);
				if (threadHandle == IntPtr.Zero)
					continue;

				var suspendCount = 0;
				do
				{
					suspendCount = Win32.PInvoke.ResumeThread(threadHandle);
				} while (suspendCount > 0);

				Win32.PInvoke.CloseHandle(threadHandle);
			}

			Win32.PInvoke.CloseHandle(_handle);
			return true;
		}

		public static ProcessModule FindProcessModule(this Process processObject, string moduleName, bool sloppySearch = false)
		{
			if (string.IsNullOrEmpty(moduleName)) throw new Exception($"Module Name cannot be null or empty!");

			foreach (ProcessModule pm in processObject.Modules)
			{
				if (sloppySearch && (pm.ModuleName.ToLower().Contains(moduleName.ToLower()) || pm.ModuleName.ToLower().StartsWith(moduleName.ToLower())))
					return pm;
				else if (string.Equals(pm.ModuleName, moduleName, StringComparison.CurrentCultureIgnoreCase)) return pm;
			}
			throw new Exception($"Cannot find any process module with name \"{moduleName}\"");
		}

		public static bool Is64Bit(this Process processObject, bool bOpenHandleIfNeeded = false)
		{
			if (Extensions._mainReference == null) return false;
			if (!Extensions._mainReference.IsValid && bOpenHandleIfNeeded)
			{
				IntPtr _handle = Win32.PInvoke.OpenProcess(ProcessAccessFlags.Enumeration.All, false, processObject.Id);
				if (_handle == IntPtr.Zero) return false;

				bool flag_1 = Environment.Is64BitOperatingSystem && (Win32.PInvoke.IsWow64Process(_handle, out bool retVal_1) && !retVal_1);
				Win32.PInvoke.CloseHandle(_handle);
				return flag_1;
			}

			bool flag = Environment.Is64BitOperatingSystem && (Win32.PInvoke.IsWow64Process(Extensions._mainReference.ProcessHandle, out bool retVal) && !retVal);
			return flag;
		}

	}

	public static class ProcessModuleExtensions
	{
		public static IntPtr EndAddress(this ProcessModule processModuleObject)
		{
#if x86
			return new IntPtr(processModuleObject.BaseAddress.ToInt32() + processModuleObject.ModuleMemorySize);
#else
			return new IntPtr(processModuleObject.BaseAddress.ToInt64() + processModuleObject.ModuleMemorySize);
#endif
		}
	}
}
