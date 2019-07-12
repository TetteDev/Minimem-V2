using System;
using System.Collections.Generic;
using System.Linq;

namespace Minimem.Features
{
	public class Assembler
	{
		private Main _mainReference;
		public Assembler(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Assembler cannot be null");
		}

#if x86
		public byte[] Assemble(string[] mnemonics, int rebaseOrigin = 0)
#else
		public byte[] Assemble(string[] mnemonics, long rebaseOrigin = 0)
#endif
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (mnemonics == null || mnemonics.Length < 1) throw new InvalidOperationException("Passed Mnemonics to Assembler.Assemble was null or empty!");

			Reloaded.Assembler.Assembler asm = new Reloaded.Assembler.Assembler();
			if (rebaseOrigin != 0)
			{
				List<string> mnemonicsList = mnemonics.ToList();
				if (mnemonicsList[0].ToLower() == "use32" || mnemonicsList[0].ToLower() == "use64")
					mnemonicsList.Insert(1, $"org 0x{rebaseOrigin:X}");
				else
					mnemonicsList.Insert(0, $"org 0x{rebaseOrigin:X}");

				byte[] bytes_rebased = asm.Assemble(mnemonicsList);
				asm.Dispose();
				return bytes_rebased;
			}

			byte[] bytes = asm.Assemble(mnemonics);
			asm.Dispose();
			return bytes;
		}
	}
}
