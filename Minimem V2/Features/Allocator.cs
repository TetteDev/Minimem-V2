using System;
using System.Collections.Generic;
using static Minimem.Classes;

namespace Minimem.Features
{
	public class Allocator
	{
		private readonly Main _mainReference;
		public readonly List<(DateTime Tmestamp, RemoteMemory AllocationObject)> Allocations = new List<(DateTime, RemoteMemory)>();
		public Allocator(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Allocator cannot be null");
		}

#if x86
		public RemoteMemory AllocateMemory(uint size, AllocationType allocationType = AllocationType.Commit | AllocationType.Reserve, MemoryProtection protectionType = MemoryProtection.ExecuteReadWrite)
#else
		public RemoteMemory AllocateMemory(ulong size, AllocationType allocationType = AllocationType.Commit | AllocationType.Reserve, MemoryProtection protectionType = MemoryProtection.ExecuteReadWrite)
#endif
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");

			IntPtr baseAddress = Win32.PInvoke.VirtualAllocEx(_mainReference.ProcessHandle, IntPtr.Zero, size, allocationType, protectionType);
			if (baseAddress != IntPtr.Zero)
			{
				RemoteMemory alloc = new RemoteMemory(baseAddress, (IntPtr)size, allocationType, protectionType, _mainReference);
				Allocations.Add((Tmestamp: DateTime.Now, AllocationObject: alloc));
				return alloc;
			}
			return new RemoteMemory(IntPtr.Zero, (IntPtr)0, AllocationType.Invalid, MemoryProtection.Invalid, null);
		}
	}
}
