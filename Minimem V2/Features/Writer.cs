using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Minimem.Features
{
	public class Writer
	{
		private readonly Main _mainReference;

		public Writer(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Reader cannot be null");
		}

		public bool Write<T>(IntPtr address, T obj) where T : struct
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			byte[] buffer = HelperMethods.StructureToByteArray(obj);
#if x86
			bool flag = Win32.PInvoke.WriteProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.Length, out IntPtr bytesWritten);
#else
			bool flag = Win32.PInvoke.WriteProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.LongLength, out IntPtr bytesWritten);
#endif
			return flag;
		}
		public Task<bool> AsyncWrite<T>(IntPtr address, T obj) where T : struct
		{
			return Task.Run(() => Write<T>(address, obj));
		}

		public bool WriteBytes(IntPtr address, byte[] buffer, Classes.MemoryProtection overrideProtectionType = Classes.MemoryProtection.DoNothing)
		{
			if (address == IntPtr.Zero || buffer == null || buffer.Length < 1) return false;
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");

			Classes.MemoryProtection oldProtect = Classes.MemoryProtection.Invalid;
			if (overrideProtectionType != Classes.MemoryProtection.DoNothing && overrideProtectionType != Classes.MemoryProtection.Invalid)
			{
#if x86
				Win32.PInvoke.VirtualProtectEx(_mainReference.ProcessHandle, address, new IntPtr(buffer.Length), overrideProtectionType, out oldProtect);
#else
				bool success = Win32.PInvoke.VirtualProtectEx(_mainReference.ProcessHandle, address, new IntPtr(buffer.LongLength), overrideProtectionType, out oldProtect);
#endif
			}
#if x86
			Win32.PInvoke.WriteProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.Length, out IntPtr numBytesWritten);
#else
			Win32.PInvoke.WriteProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.LongLength, out IntPtr numBytesWritten);
#endif

			if (oldProtect != Classes.MemoryProtection.Invalid)
				Win32.PInvoke.VirtualProtectEx(_mainReference.ProcessHandle, address, new IntPtr(buffer.LongLength), oldProtect, out oldProtect);

#if x86
			return numBytesWritten.ToInt32() == buffer.Length;
#else
			return numBytesWritten.ToInt64() == buffer.LongLength;
#endif
		}
		public Task<bool> AsyncWriteBytes(IntPtr address, byte[] buffer, Classes.MemoryProtection overrideProtectionType = Classes.MemoryProtection.DoNothing)
		{
			return Task.Run(() => WriteBytes(address, buffer, overrideProtectionType));
		}

		public bool WriteString(IntPtr address, string value, Encoding encoding)
		{
			if (address == IntPtr.Zero || value.Length < 1) return false;
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (encoding == null) encoding = Encoding.UTF8;
			byte[] buffer = encoding.GetBytes(value);
#if x86
			Win32.PInvoke.WriteProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.Length, out IntPtr numBytesWritten);
#else
			Win32.PInvoke.WriteProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.LongLength, out IntPtr numBytesWritten);
#endif
			return true;
		}
		public Task<bool> AsyncWriteString(IntPtr address, string value, Encoding encoding)
		{
			return Task.Run(() => AsyncWriteString(address, value, encoding));
		}

		public bool WriteArray<T>(IntPtr address, T[] arr) where T : struct
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (address == IntPtr.Zero || arr.Length < 1) return false;

			int itemSize = Marshal.SizeOf(typeof(T));
			int stepsNeeded = arr.Length;

			for (int stepIdx = 0; stepIdx < stepsNeeded; stepIdx++)
			{
#if x86
				_mainReference.Writer.Write(new IntPtr(address.ToInt32() + (itemSize * stepIdx)), arr[stepIdx]);
#else
				_mainReference.Writer.Write(new IntPtr(address.ToInt64() + (long)(itemSize * stepIdx)), arr[stepIdx]);
#endif
			}
			return true;
		}
		public Task<bool> AsyncWriteArray<T>(IntPtr address, T[] arr) where T : struct
		{
			return Task.Run(() => WriteArray(address, arr));
		}
	}
}
