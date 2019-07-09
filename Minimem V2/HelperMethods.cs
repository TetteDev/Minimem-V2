using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Minimem
{
	public class HelperMethods
	{
		public static int TranslateProcessNameIntoProcessId(string processName)
		{
			if (string.IsNullOrEmpty(processName)) return -1;
			var _procObj = Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName == processName);
			if (_procObj == default) return -1;
			return _procObj.Id;
		}

		public static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
		{
			var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			try
			{
				return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
			}
			finally
			{
				handle.Free();
			}
		}

		public static byte[] StructureToByteArray(object obj)
		{
			if (obj == null) throw new NullReferenceException("Null");
			var length = Marshal.SizeOf(obj);
			var array = new byte[length];
			var pointer = Marshal.AllocHGlobal(length);
			Marshal.StructureToPtr(obj, pointer, true);
			Marshal.Copy(pointer, array, 0, length);
			Marshal.FreeHGlobal(pointer);
			return array;
		}
	}

	public static class ProcessExtensions
	{
		public static List<ProcessModule> ProcessModules(this Process processObject)
		{
			return processObject.Modules.Cast<ProcessModule>().ToList();
		}
	}
}
