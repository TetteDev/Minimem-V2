using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using Minimem.Extension_Methods;

namespace Minimem.Features
{
	public class Injector
	{
		private Main _mainReference;
		public Injector(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Injector cannot be null");
		}

		public bool InjectModule(string dllPath)
		{
			if(_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (string.IsNullOrEmpty(dllPath)) return false;
			if (!File.Exists(dllPath)) return false;
			IntPtr loadLibraryAddr = Win32.PInvoke.GetProcAddress(Win32.PInvoke.GetModuleHandle("kernel32.dll"), "LoadLibraryA");
			if (loadLibraryAddr == IntPtr.Zero) return false;
			var allocMemAddress = _mainReference.Allocator.AllocateMemory((uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))));
			if (allocMemAddress.BaseAddress == IntPtr.Zero) return false;
			_mainReference.Writer.WriteBytes(allocMemAddress.BaseAddress, Encoding.Default.GetBytes(dllPath));
			IntPtr threadHandle = Win32.PInvoke.CreateRemoteThread(_mainReference.ProcessHandle, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress.BaseAddress, 0, out IntPtr lpThreadId);
			if (threadHandle == IntPtr.Zero)
			{
				allocMemAddress.ReleaseMemory();
				return false;
			}

			Classes.WaitForSingleObjectResult result = (Classes.WaitForSingleObjectResult)Win32.PInvoke.WaitForSingleObject(threadHandle, (uint)Classes.WaitForSingleObjectResult.INFINITE);
			allocMemAddress.ReleaseMemory();
			return true;
		}

		public bool EjectModule(string moduleName)
		{
			if(_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (string.IsNullOrEmpty(moduleName)) return false;
			_mainReference.Refresh();
			ProcessModule pm = _mainReference.ProcessObject.ProcessModules().FirstOrDefault(mod => string.Equals(mod.ModuleName, moduleName, StringComparison.CurrentCultureIgnoreCase));
			if (pm == default(ProcessModule))
			{
				pm = _mainReference.ProcessObject.ProcessModules().FirstOrDefault(mod => mod.ModuleName.ToLower().Contains(moduleName.ToLower())); // Sloppy
				if (pm == default(ProcessModule)) return false;
			}
			IntPtr freeLibraryAddr = Win32.PInvoke.GetProcAddress(Win32.PInvoke.GetModuleHandle("kernel32.dll"), "FreeLibrary");
			if (freeLibraryAddr == IntPtr.Zero) return false;
			IntPtr threadHandle = Win32.PInvoke.CreateRemoteThread(_mainReference.ProcessHandle, IntPtr.Zero, 0, freeLibraryAddr, pm.BaseAddress, 0, out IntPtr lpThreadId);
			if (threadHandle == IntPtr.Zero) return false;
			Classes.WaitForSingleObjectResult result = (Classes.WaitForSingleObjectResult)Win32.PInvoke.WaitForSingleObject(threadHandle, (uint)Classes.WaitForSingleObjectResult.INFINITE);
			return true;
		}
	}
}
