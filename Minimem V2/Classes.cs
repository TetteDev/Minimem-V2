using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.InteropServices;
using System.Text;

namespace Minimem
{
	public class Classes
	{
		public class RemoteMemory
		{
			private readonly Main _mainReference;
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

					try
					{
						_mainReference.Allocator.Allocations.RemoveAt(_mainReference.Allocator.Allocations.FindIndex(
							alloc => alloc.AllocationObject.BaseAddress == BaseAddress));
					}
					catch
					{
						// swallow exception
					}
					
					return true;
				}
				return false ;
			}

			public bool IsValid => BaseAddress != IntPtr.Zero &&
				Size != IntPtr.Zero &&
				AllocationType != AllocationType.Invalid &&
				ProtectionType != MemoryProtection.Invalid;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Registers32
		{
			public readonly int EAX; // 0x0
			public readonly int EBX; // 0x4
			public readonly int ECX; // 0x8
			public readonly int EDX; // 0x12
			public readonly int EDI; // 0x16
			public readonly int ESI; // 0x20
			public readonly int EBP; // 0x24
			public readonly int ESP; // 0x28

			public readonly short CS; // 0x62;
			public readonly short SS; // 0x64;
			public readonly short DS; // 0x66;
			public readonly short ES; // 0x68;
			public readonly short FS; // 0x70;
			public readonly short GS; // 0x72;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Registers64
		{
			public readonly int EAX; // 0
			public readonly int EBX; // 4
			public readonly int ECX; // 8
			public readonly int EDX; // 12
			public readonly int EDI; // 16
			public readonly int ESI; // 20
			public readonly int EBP; // 24
			public readonly int ESP; // 28

			public readonly long RAX; // 36
			public readonly long RBX; // 44
			public readonly long RCX; // 52
			public readonly long RDX; // 60
			public readonly long RDI; // 68
			public readonly long RSI; // 76
			public readonly long RBP; // 84
			public readonly long RSP; // 92

			// These are unused atm
			public readonly short CS; // 94;
			public readonly short SS; // 96;
			public readonly short DS; // 98;
			public readonly short ES; // 100;
			public readonly short FS; // 102;
			public readonly short GS; // 104;
		}

		public class MultiAobResultItem
		{
			public string Identifier = "NO_IDENTIFIER_PROVIDED";
			public string Pattern = "";
			public IntPtr FirstResult = IntPtr.Zero;
			public string FirstResultAsHexString = "";
			public List<long> Results;
		}

		public class MultiAobItem // Used internally for method 'MultiAobScan'
		{
			public string OptionalIdentifier = "NO_IDENTIFIER_PROVIDED";
			public string ArrayOfBytesString;
			public byte[] Pattern;
			public byte[] Mask;
		}

		public struct MemoryRegionResult
		{
			public UIntPtr CurrentBaseAddress { get; set; }
			public long RegionSize { get; set; }
			public UIntPtr RegionBase { get; set; }
		}


		public delegate void InterceptHookExecuteDelegate(object InterceptHookObject);

		public class DetourCallback
		{
			public bool IsEnabled { get; private set; }
			public bool IsDisposed { get; private set; } = false;
			private readonly Main MainReference;

			public readonly IntPtr DetourStartAddress;

			public readonly bool SaveOriginalBytes = false;
			public readonly bool PutOriginalBytesAfterMnemonics = false;
			public readonly byte[] OriginalBytes;
			public readonly int OriginalByteOverwriteCount;

			public readonly IntPtr CodeCaveAddress;
			public RemoteMemory CodeCaveAllocationObject;

			public RemoteMemory HitCounter;
			public RemoteMemory RegisterStructs = null;
			public byte[] CodeCaveBytes;

			public InterceptHookExecuteDelegate RaiseEvent = null;

			public uint lastValue = 0;

			private readonly byte[] JumpInBytes;
			private readonly byte[] JumpOutBytes;

			public DetourCallback(IntPtr _startAddress, IntPtr _codeCaveAddress, int _overwriteByteCount, byte[] _overwrittenOriginalBytes, byte[] _jumpInBytes,byte[] _jumpOutBytes, byte[] _codeCaveBytes,bool _saveOriginalBytes, bool _putOriginalBytesAfterMnemonics, Classes.RemoteMemory _allocationObject, Classes.RemoteMemory _hitCounter, Classes.RemoteMemory _registerStructs, InterceptHookExecuteDelegate _RaiseEvent, Main _mainReference)
			{
				DetourStartAddress = _startAddress;
				CodeCaveAddress = _codeCaveAddress;
				OriginalByteOverwriteCount = _overwriteByteCount;
				OriginalBytes = _overwrittenOriginalBytes;
				SaveOriginalBytes = _saveOriginalBytes;
				PutOriginalBytesAfterMnemonics = _putOriginalBytesAfterMnemonics;
				JumpInBytes = _jumpInBytes;
				CodeCaveBytes = _codeCaveBytes;
				CodeCaveAllocationObject = _allocationObject;
				HitCounter = _hitCounter;
				RegisterStructs = _registerStructs;
				RaiseEvent = _RaiseEvent;
				JumpOutBytes = _jumpOutBytes;
				MainReference = _mainReference;
			}

			public void Enable()
			{
				if (!IsEnabled)
				{
					if (MainReference == null) throw new Exception("Reference to Main Class cannot be null");
					if (!MainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
					if (SaveOriginalBytes)
					{
						if (PutOriginalBytesAfterMnemonics)
						{
							MainReference.Writer.WriteBytes(CodeCaveAddress, CodeCaveBytes);
							MainReference.Writer.WriteBytes(IntPtr.Add(CodeCaveAddress, CodeCaveBytes.Length), OriginalBytes);
							MainReference.Writer.WriteBytes(IntPtr.Add(CodeCaveAddress, CodeCaveBytes.Length + OriginalBytes.Length), JumpOutBytes);
							MainReference.Writer.WriteBytes(DetourStartAddress, JumpInBytes, MemoryProtection.ExecuteReadWrite);
						}
						else
						{
							MainReference.Writer.WriteBytes(CodeCaveAddress, OriginalBytes);
							MainReference.Writer.WriteBytes(IntPtr.Add(CodeCaveAddress, OriginalBytes.Length), CodeCaveBytes);
							MainReference.Writer.WriteBytes(IntPtr.Add(CodeCaveAddress, OriginalBytes.Length + CodeCaveBytes.Length), JumpOutBytes);
							MainReference.Writer.WriteBytes(DetourStartAddress, JumpInBytes, MemoryProtection.ExecuteReadWrite);
						}
					}
					else
					{
						MainReference.Writer.WriteBytes(CodeCaveAddress, CodeCaveBytes);
						MainReference.Writer.WriteBytes(IntPtr.Add(CodeCaveAddress, CodeCaveBytes.Length), JumpOutBytes);
						MainReference.Writer.WriteBytes(DetourStartAddress, JumpInBytes, MemoryProtection.ExecuteReadWrite);
					}
					IsEnabled = true;
				}
			}

			public void Disable()
			{
				if (IsEnabled)
				{
					if (MainReference == null) throw new Exception("Reference to Main Class cannot be null");
					if (!MainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");

					MainReference.Writer.WriteBytes(DetourStartAddress, OriginalBytes, MemoryProtection.ExecuteReadWrite);
					IsEnabled = false;
				}
			}

			public void Dispose()
			{
				if (IsEnabled) 
					Disable();

				RegisterStructs?.ReleaseMemory();
				HitCounter?.ReleaseMemory();
				CodeCaveAllocationObject?.ReleaseMemory();
				IsDisposed = true;
			}
		}

		public struct SYSTEM_INFO
		{
			public ushort processorArchitecture;
			ushort reserved;
			public uint pageSize;
			public UIntPtr minimumApplicationAddress;
			public UIntPtr maximumApplicationAddress;
			public IntPtr activeProcessorMask;
			public uint numberOfProcessors;
			public uint processorType;
			public uint allocationGranularity;
			public ushort processorLevel;
			public ushort processorRevision;
		}

		public struct MEMORY_BASIC_INFORMATION32
		{
			public UIntPtr BaseAddress;
			public UIntPtr AllocationBase;
			public uint AllocationProtect;
			public uint RegionSize;
			public uint State;
			public uint Protect;
			public uint Type;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct MEMORY_BASIC_INFORMATION64
		{
			public ulong BaseAddress;
			public ulong AllocationBase;
			public int AllocationProtect;
			public int __alignment1;
			public ulong RegionSize;
			public int State;
			public int Protect;
			public int Type;
			public int __alignment2;
		}

		public const uint TWO_GIGABYTES = 2147483648;

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
			DoNothing = 0x9998,
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

		[Flags]
		public enum ThreadAccess : int
		{
			TERMINATE = (0x0001),
			SUSPEND_RESUME = (0x0002),
			GET_CONTEXT = (0x0008),
			SET_CONTEXT = (0x0010),
			SET_INFORMATION = (0x0020),
			QUERY_INFORMATION = (0x0040),
			SET_THREAD_TOKEN = (0x0080),
			IMPERSONATE = (0x0100),
			DIRECT_IMPERSONATION = (0x0200),
			THREAD_HIJACK = SUSPEND_RESUME | GET_CONTEXT | SET_CONTEXT,
			THREAD_ALL = TERMINATE | SUSPEND_RESUME | GET_CONTEXT | SET_CONTEXT | SET_INFORMATION | QUERY_INFORMATION | SET_THREAD_TOKEN | IMPERSONATE | DIRECT_IMPERSONATION
		}

		public enum DetourType
		{
			PUSH_RET = 1,
			JMP = 2,
		}

		public enum CallingConventionsEnum
		{
			Winapi = 1,
			/// <summary>The caller cleans the stack. This enables calling functions with <see langword="varargs" />, which makes it appropriate to use for methods that accept a variable number of parameters, such as <see langword="Printf" />.</summary>
			Cdecl = 2,
			/// <summary>The callee cleans the stack. This is the default convention for calling unmanaged functions with platform invoke.</summary>
			StdCall = 3,
			/// <summary>The first parameter is the <see langword="this" /> pointer and is stored in register ECX. Other parameters are pushed on the stack. This calling convention is used to call methods on classes exported from an unmanaged DLL.</summary>
			ThisCall = 4,
			FastCall = 5,
			x64Convention = 6,
		}
	}
}
