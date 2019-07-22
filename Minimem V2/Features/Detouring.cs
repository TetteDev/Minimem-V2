using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Minimem.Features
{
	public class Detouring
	{
		private readonly Main _mainReference;

		public Detouring(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Detouring cannot be null");
		}

		public IntPtr CreateCodeCave(IntPtr address, int targetBytesCount,string[] cavemnemonics, Classes.DetourType detourType = Classes.DetourType.JMP, bool x64Process = false)
		{
			int finalDetourMethodType = 1;

			/*
			 * 1: 32Bit Jmp - Relative Address (5 bytes)
			 * 2: 32Bit Push, Ret (6 bytes)
			 * 3: 64bit Jmp Relative Address (12 bytes)
			 * 4: 64bit Push "XCHG" Absolute Address (16 bytes)
			 */

			int BYTES_NEEDED = 5;
			Classes.RemoteMemory codecave = _mainReference.Allocator.AllocateMemory(0x10000);
			if (!codecave.IsValid) return IntPtr.Zero;

			if (x64Process)
			{
				if (detourType == Classes.DetourType.JMP)
				{
					IntPtr relAddressCheck = new IntPtr(codecave.BaseAddress.ToInt64() - address.ToInt64() - BYTES_NEEDED);
					if (relAddressCheck.ToInt64() > Classes.TWO_GIGABYTES || relAddressCheck.ToInt64() < -Classes.TWO_GIGABYTES)
					{
						finalDetourMethodType = 4;
						BYTES_NEEDED = 16;
					}
					else
					{
						finalDetourMethodType = 3;
						BYTES_NEEDED = 12;
					}
				}
			}
			else
			{
				finalDetourMethodType = detourType == Classes.DetourType.JMP ? 1 : 2;
				if (detourType == Classes.DetourType.JMP)
				{
					finalDetourMethodType = 1;
					BYTES_NEEDED = 5;
				}
				else
				{
					finalDetourMethodType = 2;
					BYTES_NEEDED = 6;
				}
			}

			if (targetBytesCount < BYTES_NEEDED) return IntPtr.Zero;
			int NOPS_NEEDED = targetBytesCount - BYTES_NEEDED;
			List<string> nops = new List<string>();

			for(int i = 0; i < NOPS_NEEDED; i++)
				nops.Add("nop");

			IntPtr relAddressToCodeCave32Bit = IntPtr.Zero;
			IntPtr relAddressToCodeCave64Bit = IntPtr.Zero;

			List<string> jumpInMnemonics = new List<string>();
			switch (finalDetourMethodType)
			{
				case 1: // 32Bit Jmp (5 bytes)
					relAddressToCodeCave32Bit = IntPtr.Subtract(codecave.BaseAddress, address.ToInt32() - BYTES_NEEDED);
					jumpInMnemonics.InsertRange(0, new string[]
					{
						"use32",
						$"jmp 0x{relAddressToCodeCave32Bit.ToInt32():X}"
					});
					jumpInMnemonics.AddRange(nops);
					break;

				case 2: // 32Bit PushRet (6 bytes)
					jumpInMnemonics.InsertRange(0, new string[]
					{
						"use32",
						$"push 0x{codecave.BaseAddress.ToInt32():X}",
						"ret"
					});
					jumpInMnemonics.AddRange(nops);
					break;
				case 3:
				case 4: // 64Bit PushRet (16 bytes)
					jumpInMnemonics.InsertRange(0, new string[]
					{
						"use64",
						"push rax",
						$"mov rax, 0x{codecave.BaseAddress.ToInt64():X}",
						"xchg rax, [rsp]",
						"ret"
					});
					jumpInMnemonics.AddRange(nops);
					break;
			}

			byte[] jumpInBytes = _mainReference.Assembler.Assemble(jumpInMnemonics.ToArray());
			List<string> jumpOutMnemonics = new List<string>()
			{
				(x64Process ? "use64" : "use32"),
				"<cavemnemonics>",
				"<jmpout>"
			};

			jumpOutMnemonics.InsertRange(jumpOutMnemonics.IndexOf("<cavemnemonics>"), cavemnemonics);
			jumpOutMnemonics.RemoveAt(jumpOutMnemonics.IndexOf("<cavemnemonics>"));

			if (x64Process)
			{
				jumpOutMnemonics.InsertRange(jumpOutMnemonics.IndexOf("<jmpout>"), new string[]
				{
					"push rax",
					$"mov rax, 0x{IntPtr.Add(address, targetBytesCount + NOPS_NEEDED).ToInt64():X}",
					"xchg rax, [rsp]",
					"ret"
				});
				jumpOutMnemonics.RemoveAt(jumpOutMnemonics.IndexOf("<jmpout>"));
			}
			else
			{
				jumpOutMnemonics.InsertRange(jumpOutMnemonics.IndexOf("<jmpout>"), new string[]
				{
					$"push 0x{IntPtr.Add(address, targetBytesCount + NOPS_NEEDED).ToInt32():X}",
					"ret"
				});
				jumpOutMnemonics.RemoveAt(jumpOutMnemonics.IndexOf("<jmpout>"));
			}

			byte[] originalBytes = _mainReference.Reader.ReadBytes(address, new IntPtr(targetBytesCount)); // Read Original Bytes
			_mainReference.Writer.WriteBytes(codecave.BaseAddress, originalBytes); // Write original bytes top of code cave

			byte[] jmpOutBytes = _mainReference.Assembler.Assemble(jumpOutMnemonics.ToArray());  // Assemble Codecave bytes
			_mainReference.Writer.WriteBytes(IntPtr.Add(codecave.BaseAddress, originalBytes.Length), jmpOutBytes); // WriteCodeCave bytes
			_mainReference.Writer.WriteBytes(address, jumpInBytes, Classes.MemoryProtection.ExecuteReadWrite); // write jump in bytes to target address

			return codecave.BaseAddress;
		}

		public Classes.DetourCallback GenerateDetour(IntPtr address, int targetBytesCount, string[] mnemonics, Classes.InterceptHookExecuteDelegate executedEvent = null, bool saveOriginalBytes = true, bool putOriginalBytesAfterMnemonics = true)
		{
			if (_mainReference.CallbackThread == null)
			{
				_mainReference.CallbackThread = new Thread(_mainReference.CallbackLoop)
				{
					IsBackground = true
				};
				_mainReference.CallbackThread.Start();
			}

			int bytesNeeded = _mainReference.Is64Bit ? 16 : 5;
			if (targetBytesCount < bytesNeeded) throw new InvalidOperationException($"{(_mainReference.Is64Bit ? "A 64Bit Process need atleast 16 bytes of headroom" : "A 32Bit Process need atleast 5 bytes of headroom")}");

			byte[] originalBytes = _mainReference.Reader.ReadBytes(address, new IntPtr(targetBytesCount), Classes.MemoryProtection.ExecuteReadWrite);
			//int codecaveallocationSize = _mainReference.Assembler.Assemble(new[] { _mainReference.Is64Bit ? "use64" : "use32"}.Concat(mnemonics).ToArray()).Length + originalBytes.Length + 16 /* + SizeOf Register Struct */;
			Classes.RemoteMemory caveAllocation = _mainReference.Allocator.AllocateMemory(0x10000);
			if (!caveAllocation.IsValid) throw new InvalidOperationException($"Failed allocating {0x10000} bytes for the code cave");

			int nopsNeeded = targetBytesCount - bytesNeeded;
			List<string> jumpInMnemonics = _mainReference.Is64Bit
				? new List<string>()
				{
					"use64",
					"push rax",
					$"mov rax, 0x{caveAllocation.BaseAddress.ToInt64():X}",
					"xchg rax, [rsp]",
					"ret"
				}
				: new List<string>()
				{
					"use32",
					$"jmp 0x{((uint)caveAllocation.BaseAddress - (uint)address):X}"
				};

			for (int iNop = 0; iNop < nopsNeeded; iNop++)
				jumpInMnemonics.Add("nop");

			List<string> codeCaveMnemonics = _mainReference.Is64Bit
				? new List<string>()
				{
					"use64",
					"<mnemonics>",
				}
				: new List<string>()
				{
					"use32",
					"<mnemonics>",
				};

			if (mnemonics.Length > 1)
			{
				codeCaveMnemonics.InsertRange(codeCaveMnemonics.FindIndex(x => x == "<mnemonics>"), mnemonics);
				codeCaveMnemonics.RemoveAt(codeCaveMnemonics.FindIndex(x => x == "<mnemonics>"));
			}
			else
			{
				codeCaveMnemonics.RemoveAt(codeCaveMnemonics.FindIndex(x => x == "<mnemonics>"));
			}
			

			List<string> jumpOutMnemonics = _mainReference.Is64Bit
				? new List<string>()
				{
					"use64",
					"<registers>",
					"<hitcounter>",
					"push rax",
					$"mov rax, 0x{((ulong)address + (ulong)bytesNeeded + (ulong)nopsNeeded):X}",
					"xchg rax, [rsp]",
					//"pop rax",
					"ret"
				}
				: new List<string>()
				{
					"use32",
					//$"jmp 0x{((uint)address + (nopsNeeded + 5) - (uint)caveAllocation.BaseAddress + codecaveallocationSize + bytesNeeded):X}"
					"<registers>",
					"<hitcounter>",
					$"push 0x{((uint)address + bytesNeeded + nopsNeeded):X}",
					"ret"
				};


#if x86
			Classes.RemoteMemory registerStructurePointer = _mainReference.Allocator.AllocateMemory((uint) Marshal.SizeOf<Classes.Registers32>());
#else
			Classes.RemoteMemory registerStructurePointer = _mainReference.Allocator.AllocateMemory((ulong)Marshal.SizeOf<Classes.Registers64>());
#endif
			if (!registerStructurePointer.IsValid)
				throw new Exception("registerStructurePointer ptr is null");

#if x86
			Classes.RemoteMemory hitCounterPointer = _mainReference.Allocator.AllocateMemory(4);
#else
			Classes.RemoteMemory hitCounterPointer = _mainReference.Allocator.AllocateMemory(8);
#endif
			if (!hitCounterPointer.IsValid) throw new Exception("hitcounter ptr is null");

			if (_mainReference.Is64Bit)
			{
				jumpOutMnemonics.InsertRange(jumpOutMnemonics.FindIndex(x => x == "<registers>"), new List<string>()
				{
					/*
					$"movsxd [dword {registerStructurePointer.BaseAddress}],eax",
					$"movsxd [dword {registerStructurePointer.BaseAddress + 4}],ebx",
					$"movsxd [dword {registerStructurePointer.BaseAddress + 8}],ecx",
					$"movsxd [dword {registerStructurePointer.BaseAddress + 12}],edx",
					$"movsxd [dword {registerStructurePointer.BaseAddress + 16}],edi",
					$"movsxd [dword {registerStructurePointer.BaseAddress + 20}],esi",
					$"movsxd [dword {registerStructurePointer.BaseAddress + 24}],ebp",
					$"movsxd [dword {registerStructurePointer.BaseAddress + 28}],esp",
					*/

					$"mov [qword {registerStructurePointer.BaseAddress + 36}],rax",
					"nop",
					
					// Moving rbx into imm64
					"push rax",
					$"mov rax, {registerStructurePointer.BaseAddress}",
					"add rax, 44",
					"mov [rax], rbx",
					"pop rax",
					"nop",

					// Moving rcx into imm64
					"push rax",
					$"mov rax, {registerStructurePointer.BaseAddress}",
					"add rax, 52",
					"mov [rax], rcx",
					"pop rax",
					"nop",

					// Moving rdx into imm64
					"push rax",
					$"mov rax, {registerStructurePointer.BaseAddress}",
					"add rax, 60",
					"mov [rax], rdx",
					"pop rax",
					"nop",

					// Moving rdi into imm64
					"push rax",
					$"mov rax, {registerStructurePointer.BaseAddress}",
					"add rax, 68",
					"mov [rax], rdi",
					"pop rax",
					"nop",

					// Moving rsi into imm64
					"push rax",
					$"mov rax, {registerStructurePointer.BaseAddress}",
					"add rax, 76",
					"mov [rax], rsi",
					"pop rax",

					// Moving rbp into imm64
					"push rax",
					$"mov rax, {registerStructurePointer.BaseAddress}",
					"add rax, 84",
					"mov [rax], rbp",
					"pop rax",
					"nop",

					// Moving rsp into imm64
					"push rax",
					$"mov rax, {registerStructurePointer.BaseAddress}",
					"add rax, 92",
					"mov [rax], rsp",
					"pop rax",
					"nop"

					//$"mov [qword {registerStructurePointer.BaseAddress + 44}],rbx",
					//$"mov [qword {registerStructurePointer.BaseAddress + 52}],rcx",
					//$"mov [qword {registerStructurePointer.BaseAddress + 60}],rdx",
					//$"mov [qword {registerStructurePointer.BaseAddress + 68}],rdi",
					//$"mov [qword {registerStructurePointer.BaseAddress + 76}],rsi",
					//$"mov [qword {registerStructurePointer.BaseAddress + 84}],rbp",
					//$"mov [qword {registerStructurePointer.BaseAddress + 92}],rsp"
				});
			}
			else
			{
				jumpOutMnemonics.InsertRange(jumpOutMnemonics.FindIndex(x => x == "<registers>"), new List<string>()
				{
					$"mov [{registerStructurePointer.BaseAddress}],eax",
					$"mov [{registerStructurePointer.BaseAddress + 4}],ebx",
					$"mov [{registerStructurePointer.BaseAddress + 8}],ecx",
					$"mov [{registerStructurePointer.BaseAddress + 12}],edx",
					$"mov [{registerStructurePointer.BaseAddress + 16}],edi",
					$"mov [{registerStructurePointer.BaseAddress + 20}],esi",
					$"mov [{registerStructurePointer.BaseAddress + 24}],ebp",
					$"mov [{registerStructurePointer.BaseAddress + 28}],esp",

					$"mov [{registerStructurePointer.BaseAddress + 30}],cs",
					$"mov [{registerStructurePointer.BaseAddress + 32}],ss",
					$"mov [{registerStructurePointer.BaseAddress + 34}],ds",
					$"mov [{registerStructurePointer.BaseAddress + 36}],es",
					$"mov [{registerStructurePointer.BaseAddress + 38}],fs",
					$"mov [{registerStructurePointer.BaseAddress + 40}],gs",
				});
			}
			jumpOutMnemonics.RemoveAt(jumpOutMnemonics.FindIndex(x => x == "<registers>"));
			if (_mainReference.Is64Bit)
			{
				jumpOutMnemonics.InsertRange(jumpOutMnemonics.FindIndex(x => x == "<hitcounter>"), new string[]
				{
					"push rax",
					//$"lea rax, [{hitCounterPointer.BaseAddress}]",
					$"mov rax, [qword {hitCounterPointer.BaseAddress}]",
					"inc qword [rax]",
					"pop rax", // restore original value of rax
					$"mov [qword {hitCounterPointer.BaseAddress}],rax"
				});
				jumpOutMnemonics.RemoveAt(jumpOutMnemonics.FindIndex(x => x == "<hitcounter>"));
			}
			else
			{
				jumpOutMnemonics[jumpOutMnemonics.FindIndex(x => x == "<hitcounter>")] = $"inc dword [{hitCounterPointer.BaseAddress}]";
			}

			byte[] jumpInBytes = _mainReference.Assembler.Assemble(jumpInMnemonics.ToArray());
			byte[] CodeCaveBytes = _mainReference.Assembler.Assemble(codeCaveMnemonics.ToArray());
			byte[] jumpOutBytes = _mainReference.Assembler.Assemble(jumpOutMnemonics.ToArray());

			return new Classes.DetourCallback(address, 
				caveAllocation.BaseAddress, 
				targetBytesCount, 
				originalBytes, 
				jumpInBytes, 
				jumpOutBytes,
				CodeCaveBytes,
				saveOriginalBytes, 
				putOriginalBytesAfterMnemonics,
				caveAllocation,
				hitCounterPointer,
				registerStructurePointer,
				executedEvent,
				_mainReference);
		}
	}
}
