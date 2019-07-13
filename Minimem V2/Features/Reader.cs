using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
#else
			byte[] buffer = new byte[(long)Marshal.SizeOf(typeof(T))];
			bool flag = Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buffer, (long)Marshal.SizeOf(typeof(T)), out IntPtr numBytesRead);
#endif
			return HelperMethods.ByteArrayToStructure<T>(buffer);
		}
		public Task<T> AsyncRead<T>(IntPtr address) where T : struct
		{
			return Task.Run(() => Read<T>(address));
		}

		public string ReadString(IntPtr address, Encoding encoding, int maxLength = 128, bool zeroTerminated = false)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (encoding == null) encoding = Encoding.UTF8;
			var buff = new byte[maxLength];
#if x86
			bool flag = Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buff, buff.Length, out IntPtr numBytesRead);
#else
			bool flag = Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buff, buff.LongLength, out IntPtr numBytesRead);
#endif
			return zeroTerminated ? encoding.GetString(buff).Split('\0')[0] : encoding.GetString(buff);
		}
		public Task<string> AsyncReadString(IntPtr address, Encoding encoding, int maxLength = 128, bool zeroTerminated = false)
		{
			return Task.Run(() => ReadString(address, encoding, maxLength, zeroTerminated));
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
#else
			byte[] buffer = new byte[size.ToInt64()];
			Win32.PInvoke.ReadProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.LongLength, out IntPtr numBytesRead);
#endif
			return buffer;
		}
		public Task<byte[]> AsyncReadBytes(IntPtr address, IntPtr size)
		{
			return Task.Run(() => ReadBytes(address, size));
		}
	}
}
