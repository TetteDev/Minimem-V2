using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Minimem.Extension_Methods;

namespace Minimem.Features
{
	public class Patterns
	{
		private readonly Main _mainReference;

		public Patterns(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Patterns cannot be null");
		}

		private IntPtr FindPattern(ProcessModule processModule, string pattern, bool resultAbsolute = true)
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
			if (buffer == null || buffer.Length != processModule.ModuleMemorySize)
			{
				Debug.WriteLine("Failed Reading bytes - FindPattern");
				return IntPtr.Zero;
			}
			
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
		private Task<IntPtr> AsyncFindPattern(ProcessModule processModule, string pattern, bool resultAbsolute = true)
		{
			return Task.Run(() => FindPattern(processModule, pattern, resultAbsolute));
		}

		private List<IntPtr> FindPatternMany(ProcessModule processModule, string pattern, bool resultAbsolute = true)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (processModule == null || string.IsNullOrEmpty(pattern)) return new List<IntPtr>();

			List<IntPtr> lpResults = new List<IntPtr>();
			List<byte> bytesPattern = new List<byte>();
			List<bool> boolMask = new List<bool>();

			byte[] bytesBuffer = _mainReference.Reader.ReadBytes(processModule.BaseAddress, new IntPtr(processModule.ModuleMemorySize));
			if (bytesBuffer.Length < 1) throw new Exception("Failed reading bytes for region 'processModule'");

			foreach (string s in pattern.Split(' '))
			{
				if (string.IsNullOrEmpty(s)) continue;
				if (s == "?" || s == "??")
				{
					bytesPattern.Add(0x0);
					boolMask.Add(false);
				}
				else
				{
					byte b;
					if (byte.TryParse(s, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out b))
					{
						bytesPattern.Add(Convert.ToByte(s, 16));
						boolMask.Add(true);
					}
					else
					{
						break;
					}
				}
			}

			int intIx, intIy = 0;
			int intPatternLength = bytesPattern.Count;
			int intDataLength = bytesBuffer.Length - intPatternLength;

			for (intIx = 0; intIx < intDataLength; intIx++)
			{
				var boolFound = true;
				for (intIy = 0; intIy < intPatternLength; intIy++)
				{
					if (boolMask[intIy] && bytesPattern[intIy] != bytesBuffer[intIx + intIy])
					{
						boolFound = false;
						break;
					}
				}

				if (boolFound)
				{
#if x86
					lpResults.Add(!resultAbsolute ? new IntPtr(intIx) : new IntPtr(processModule.BaseAddress.ToInt32() + intIx));
#else
					lpResults.Add(!resultAbsolute ? new IntPtr(intIx) : new IntPtr(processModule.BaseAddress.ToInt64() + intIx));
#endif
				}
			}

			return lpResults;
		}
		private Task<List<IntPtr>> AsyncFindPatternMany(ProcessModule processModule, string pattern, bool resultAbsolute = true)
		{
			return Task.Run(() => FindPatternMany(processModule, pattern, resultAbsolute));
		}

		public IntPtr FindPattern(string processModule, string pattern, bool resultAbsolute = true)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (string.IsNullOrEmpty(processModule) || string.IsNullOrEmpty(pattern)) return IntPtr.Zero;

