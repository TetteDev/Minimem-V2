using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Minimem.Features
{
	public class Executor
	{
		private Main _mainReference;

		public Executor(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Executor cannot be null");
		}

		public void Execute(string[] mnemonics)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (mnemonics == null || mnemonics.Length < 1) throw new InvalidOperationException("Passed Mnemonics to Executor.Execute was null or empty!");

			byte[] asm = _mainReference.Assembler.Assemble(mnemonics);
			Classes.RemoteMemory alloc = _mainReference.Allocator.AllocateMemory(0x10000);
			if (!alloc.IsValid) throw new InvalidOperationException("Failed allocating memory for assembled bytes - Executor.Execute");

			_mainReference.Writer.WriteBytes(alloc.BaseAddress, asm);
			IntPtr threadHandle = Win32.PInvoke.CreateRemoteThread(_mainReference.ProcessHandle, IntPtr.Zero, 0, alloc.BaseAddress, IntPtr.Zero, (uint)Classes.ThreadCreationFlags.Run, out IntPtr threadId);
			if (threadHandle == IntPtr.Zero)
			{
				alloc.ReleaseMemory();
				return;
			}

			Win32.PInvoke.WaitForSingleObject(threadHandle, (uint) Classes.WaitForSingleObjectResult.INFINITE);
			Win32.PInvoke.CloseHandle(threadHandle);
			alloc.ReleaseMemory();
		}

		public T Execute<T>(string[] mnemonics) where T : struct
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (mnemonics == null || mnemonics.Length < 1) throw new InvalidOperationException("Passed Mnemonics to Executor.Execute<T> was null or empty!");
			Classes.RemoteMemory returnAllocation = null;
			Classes.RemoteMemory alloc = _mainReference.Allocator.AllocateMemory(0x10000);
			if (!alloc.IsValid) throw new InvalidOperationException("Failed allocating memory function body - Executor.Execute<T>");
			List<string> lstMnemonics = mnemonics.ToList();
			int retFlagPosition = lstMnemonics.FindIndex(x => x.ToLower() == "<return>");
			if (retFlagPosition != -1)
			{
				returnAllocation = _mainReference.Allocator.AllocateMemory((uint)Marshal.SizeOf(typeof(T)));
				if (!returnAllocation.IsValid)
				{
					alloc.ReleaseMemory();
					throw new InvalidOperationException("Failed allocating memory for return value - Executor.Execute<T>");
				}

				if (lstMnemonics[0].ToLower() == "use64")
					lstMnemonics[retFlagPosition] = $"mov [0x{returnAllocation.BaseAddress.ToInt64():X}], rax";
				else
					lstMnemonics[retFlagPosition] = $"mov [0x{returnAllocation.BaseAddress.ToInt32():X}], eax";
			}
			else
			{
				int lastCallInstructionPosition = lstMnemonics.FindLastIndex(x => x.ToLower().StartsWith("call"));
				if (lastCallInstructionPosition != -1)
				{
					lstMnemonics.Insert(lastCallInstructionPosition + 1,
						lstMnemonics[0].ToLower() == "use64" ? $"mov [0x{returnAllocation.BaseAddress.ToInt64():X}], rax" : $"mov [0x{returnAllocation.BaseAddress.ToInt64():X}], eax");
				}
				else
				{
					int retnIdx = lstMnemonics.FindLastIndex(x => x.ToLower().StartsWith("retn"));
					if (retnIdx != -1)
					{
						if (lstMnemonics[0].ToLower() == "use64")
							lstMnemonics[retnIdx] = $"mov [0x{returnAllocation.BaseAddress.ToInt64():X}], rax";
						else
							lstMnemonics[retnIdx] = $"mov [0x{returnAllocation.BaseAddress.ToInt32():X}], eax";
					}
					else
					{
						lstMnemonics.Add(lstMnemonics[0].ToLower() == "use64" ? $"mov [0x{returnAllocation.BaseAddress.ToInt64():X}], rax" : $"mov [0x{returnAllocation.BaseAddress.ToInt64():X}], eax");
					}
				}
			}

			byte[] asm = _mainReference.Assembler.Assemble(lstMnemonics.ToArray());
			_mainReference.Writer.WriteBytes(alloc.BaseAddress, asm);
			try
			{
				IntPtr threadHandle = Win32.PInvoke.CreateRemoteThread(_mainReference.ProcessHandle, IntPtr.Zero, 0, alloc.BaseAddress, IntPtr.Zero, (uint) Classes.ThreadCreationFlags.Run, out IntPtr threadId);
				if (threadHandle == IntPtr.Zero)
				{
					alloc.ReleaseMemory();
					returnAllocation?.ReleaseMemory();
					throw new InvalidOperationException("Failed using CreateRemoteThread - Executor.Execute<T>");
				}

				Win32.PInvoke.WaitForSingleObject(threadHandle, (uint) Classes.WaitForSingleObjectResult.INFINITE);
				Win32.PInvoke.CloseHandle(threadHandle);

				return _mainReference.Reader.Read<T>(returnAllocation.BaseAddress);
			}
			finally
			{
				returnAllocation?.ReleaseMemory();
				alloc.ReleaseMemory();
			}
		}
	}
}
