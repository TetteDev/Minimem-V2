﻿using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

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
		public static List<string> GenerateFunctionMnemonics(IntPtr functionAddress, IntPtr returnValueAddress, List<dynamic> parameters, Classes.CallingConventionsEnum callingConvention, Main mainRef, Type funcReturnType,bool Process64Bit,out List<Classes.RemoteMemory> paramAllocations)
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
				case Classes.CallingConventionsEnum.Cdecl:
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push {param.BaseAddress}");

					mnemonics.Add($"call {functionAddress}");
					mnemonics.Add($"mov [{returnValueAddress.ToInt32()}], eax");
					mnemonics.Add($"add esp, {parameterAllocations.Count * 4}");
					break;
				case Classes.CallingConventionsEnum.Winapi: // This defaults to StdCall on windows
				case Classes.CallingConventionsEnum.StdCall:
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push {param.BaseAddress}");

					mnemonics.Add($"call {functionAddress.ToInt32()}");
					mnemonics.Add($"mov [{returnValueAddress.ToInt32()}], eax");
					break;
				case Classes.CallingConventionsEnum.FastCall:
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
					break;
				case Classes.CallingConventionsEnum.ThisCall:
					if (parameterAllocations.Count > 0)
					{
						mnemonics.Add($"mov ecx, {parameterAllocations[offset].BaseAddress}");
						offset++;
					}

					parameterAllocations = parameterAllocations.Skip(offset).ToList();
					parameterAllocations.Reverse();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push {param.BaseAddress}");

					mnemonics.Add($"call {(Process64Bit ? functionAddress.ToInt64().ToString() : functionAddress.ToInt32().ToString())}");
					mnemonics.Add($"mov [{(Process64Bit ? returnValueAddress.ToInt64().ToString() : returnValueAddress.ToInt32().ToString())}], eax");

					// Confirm this - caller clean up stack, and not callee
					//if (parameterAllocations.Count > 0)
					//	mnemonics.Add($"add esp, {parameterAllocations.Count * 4}");
					// Confirm this
					break;
				case Classes.CallingConventionsEnum.x64Convention:
					if (parameterAllocations.Count > 0)
					{
						mnemonics.Add($"mov rcx, {parameterAllocations[offset].BaseAddress}");
						offset++;
					}

					if (parameterAllocations.Count - offset > 0)
					{
						mnemonics.Add($"mov rdx, {parameterAllocations[offset].BaseAddress}");
						offset++;
					}

					if (parameterAllocations.Count - offset > 0)
					{
						mnemonics.Add($"mov r8, {parameterAllocations[offset].BaseAddress}");
						offset++;
					}

					if (parameterAllocations.Count - offset > 0)
					{
						mnemonics.Add($"mov r9, {parameterAllocations[offset].BaseAddress}");
						offset++;
					}

					parameterAllocations = parameterAllocations.Skip(offset).ToList();
					foreach (var param in parameterAllocations)
						mnemonics.Add($"push {param.BaseAddress}");
					mnemonics.Add($"call {functionAddress.ToInt64()}");

					if (funcReturnType == typeof(float) || funcReturnType == typeof(double))
						mnemonics.Add($"mov [{returnValueAddress.ToInt64()}], XMM0");
					else
						mnemonics.Add($"mov [{returnValueAddress.ToInt64()}], rax");

					break;
				default:
					throw new InvalidOperationException("Deal with this");
			}
			paramAllocations = parameterAllocationsReturn;
			mnemonics.Insert(0, (callingConvention != Classes.CallingConventionsEnum.x64Convention ? $"use32" : "use64"));
			mnemonics.Add((callingConvention == Classes.CallingConventionsEnum.x64Convention ? "ret" : "retn"));
			return mnemonics;
		} }
}
