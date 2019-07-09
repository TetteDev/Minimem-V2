using System;
using System.Linq;
using System.Text;

namespace Minimem.Features
{
	public class Writer
	{
		private Main _mainReference;

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
			bool flag = Win32.PInvoke.WriteProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.Length, out IntPtr bytesWritten);
			return flag;
		}

		public bool WriteBytes(IntPtr address, byte[] buffer)
		{
			if (address == IntPtr.Zero || buffer == null || buffer.Length < 1) return false;
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			Win32.PInvoke.WriteProcessMemory(_mainReference.ProcessHandle, address, buffer, buffer.Length, out IntPtr numBytesWritten);
			return true;
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
	}
}