			ProcessModule pm = _mainReference.ProcessObject.FindProcessModule(processModule, true);
			if (pm == default) throw new NullReferenceException($"Cannot find a process module inside process \"{_mainReference.ProcessObject.ProcessName}\" with the name \"{processModule}\"");
			byte[] buff = _mainReference.Reader.ReadBytes(pm.BaseAddress, new IntPtr(pm.ModuleMemorySize));
			string[] pat = pattern.TrimStart(' ').TrimEnd(' ').Split(' ');
			int matchCount = 0;

#if x86
			for (int pCur = 0; pCur < pm.ModuleMemorySize; pCur++)
#else
			for (long pCur = 0L; pCur < pm.ModuleMemorySize; pCur++)
#endif
			{
				if (pat[matchCount] == "?" || pat[matchCount] == "??")
					matchCount++;
				else if (!pat[matchCount].Contains("?") && buff[pCur] == Convert.ToByte(pat[matchCount], 16))
					matchCount++;
				else
				{
					if (pat[matchCount].Contains("?"))
					{
						(string LeftNibble, string RightNibble) patSplit = pat[matchCount].SplitAt(1);
						(string LeftNibble, string RightNibble) buffSplit = $"{buff[pCur]:X}".SplitAt(1);

						if (patSplit.LeftNibble == "?" && (string.Equals(patSplit.RightNibble,buffSplit.RightNibble, StringComparison.CurrentCultureIgnoreCase) || string.IsNullOrEmpty(buffSplit.RightNibble)) ||
						    patSplit.RightNibble == "?" && (string.Equals(patSplit.LeftNibble, buffSplit.LeftNibble, StringComparison.CurrentCultureIgnoreCase) || string.IsNullOrEmpty(buffSplit.LeftNibble)))
							matchCount++;
						else
							matchCount = 0;
					}
					else
					{
						matchCount = 0;
					}
				} 
				
				if (matchCount >= pat.Length)
					return new IntPtr(resultAbsolute ? pm.BaseAddress.ToInt64() + pCur - pat.Length + 1 : pCur - pat.Length + 1);
			}

			return IntPtr.Zero;
		}
		public Task<IntPtr> AsyncFindPattern(string processModule, string pattern, bool resultAbsolute = true)
		{
			return Task.Run(() => FindPattern(processModule, pattern, resultAbsolute));
		}

		public List<IntPtr> FindPatternMany(string processModule, string pattern, bool resultAbsolute = true)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (string.IsNullOrEmpty(processModule) || string.IsNullOrEmpty(pattern)) return new List<IntPtr>();

			ProcessModule pm = _mainReference.ProcessObject.FindProcessModule(processModule, true);
			if (pm == default) throw new NullReferenceException($"Cannot find a process module inside process \"{_mainReference.ProcessObject.ProcessName}\" with the name \"{processModule}\"");
			byte[] buff = _mainReference.Reader.ReadBytes(pm.BaseAddress, new IntPtr(pm.ModuleMemorySize));
			string[] pat = pattern.TrimStart(' ').TrimEnd(' ').Split(' ');
			int matchCount = 0;

			List<IntPtr> results = new List<IntPtr>();
#if x86
			for (int pCur = 0; pCur < pm.ModuleMemorySize; pCur++)
#else
			for (long pCur = 0L; pCur < pm.ModuleMemorySize; pCur++)
#endif
			{
				if (pat[matchCount] == "?" || pat[matchCount] == "??")
					matchCount++;
				else if (!pat[matchCount].Contains("?") && buff[pCur] == Convert.ToByte(pat[matchCount], 16))
					matchCount++;
				else
				{
					if (pat[matchCount].Contains("?"))
					{
						(string LeftNibble, string RightNibble) patSplit = pat[matchCount].SplitAt(1);
						(string LeftNibble, string RightNibble) buffSplit = $"{buff[pCur]:X}".SplitAt(1);

						if (patSplit.LeftNibble == "?" && (string.Equals(patSplit.RightNibble, buffSplit.RightNibble, StringComparison.CurrentCultureIgnoreCase) || string.IsNullOrEmpty(buffSplit.RightNibble)) ||
							patSplit.RightNibble == "?" && (string.Equals(patSplit.LeftNibble, buffSplit.LeftNibble, StringComparison.CurrentCultureIgnoreCase) || string.IsNullOrEmpty(buffSplit.LeftNibble)))
							matchCount++;
						else
							matchCount = 0;
					}
					else
					{
						matchCount = 0;
					}
				}

				if (matchCount >= pat.Length)
				{
					results.Add(new IntPtr(resultAbsolute ? pm.BaseAddress.ToInt64() + pCur - pat.Length + 1 : pCur - pat.Length + 1));
					matchCount = 0;
				}
			}

			return results;
		}
		public Task<List<IntPtr>> AsyncFindPatternMany(string processModule, string pattern, bool resultAbsolute = true)
		{
			return Task.Run(() => FindPatternMany(processModule, pattern, resultAbsolute));
		}
	}
}
