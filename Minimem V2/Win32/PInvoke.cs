using System;
using System.Runtime.InteropServices;

namespace Minimem.Win32
{
	public class PInvoke
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr OpenProcess(Enumerations.ProcessAccessFlags.Enumeration processAccess,bool bInheritHandle,int processId);

		[DllImport("kernel32.dll", SetLastError = true)]
#if x86
		public static extern bool ReadProcessMemory(IntPtr hProcess,IntPtr lpBaseAddress,[Out] byte[] lpBuffer,int dwSize,out IntPtr lpNumberOfBytesRead);
#else
		public static extern bool ReadProcessMemory(IntPtr hProcess,IntPtr lpBaseAddress,[Out] byte[] lpBuffer,long dwSize,out IntPtr lpNumberOfBytesRead);
#endif
		[DllImport("kernel32.dll", SetLastError = true)]
#if x86
		public static extern bool WriteProcessMemory(IntPtr hProcess,IntPtr lpBaseAddress,byte[] lpBuffer,Int32 nSize,out IntPtr lpNumberOfBytesWritten);
#else
		public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, Int64 nSize, out IntPtr lpNumberOfBytesWritten);
#endif

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool CloseHandle(IntPtr hHandle);

		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
#if x86
		public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,uint dwSize, Classes.AllocationType flAllocationType, Classes.MemoryProtection flProtect);
#else
		public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, ulong dwSize, Classes.AllocationType flAllocationType, Classes.MemoryProtection flProtect);
#endif

		[DllImport("kernel32.dll")]
		public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, Classes.MemoryProtection flNewProtect, out Classes.MemoryProtection lpflOldProtect);

		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
#if x86
		public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress,int dwSize, Classes.AllocationType dwFreeType);
#else
		public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, long dwSize, Classes.AllocationType dwFreeType);
#endif

		[DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr GetModuleHandle(string lpModuleName);

		[DllImport("kernel32.dll")]
		public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress,IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

		[DllImport("kernel32.dll")]
		public static extern IntPtr OpenThread(Classes.ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

		[DllImport("kernel32.dll")]
		public static extern int ResumeThread(IntPtr hThread);

		[DllImport("kernel32.dll")]
		public static extern uint SuspendThread(IntPtr hThread);

		[DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWow64Process([In] IntPtr processHandle,
			[Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress,
			UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

		[DllImport("kernel32.dll")]
		public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

		[DllImport("kernel32.dll")]
		public static extern void GetSystemInfo(out Classes.SYSTEM_INFO lpSystemInfo);


#if x86
		[DllImport("kernel32.dll", EntryPoint = "VirtualQueryEx", SetLastError = true)]
		public static extern UIntPtr Native_VirtualQueryEx(IntPtr hProcess, UIntPtr lpAddress,
			out Classes.MEMORY_BASIC_INFORMATION32 lpBuffer, UIntPtr dwLength);
#else
		[DllImport("kernel32.dll", EntryPoint = "VirtualQueryEx", SetLastError = true)]
		public static extern UIntPtr Native_VirtualQueryEx(IntPtr hProcess, UIntPtr lpAddress,
			out Classes.MEMORY_BASIC_INFORMATION64 lpBuffer, UIntPtr dwLength);
#endif

		[DllImport("kernel32.dll", EntryPoint = "ReadProcessMemory")]
		public static extern bool ReadProcessMemoryMulti(IntPtr hProcess, UIntPtr lpBaseAddress, [Out] IntPtr lpBuffer, UIntPtr nSize, out ulong lpNumberOfBytesRead);

#if x86
		public static UIntPtr VirtualQueryExCustom(IntPtr hProcess, UIntPtr lpAddress,
			out Classes.MEMORY_BASIC_INFORMATION32 lpBuffer)
#else
		public static UIntPtr VirtualQueryExCustom(IntPtr hProcess, UIntPtr lpAddress,
			out Classes.MEMORY_BASIC_INFORMATION64 lpBuffer)
#endif
		{
#if x86
			var retVal = Native_VirtualQueryEx(hProcess, lpAddress, out Classes.MEMORY_BASIC_INFORMATION32 tmp32, new UIntPtr((uint)Marshal.SizeOf<Classes.MEMORY_BASIC_INFORMATION32>()));
			if (retVal == UIntPtr.Zero)
			{
				int err = Marshal.GetLastWin32Error();
				int y = 1;
			}

			lpBuffer.BaseAddress = tmp32.BaseAddress;
			lpBuffer.AllocationBase = tmp32.AllocationBase;
			lpBuffer.AllocationProtect = tmp32.AllocationProtect;
			lpBuffer.RegionSize = tmp32.RegionSize;
			lpBuffer.State = tmp32.State;
			lpBuffer.Protect = tmp32.Protect;
			lpBuffer.Type = tmp32.Type;
#else
			var retVal = Native_VirtualQueryEx(hProcess, lpAddress, out Classes.MEMORY_BASIC_INFORMATION64 tmp64, new UIntPtr((uint)Marshal.SizeOf<Classes.MEMORY_BASIC_INFORMATION64>()));
			if (retVal == UIntPtr.Zero)
			{
				int err = Marshal.GetLastWin32Error();
				int y = 1;
			}

			lpBuffer.BaseAddress = tmp64.BaseAddress;
			lpBuffer.AllocationBase = tmp64.AllocationBase;
			lpBuffer.AllocationProtect = tmp64.AllocationProtect;
			lpBuffer.RegionSize = tmp64.RegionSize;
			lpBuffer.State = tmp64.State;
			lpBuffer.Protect = tmp64.Protect;
			lpBuffer.Type = tmp64.Type;
			lpBuffer.__alignment1 = tmp64.__alignment1;
			lpBuffer.__alignment2 = tmp64.__alignment2;
#endif
			return retVal;
		}
	}
}
