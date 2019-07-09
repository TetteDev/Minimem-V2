using System;

namespace Minimem.Features
{
	public class Assembler
	{
		private Main _mainReference;
		public Assembler(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Assembler cannot be null");
		}

		public byte[] Assemble(string[] mnemonics)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (mnemonics == null || mnemonics.Length < 1) throw new InvalidOperationException("Passed Mnemonics to Assembler.Assemble was null or empty!");

			Reloaded.Assembler.Assembler asm = new Reloaded.Assembler.Assembler();
			byte[] bytes = asm.Assemble(mnemonics);
			asm.Dispose();
			return bytes;
		}
	}
}
