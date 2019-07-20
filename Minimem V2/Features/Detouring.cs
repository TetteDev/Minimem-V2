using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;

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
		
	}
}
