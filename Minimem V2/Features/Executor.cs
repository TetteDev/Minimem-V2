using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Minimem.Features
{
	public class Executor
	{
		private readonly Main _mainReference;

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

			Classes.WaitForSingleObjectResult result = (Classes.WaitForSingleObjectResult)Win32.PInvoke.WaitForSingleObject(threadHandle, (uint)Classes.WaitForSingleObjectResult.INFINITE);
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
				returnAllocation = _mainReference.Allocator.AllocateMemory((uint)Marshal.SizeOf<T>());
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
				returnAllocation = _mainReference.Allocator.AllocateMemory((uint)Marshal.SizeOf(typeof(T)));
				if (!returnAllocation.IsValid)
				{
					alloc.ReleaseMemory();
					throw new InvalidOperationException("Failed allocating memory for return value - Executor.Execute<T>");
				}

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
				IntPtr threadHandle = Win32.PInvoke.CreateRemoteThread(_mainReference.ProcessHandle, IntPtr.Zero, 0, alloc.BaseAddress, IntPtr.Zero, (uint)Classes.ThreadCreationFlags.Run, out IntPtr threadId);
				if (threadHandle == IntPtr.Zero)
				{
					alloc.ReleaseMemory();
					returnAllocation?.ReleaseMemory();
					throw new InvalidOperationException("Failed using CreateRemoteThread - Executor.Execute<T>");
				}

				Classes.WaitForSingleObjectResult result = (Classes.WaitForSingleObjectResult)Win32.PInvoke.WaitForSingleObject(threadHandle, (uint)Classes.WaitForSingleObjectResult.INFINITE);
				bool exitCodeResult = Win32.PInvoke.GetExitCodeThread(threadHandle, out uint lpExitCode);
				Win32.PInvoke.CloseHandle(threadHandle);

				return _mainReference.Reader.Read<T>(returnAllocation.BaseAddress);
			}
			finally
			{
				returnAllocation?.ReleaseMemory();
				alloc.ReleaseMemory();
			}
		}

		public T ExecuteTest<T>(string[] mnemonics) where T : struct
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			byte[] asm = _mainReference.Assembler.Assemble(mnemonics);

#if x86
			Classes.RemoteMemory alloc = _mainReference.Allocator.AllocateMemory((uint)asm.Length);
#else
			Classes.RemoteMemory alloc = _mainReference.Allocator.AllocateMemory((ulong)asm.Length);
