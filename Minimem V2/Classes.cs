using System;

namespace Minimem
{
	public class Classes
	{
		public class RemoteMemory
		{
			private Main _mainReference;
			public IntPtr BaseAddress;
			public IntPtr Size;

			public AllocationType AllocationType;
			public MemoryProtection ProtectionType;

			public RemoteMemory(IntPtr baseAddress, IntPtr size, AllocationType allocationType, MemoryProtection protectionType, Main main)
			{
				BaseAddress = baseAddress;
				Size = size;
				AllocationType = allocationType;
				ProtectionType = protectionType;
				_mainReference = main;
			}

			public bool ChangeProtection(MemoryProtection newProtectionType)
			{
				if (BaseAddress == IntPtr.Zero && (uint)Size == 0 && AllocationType == AllocationType.Invalid && ProtectionType == MemoryProtection.Invalid) return false;
				if (_mainReference == null) throw new NullReferenceException("Main Reference was null - Cannot change protection of memory");
				if (newProtectionType == ProtectionType) return false;
				bool flag = Win32.PInvoke.VirtualProtectEx(_mainReference.ProcessHandle, BaseAddress, Size, newProtectionType, out MemoryProtection oldProtectionType);
				if (flag) ProtectionType = newProtectionType;
				return flag;
			}

			public bool ReleaseMemory()
			{
				if (BaseAddress == IntPtr.Zero && (uint)Size == 0 && AllocationType == AllocationType.Invalid && ProtectionType == MemoryProtection.Invalid) return false;
				if (_mainReference == null) throw new NullReferenceException("Main Reference was null - Cannot free memory");
				bool flag = Win32.PInvoke.VirtualFreeEx(_mainReference.ProcessHandle, BaseAddress, 0, AllocationType.Release);
				if (flag)
				{
					BaseAddress = IntPtr.Zero;
					Size = IntPtr.Zero;
					AllocationType = AllocationType.Invalid;
					ProtectionType = MemoryProtection.Invalid;
					return true;
				}
				return false ;
			}

			public bool IsValid => BaseAddress != IntPtr.Zero &&
				Size != IntPtr.Zero &&
				AllocationType != AllocationType.Invalid &&
				ProtectionType != MemoryProtection.Invalid;
		}

		[Flags]
		public enum AllocationType
		{
			Invalid = 0x9999,
			Commit = 0x1000,
			Reserve = 0x2000,
			Decommit = 0x4000,
			Release = 0x8000,
			Reset = 0x80000,
			Physical = 0x400000,
			TopDown = 0x100000,
			WriteWatch = 0x200000,
			LargePages = 0x20000000
		}

		[Flags]
		public enum MemoryProtection
		{
			Invalid = 0x9999,
			Execute = 0x10,
			ExecuteRead = 0x20,
			ExecuteReadWrite = 0x40,
			ExecuteWriteCopy = 0x80,
			NoAccess = 0x01,
			ReadOnly = 0x02,
			ReadWrite = 0x04,
			WriteCopy = 0x08,
			GuardModifierflag = 0x100,
			NoCacheModifierflag = 0x200,
			WriteCombineModifierflag = 0x400
		}

		[Flags]
		public enum WaitForSingleObjectResult : uint
		{
			INFINITE = 0xFFFFFFFF,
			WAIT_ABANDONED = 0x00000080,
			WAIT_OBJECT_0 = 0x00000000,
			WAIT_TIMEOUT = 0x00000102,
		}

		[Flags]
		public enum ThreadCreationFlags : uint
		{
			StackSizeParamIsAReservation = 65536u,
			Suspended = 4u,
			Run = 0u
		}
	}
}
