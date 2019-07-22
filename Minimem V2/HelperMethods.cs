using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Minimem
{
	public class HelperMethods
	{
		public static void CompareScanMulti(Classes.MemoryRegionResult item, ref List<Tuple<Classes.MultiAobItem, ConcurrentBag<long>>> aobCollection, IntPtr processHandle)
		{
			if (processHandle == IntPtr.Zero) return;
			foreach (var aobToSearchFor in aobCollection)
			{
				if (aobToSearchFor.Item1.Mask.Length != aobToSearchFor.Item1.Pattern.Length)
					throw new ArgumentException($"{nameof(aobToSearchFor.Item1.Pattern)}.Length != {nameof(aobToSearchFor.Item1.Mask)}.Length");
			}

			IntPtr buffer = Marshal.AllocHGlobal((int)item.RegionSize);
			Win32.PInvoke.ReadProcessMemoryMulti(processHandle, item.CurrentBaseAddress, buffer, (UIntPtr)item.RegionSize, out ulong bytesRead);

			foreach (var aobPattern in aobCollection)
			{
				int result = 0 - aobPattern.Item1.Pattern.Length;
				unsafe
				{
					do
					{

						result = FindPattern((byte*)buffer.ToPointer(), (int)bytesRead, aobPattern.Item1.Pattern, aobPattern.Item1.Mask, result + aobPattern.Item1.Pattern.Length);

						if (result >= 0)
							aobPattern.Item2.Add((long)item.CurrentBaseAddress + result);

					} while (result != -1);
				}
			}

			Marshal.FreeHGlobal(buffer);
		}

		public static unsafe int FindPattern(byte* body, int bodyLength, byte[] pattern, byte[] masks, int start = 0)
		{
			int foundIndex = -1;

			if (bodyLength <= 0 || pattern.Length <= 0 || start > bodyLength - pattern.Length ||
			    pattern.Length > bodyLength) return foundIndex;

			for (int index = start; index <= bodyLength - pattern.Length; index++)
			{
				if (((body[index] & masks[0]) != (pattern[0] & masks[0]))) continue;

				var match = true;
				for (int index2 = 1; index2 <= pattern.Length - 1; index2++)
				{
					if ((body[index + index2] & masks[index2]) == (pattern[index2] & masks[index2])) continue;
					match = false;
					break;

				}

				if (!match) continue;

				foundIndex = index;
				break;
			}

			return foundIndex;
		}

		public static int TranslateProcessNameIntoProcessId(string processName, bool sloppySearch = false)
		{
			if (string.IsNullOrEmpty(processName)) return -1;
			if (sloppySearch)
			{
				var _procObjSloppy = Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName.ToLower().Contains(processName.ToLower()));
				if (_procObjSloppy == default) return -1;
				return _procObjSloppy.Id;
			}
			else
			{
				var _procObj = Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName == processName);
				if (_procObj == default) return -1;
				return _procObj.Id;
			}

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
			if (obj == null) throw new NullReferenceException($"{nameof(obj)} was null when passed to function \"StructureToByteArray(obj)\"");
			var length = Marshal.SizeOf(obj);
			var array = new byte[length];
			var pointer = Marshal.AllocHGlobal(length);
			Marshal.StructureToPtr(obj, pointer, true);
			Marshal.Copy(pointer, array, 0, length);
			Marshal.FreeHGlobal(pointer);
			return array;
		}

		[Obsolete("If the X64 calling convention is specified, and the 32bit memory dll is used, errors will come")]
		public static List<string> GenerateFunctionMnemonics(IntPtr functionAddress, List<dynamic> parameters, Classes.CallingConventionsEnum callingConvention, Main mainRef, Type funcReturnType,bool Process64Bit,out List<Classes.RemoteMemory> paramAllocations)
		{
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
				case Classes.CallingConventionsEnum.Cdecl:
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push dword [{param.BaseAddress}]");

					mnemonics.Add($"call {functionAddress}");
					mnemonics.Add($"add esp, {parameterAllocations.Count * 4}");
					break;
				case Classes.CallingConventionsEnum.Winapi: // This defaults to StdCall on windows
				case Classes.CallingConventionsEnum.StdCall:
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push [{param.BaseAddress}]");

					mnemonics.Add($"call {functionAddress.ToInt32()}");
					break;
				case Classes.CallingConventionsEnum.FastCall:
					if (parameterAllocations.Count > 0)
					{
						mnemonics.Add($"mov ecx, [{parameterAllocations[offset].BaseAddress}]");
						offset++;
					}

					if (parameterAllocations.Count - offset > 0)	
					{
						mnemonics.Add($"mov edx, [{parameterAllocations[offset].BaseAddress}]");
						offset++;
					}

					parameterAllocations = parameterAllocations.Skip(offset).ToList();
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push [{param.BaseAddress}]");

					mnemonics.Add($"call {functionAddress.ToInt32()}");
					break;
				case Classes.CallingConventionsEnum.ThisCall:
					if (parameterAllocations.Count > 0)
					{
						mnemonics.Add($"mov ecx, [{parameterAllocations[offset].BaseAddress}]");
						offset++;
					}

					parameterAllocations = parameterAllocations.Skip(offset).ToList();
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push [{param.BaseAddress}]");

					mnemonics.Add($"call {(Process64Bit ? functionAddress.ToInt64().ToString() : functionAddress.ToInt32().ToString())}");
					break;
				case Classes.CallingConventionsEnum.x64Convention:
					if (parameterAllocations.Count > 0)
					{
						mnemonics.Add($"mov rcx, [{parameterAllocations[offset].BaseAddress}]");
						offset++;
					}

					if (parameterAllocations.Count - offset > 0)
					{
						mnemonics.Add($"mov rdx, [{parameterAllocations[offset].BaseAddress}]");
						offset++;
					}

					if (parameterAllocations.Count - offset > 0)
					{
						mnemonics.Add($"mov r8, [{parameterAllocations[offset].BaseAddress}]");
						offset++;
					}

					if (parameterAllocations.Count - offset > 0)
					{
						mnemonics.Add($"mov r9, [{parameterAllocations[offset].BaseAddress}]");
						offset++;
					}

					parameterAllocations = parameterAllocations.Skip(offset).ToList();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push [{param.BaseAddress}]");
					mnemonics.Add($"call {functionAddress.ToInt64()}");

					break;
				default:
					throw new InvalidOperationException("Deal with this");
			}
			paramAllocations = parameterAllocationsReturn;
			mnemonics.Insert(0, (callingConvention != Classes.CallingConventionsEnum.x64Convention ? "use32" : "use64"));
			mnemonics.Add((callingConvention == Classes.CallingConventionsEnum.x64Convention ? "ret" : "retn"));
			return mnemonics;
		} }
}
