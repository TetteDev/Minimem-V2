﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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

					_mainReference.Allocator.Allocations.RemoveAt(_mainReference.Allocator.Allocations.FindIndex(
						alloc=> alloc.AllocationObject.BaseAddress == BaseAddress));
					return true;
				}
				return false ;
			}

			public bool IsValid => BaseAddress != IntPtr.Zero &&
				Size != IntPtr.Zero &&
				AllocationType != AllocationType.Invalid &&
				ProtectionType != MemoryProtection.Invalid;
		}

		public class MultiAobResultItem
		{
			public string Identifier = "NO_IDENTIFIER_PROVIDED";
			public string Pattern = "";
			public long FirstResultAsLong = 0;
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
