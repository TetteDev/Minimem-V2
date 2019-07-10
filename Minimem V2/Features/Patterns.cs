using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Minimem.Features
{
	public class Patterns
	{
		private Main _mainReference;

		public Patterns(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Patterns cannot be null");
		}

		public IntPtr FindPattern(ProcessModule processModule, string pattern, bool resultAbsolute = true)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (processModule == null || string.IsNullOrEmpty(pattern)) return IntPtr.Zero;

			var list = new List<byte>();
			var list2 = new List<bool>();
			var array = pattern.Split(' ');
#if x86
			int refBufferStartAddress = resultAbsolute ? processModule.BaseAddress.ToInt32() : 0;
#else
			long refBufferStartAddress = resultAbsolute ? processModule.BaseAddress.ToInt64() : 0;
#endif
			byte[] buffer = _mainReference.Reader.ReadBytes(processModule.BaseAddress, new IntPtr(processModule.ModuleMemorySize));

			var num = 0;
			if (0 < array.Length)
				do
				{
					var text = array[num];
					if (!string.IsNullOrEmpty(text))
						if (text != "?" && text != "??")
						{
							byte b;
							if (!byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out b))
								break;
							list.Add(Convert.ToByte(text, 16));
							list2.Add(true);
						}
						else
						{
							list.Add(0);
							list2.Add(false);
						}
					num++;
				} while (num < array.Length);
			var count = list.Count;
			var num2 = buffer.Length - count;
			var num3 = 0;
			if (0 < num2)
			{
				for (; ; )
				{
					var num4 = 0;
					if (0 >= count)
						break;
					while (!list2[num4] || list[num4] == buffer[num4 + num3])
					{
						num4++;
						if (num4 >= count)
							return new IntPtr(refBufferStartAddress + num3);
					}
					num3++;
					if (num3 >= num2)
						return IntPtr.Zero;
				}
				return new IntPtr(refBufferStartAddress + num3);
			}
			return IntPtr.Zero;
		}
	}
}
