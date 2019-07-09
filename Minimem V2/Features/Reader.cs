using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Minimem.Features
{
	public class Reader
	{
		private Main _mainReference;

		public Reader(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Reader cannot be null");
		}

		public T Read<T>(IntPtr address) where T : struct
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
#if x86
			byte[] buffer = new byte[Marshal.SizeOf(typeof(T))];
			bool flag = Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buffer, Marshal.SizeOf(typeof(T)), out IntPtr numBytesRead);
			return HelperMethods.ByteArrayToStructure<T>(buffer);
#else
			byte[] buffer = new byte[(long)Marshal.SizeOf(typeof(T))];
			bool flag = Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buffer, (long)Marshal.SizeOf(typeof(T)), out IntPtr numBytesRead);
			return HelperMethods.ByteArrayToStructure<T>(buffer);
#endif
		}

		public string ReadString(IntPtr address, Encoding encoding, int maxLength = 128, bool zeroTerminated = false)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (encoding == null) encoding = Encoding.UTF8;
			var buff = new byte[maxLength];
			bool flag = Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buff, buff.Length, out IntPtr numBytesRead);
			return zeroTerminated ? encoding.GetString(buff).Split('\0')[0] : Encoding.UTF8.GetString(buff);
		}

		public byte[] ReadBytes(IntPtr address, IntPtr size)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (address == IntPtr.Zero || size == IntPtr.Zero) return null;
#if x86
			byte[] buffer = new byte[size.ToInt32()];
			Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.Length, out IntPtr numBytesRead);
			return buffer;
#else
			byte[] buffer = new byte[size.ToInt64()];
			Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.Length, out IntPtr numBytesRead);
			return buffer;
#endif
		}
	}
}