#endif
			if (!alloc.IsValid) throw new InvalidOperationException("Failed allocating memory function body - Executor.Execute<T>(string[])");
			_mainReference.Writer.WriteBytes(alloc.BaseAddress, asm);
			try
			{
				IntPtr threadHandle = Win32.PInvoke.CreateRemoteThread(_mainReference.ProcessHandle, IntPtr.Zero, 0, alloc.BaseAddress, IntPtr.Zero, (uint)Classes.ThreadCreationFlags.Run, out IntPtr threadId);
				if (threadHandle == IntPtr.Zero)
				{
					alloc.ReleaseMemory();
					throw new InvalidOperationException("Failed using CreateRemoteThread - Executor.Execute<T>(string[])");
				}

				Classes.WaitForSingleObjectResult result = (Classes.WaitForSingleObjectResult)Win32.PInvoke.WaitForSingleObject(threadHandle, (uint)10000);
				if (result == Classes.WaitForSingleObjectResult.WAIT_TIMEOUT)
				{
					Win32.PInvoke.CloseHandle(threadHandle);
					alloc.ReleaseMemory();
					throw new TimeoutException("Thread Timed Out (Exceeded 10 seconds)");
				}

				bool exitCodeResult = Win32.PInvoke.GetExitCodeThread(threadHandle, out uint lpExitCode);
				Win32.PInvoke.CloseHandle(threadHandle);

				bool IsIntPtr = typeof(T) == typeof(IntPtr);
				Type RealType = typeof(T);
				TypeCode TypeCode = Type.GetTypeCode(RealType);
				int Size = TypeCode == TypeCode.Boolean ? 1 : Marshal.SizeOf(RealType);

#if x86
				bool CanBeStoredInRegisters = IsIntPtr ||
				                         TypeCode == TypeCode.Boolean ||
				                         TypeCode == TypeCode.Byte ||
				                         TypeCode == TypeCode.Char ||
				                         TypeCode == TypeCode.Int16 ||
				                         TypeCode == TypeCode.Int32 ||
				                         TypeCode == TypeCode.Int64 ||
				                         TypeCode == TypeCode.SByte ||
				                         TypeCode == TypeCode.Single ||
				                         TypeCode == TypeCode.UInt16 ||
				                         TypeCode == TypeCode.UInt32;
#else
				bool CanBeStoredInRegisters = IsIntPtr ||
				                         TypeCode == TypeCode.Int64 ||
				                         TypeCode == TypeCode.UInt64 ||
				                         TypeCode == TypeCode.Boolean ||
				                         TypeCode == TypeCode.Byte ||
				                         TypeCode == TypeCode.Char ||
				                         TypeCode == TypeCode.Int16 ||
				                         TypeCode == TypeCode.Int32 ||
				                         TypeCode == TypeCode.Int64 ||
				                         TypeCode == TypeCode.SByte ||
				                         TypeCode == TypeCode.Single ||
				                         TypeCode == TypeCode.UInt16 ||
				                         TypeCode == TypeCode.UInt32;
#endif

				return !exitCodeResult
					? default
					: (HelperMethods.ByteArrayToStructure<T>(CanBeStoredInRegisters ? 
						BitConverter.GetBytes(lpExitCode) : 
						_mainReference.Reader.ReadBytes(new IntPtr(lpExitCode), new IntPtr(Size))));
			}
			finally
			{
				alloc.ReleaseMemory();
			}
		}

		public T Execute<T>(IntPtr functionAddress, Classes.CallingConventionsEnum callingConvention, params dynamic[] parameters) where T : struct
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (functionAddress == IntPtr.Zero) return default;

			List<string> mnemonics = new List<string>();
			List<Classes.RemoteMemory> parameterAllocations = new List<Classes.RemoteMemory>();
			try
			{
				mnemonics = HelperMethods.GenerateFunctionMnemonics(functionAddress, parameters.ToList(), callingConvention, _mainReference, typeof(T), _mainReference.Is64Bit, out parameterAllocations);
			}
			catch (Exception e)
			{
				throw e;
			}

			Classes.RemoteMemory alloc = _mainReference.Allocator.AllocateMemory(0x10000);
			if (!alloc.IsValid)
			{
				foreach (var parameteralloc in parameterAllocations)
					parameteralloc.ReleaseMemory();

				throw new InvalidOperationException("Failed allocating memory for assembled bytes - Executor.Execute<T>(IntPtr,CallingConvention,Params)");
			}

			byte[] asm = _mainReference.Assembler.Assemble(mnemonics.ToArray(), alloc.BaseAddress.ToInt32());
			

			_mainReference.Writer.WriteBytes(alloc.BaseAddress, asm);

			IntPtr threadHandle = Win32.PInvoke.CreateRemoteThread(_mainReference.ProcessHandle, IntPtr.Zero, 0, alloc.BaseAddress, IntPtr.Zero, (uint)Classes.ThreadCreationFlags.Run, out IntPtr threadId);
			if (threadHandle == IntPtr.Zero)
			{
				alloc.ReleaseMemory();
				foreach (var parameteralloc in parameterAllocations)
					parameteralloc.ReleaseMemory();

				throw new InvalidOperationException("Failed using CreateRemoteThread - Executor.Execute<T>(IntPtr,CallingConvention, dynamic Params[])");
			}

			Classes.WaitForSingleObjectResult result = (Classes.WaitForSingleObjectResult)Win32.PInvoke.WaitForSingleObject(threadHandle, 3000);
			if (result == Classes.WaitForSingleObjectResult.WAIT_TIMEOUT)
			{
				foreach (var parameteralloc in parameterAllocations)
					parameteralloc.ReleaseMemory();

				alloc.ReleaseMemory();
				throw new TimeoutException("Thread did not return within 3 seconds");
			}

			bool exitCodeResult = Win32.PInvoke.GetExitCodeThread(threadHandle, out uint lpExitCode);
			Win32.PInvoke.CloseHandle(threadHandle);

			alloc.ReleaseMemory();
			foreach (var paramAlloc in parameterAllocations)
				paramAlloc.ReleaseMemory();

			try
			{

				bool IsIntPtr = typeof(T) == typeof(IntPtr);
				Type RealType = typeof(T);
				TypeCode TypeCode = Type.GetTypeCode(RealType);
				int Size = TypeCode == TypeCode.Boolean ? 1 : Marshal.SizeOf(RealType);

#if x86
				bool CanBeStoredInRegisters = IsIntPtr ||
				                         TypeCode == TypeCode.Boolean ||
				                         TypeCode == TypeCode.Byte ||
				                         TypeCode == TypeCode.Char ||
				                         TypeCode == TypeCode.Int16 ||
				                         TypeCode == TypeCode.Int32 ||
				                         TypeCode == TypeCode.Int64 ||
				                         TypeCode == TypeCode.SByte ||
				                         TypeCode == TypeCode.Single ||
				                         TypeCode == TypeCode.UInt16 ||
				                         TypeCode == TypeCode.UInt32;
#else
				bool CanBeStoredInRegisters = IsIntPtr ||
				                              TypeCode == TypeCode.Int64 ||
				                              TypeCode == TypeCode.UInt64 ||
				                              TypeCode == TypeCode.Boolean ||
				                              TypeCode == TypeCode.Byte ||
				                              TypeCode == TypeCode.Char ||
				                              TypeCode == TypeCode.Int16 ||
				                              TypeCode == TypeCode.Int32 ||
				                              TypeCode == TypeCode.Int64 ||
				                              TypeCode == TypeCode.SByte ||
				                              TypeCode == TypeCode.Single ||
				                              TypeCode == TypeCode.UInt16 ||
				                              TypeCode == TypeCode.UInt32;
#endif

				return !exitCodeResult
					? default
					: (HelperMethods.ByteArrayToStructure<T>(CanBeStoredInRegisters ? BitConverter.GetBytes(lpExitCode) : _mainReference.Reader.ReadBytes(new IntPtr(lpExitCode), new IntPtr(Size))));
			}
			catch
			{
				// swallow
				return default;
			}
		}
	}
}
