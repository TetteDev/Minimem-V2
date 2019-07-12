using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

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

		public static List<string> GenerateFunctionMnemonics(IntPtr functionAddress, IntPtr returnValueAddress, List<dynamic> parameters, CallingConvention callingConvention, Main mainRef, out List<Classes.RemoteMemory> paramAllocations)
		{
			// Only 32bit atm
			List<string> mnemonics = new List<string>();
			List<Classes.RemoteMemory> parameterAllocationsReturn = new List<Classes.RemoteMemory>();
			List<Classes.RemoteMemory> parameterAllocations = new List<Classes.RemoteMemory>();

			foreach (var param in parameters)
			{
				Classes.RemoteMemory paramAllocation = mainRef.Allocator.AllocateMemory((uint)Marshal.SizeOf((object)param));
				if (!paramAllocation.IsValid)
				{
					foreach (var alloc in parameterAllocations)
						alloc.ReleaseMemory();
					throw new Exception("ERROR");
				}

				byte[] buff = StructureToByteArray(param);
				mainRef.Writer.WriteBytes(paramAllocation.BaseAddress, buff);
				parameterAllocations.Add(paramAllocation);
			}

			parameterAllocationsReturn = parameterAllocations;

			int offset = 0;
			switch (callingConvention)
			{
				case CallingConvention.Cdecl:
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push {param.BaseAddress}");

					mnemonics.Add($"call {functionAddress}");
					mnemonics.Add($"mov [{returnValueAddress.ToInt32()}], eax");
					mnemonics.Add($"add esp, {parameterAllocations.Count * 4}");
					mnemonics.Add($"retn");
					break;
				case CallingConvention.StdCall:
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push {param.BaseAddress}");

					mnemonics.Add($"call {functionAddress.ToInt32()}");
					mnemonics.Add($"mov [{returnValueAddress.ToInt32()}], eax");
					mnemonics.Add($"retn");
					break;
				case CallingConvention.FastCall:
					if (parameterAllocations.Count > 0)
					{
						mnemonics.Add($"mov ecx, {parameterAllocations[offset].BaseAddress}");
						offset++;
					}

					if (parameterAllocations.Count - offset > 0)	
					{
						mnemonics.Add($"mov edx, {parameterAllocations[offset].BaseAddress}");
						offset++;
					}

					parameterAllocations = parameterAllocations.Skip(offset).ToList();
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push {param.BaseAddress}");

					mnemonics.Add($"call {functionAddress.ToInt32()}");
					mnemonics.Add($"mov [{returnValueAddress.ToInt32()}], eax");
					mnemonics.Add($"retn");
					break;
				case CallingConvention.ThisCall:
					if (parameterAllocations.Count > 0)
					{
						mnemonics.Add($"mov ecx, {parameterAllocations[offset].BaseAddress}");
						offset++;
					}

					parameterAllocations = parameterAllocations.Skip(offset).ToList();
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push {param.BaseAddress}");

					mnemonics.Add($"call {functionAddress.ToInt32()}");
					mnemonics.Add($"mov [{returnValueAddress.ToInt32()}], eax");

					// Confirm this - caller clean up stack, and not callee
					//if (parameterAllocations.Count > 0)
					//	mnemonics.Add($"add esp, {parameterAllocations.Count * 4}");
					// Confirm this
					mnemonics.Add($"retn");
					break;
				default:
					throw new Exception("Deal with this");
			}
			paramAllocations = parameterAllocationsReturn;
			mnemonics.Insert(0, $"use32");
			return mnemonics;
		}

		[Obsolete("Note to developer; revise this function or remove it")]
		public string FormatParameter(dynamic param)
		{
			bool CanBeStoredInRegisters = false;

			TypeCode typeCode = Type.GetTypeCode(param.GetType());
			bool IsIntPtr = param.GetType() == typeof(IntPtr);
			CanBeStoredInRegisters = IsIntPtr ||
			                         typeCode == TypeCode.Boolean ||
			                         typeCode == TypeCode.Byte ||
			                         typeCode == TypeCode.Char ||
			                         typeCode == TypeCode.Int16 ||
			                         typeCode == TypeCode.Int32 ||
			                         typeCode == TypeCode.Int64 ||
			                         typeCode == TypeCode.SByte ||
			                         typeCode == TypeCode.Single ||
			                         typeCode == TypeCode.UInt16 ||
			                         typeCode == TypeCode.UInt32;

			return CanBeStoredInRegisters ? $"push {param}" : null /* Instead of null return, write value to the memory, and return the base address */;
		}
	}

	public static class ProcessExtensions
	{
		public static List<ProcessModule> ProcessModules(this Process processObject)
		{
			return processObject.Modules.Cast<ProcessModule>().ToList();
		}
	}

	public static class ProcessModuleExtensions
	{
		public static IntPtr EndAddress(this ProcessModule processModuleObject)
		{
#if x86
			return new IntPtr(processModuleObject.BaseAddress.ToInt32() + processModuleObject.ModuleMemorySize);
#else
			return new IntPtr(processModuleObject.BaseAddress.ToInt64() + processModuleObject.ModuleMemorySize);
#endif
		}
	}

	public static class ListExtensions
	{
		public static IEnumerable<A> Slice<A>(this IEnumerable<A> list, int from, int to)
		{
			return list.Take(to).Skip(from);
		}

		
	}
}
