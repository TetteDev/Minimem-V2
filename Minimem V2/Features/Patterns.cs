using Minimem.Extension_Methods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

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
#if x86
					results.Add(new IntPtr(resultAbsolute ? pm.BaseAddress.ToInt32() + pCur - pat.Length + 1 : pCur - pat.Length + 1));
#else
					results.Add(new IntPtr(resultAbsolute ? pm.BaseAddress.ToInt64() + pCur - pat.Length + 1 : pCur - pat.Length + 1));
#endif
					matchCount = 0;
				}
			}

			return results;
		}
		public Task<List<IntPtr>> AsyncFindPatternMany(string processModule, string pattern, bool resultAbsolute = true)
		{
			return Task.Run(() => FindPatternMany(processModule, pattern, resultAbsolute));
		}

		public List<Classes.MultiAobResultItem> CEFindPattern(string[][] byteArrays, bool readable = true, bool writable = false, bool executable = true, long start = 0, long end = long.MaxValue)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (!readable && !writable && !executable) return new List<Classes.MultiAobResultItem>();

			var memRegionList = new List<Classes.MemoryRegionResult>();
			var itms = new List<Tuple<Classes.MultiAobItem, ConcurrentBag<long>>>();

			foreach (var aob in byteArrays)
			{
				var tmpSplitPattern = aob[0].TrimStart(' ').TrimEnd(' ').Split(' ');

				var tmpPattern = new byte[tmpSplitPattern.Length];
				var tmpMask = new byte[tmpSplitPattern.Length];

				for (var i = 0; i < tmpSplitPattern.Length; i++)
				{
					var ba = tmpSplitPattern[i];

					if (ba == "??" || ba.Length == 1 && ba == "?")
					{
						tmpMask[i] = 0x00;
						tmpSplitPattern[i] = "0x00";
					}
					else if (char.IsLetterOrDigit(ba[0]) && ba[1] == '?')
					{
						tmpMask[i] = 0xF0;
						tmpSplitPattern[i] = ba[0] + "0";
					}
					else if (char.IsLetterOrDigit(ba[1]) && ba[0] == '?')
					{
						tmpMask[i] = 0x0F;
						tmpSplitPattern[i] = "0" + ba[1];
					}
					else
					{
						tmpMask[i] = 0xFF;
					}
				}

				for (var i = 0; i < tmpSplitPattern.Length; i++)
					tmpPattern[i] = (byte)(Convert.ToByte(tmpSplitPattern[i], 16) & tmpMask[i]);

				var itm = new Classes.MultiAobItem
				{
					ArrayOfBytesString = aob[0],
					Mask = tmpMask,
					Pattern = tmpPattern,
					OptionalIdentifier = string.IsNullOrEmpty(aob[1]) ? "NO_IDENTIFIER_SPECIFIED" : aob[1]
				};

				itms.Add(new Tuple<Classes.MultiAobItem, ConcurrentBag<long>>(itm, new ConcurrentBag<long>()));
			}

			Win32.PInvoke.GetSystemInfo(out Classes.SYSTEM_INFO sys_info);

			var proc_min_address = sys_info.minimumApplicationAddress;
			var proc_max_address = sys_info.maximumApplicationAddress;

			start = start < (long)proc_min_address.ToUInt64() ? (long)proc_min_address.ToUInt64() : start;
			end = end > (long)proc_max_address.ToUInt64() ? (long)proc_max_address.ToUInt64() : end;

			var currentBaseAddress = new UIntPtr((ulong)start);

			const uint MEM_COMMIT = 0x00001000;
			const uint PAGE_GUARD = 0x100;
			const uint PAGE_NOACCESS = 0x01;
			const uint MEM_PRIVATE = 0x20000;
			const uint MEM_IMAGE = 0x1000000;
			const uint PAGE_READONLY = 0x02;
			const uint PAGE_READWRITE = 0x04;
			const uint PAGE_WRITECOPY = 0x08;
			const uint PAGE_EXECUTE_READWRITE = 0x40;
			const uint PAGE_EXECUTE_WRITECOPY = 0x80;
			const uint PAGE_EXECUTE = 0x10;
			const uint PAGE_EXECUTE_READ = 0x20;

			while (Win32.PInvoke.VirtualQueryExCustom(_mainReference.ProcessHandle, currentBaseAddress, out var memInfo).ToUInt64() != 0 &&
			       currentBaseAddress.ToUInt64() < (ulong) end &&
			       currentBaseAddress.ToUInt64() + memInfo.RegionSize >
			       currentBaseAddress.ToUInt64())
			{
				var isValid = memInfo.State == MEM_COMMIT;
#if x86
				isValid &= memInfo.BaseAddress.ToUInt64() < proc_max_address.ToUInt64();
#else
				isValid &= new UIntPtr(memInfo.BaseAddress).ToUInt64() < proc_max_address.ToUInt64();
#endif
				isValid &= (memInfo.Protect & PAGE_GUARD) == 0;
				isValid &= (memInfo.Protect & PAGE_NOACCESS) == 0;
				isValid &= memInfo.Type == MEM_PRIVATE || memInfo.Type == MEM_IMAGE;

				if (isValid)
				{
					var isReadable = (memInfo.Protect & PAGE_READONLY) > 0;

					var isWritable = (memInfo.Protect & PAGE_READWRITE) > 0 ||
					                 (memInfo.Protect & PAGE_WRITECOPY) > 0 ||
					                 (memInfo.Protect & PAGE_EXECUTE_READWRITE) > 0 ||
					                 (memInfo.Protect & PAGE_EXECUTE_WRITECOPY) > 0;

					var isExecutable = (memInfo.Protect & PAGE_EXECUTE) > 0 ||
					                   (memInfo.Protect & PAGE_EXECUTE_READ) > 0 ||
					                   (memInfo.Protect & PAGE_EXECUTE_READWRITE) > 0 ||
					                   (memInfo.Protect & PAGE_EXECUTE_WRITECOPY) > 0;

					isReadable &= readable;
					isWritable &= writable;
					isExecutable &= executable;

					isValid &= isReadable || isWritable || isExecutable;
				}

				if (!isValid)
				{
#if x86
					currentBaseAddress = new UIntPtr(memInfo.BaseAddress.ToUInt64() + memInfo.RegionSize);
#else
					currentBaseAddress = new UIntPtr(new UIntPtr(memInfo.BaseAddress).ToUInt64() + memInfo.RegionSize);
#endif
					continue;
				}

				var memRegion = new Classes.MemoryRegionResult
				{
					CurrentBaseAddress = currentBaseAddress,
					RegionSize = (long)memInfo.RegionSize,
#if x86
					RegionBase = memInfo.BaseAddress,
#else
					RegionBase = new UIntPtr(memInfo.BaseAddress)
#endif
				};

#if x86
				currentBaseAddress = new UIntPtr(memInfo.BaseAddress.ToUInt64() + memInfo.RegionSize);
#else
				currentBaseAddress = new UIntPtr(new UIntPtr(memInfo.BaseAddress).ToUInt64() + memInfo.RegionSize);
#endif

				if (memRegionList.Count > 0)
				{
					var previousRegion = memRegionList[memRegionList.Count - 1];

					if ((long)previousRegion.RegionBase + previousRegion.RegionSize == (long)memInfo.BaseAddress)
					{
						memRegionList[memRegionList.Count - 1] = new Classes.MemoryRegionResult
						{
							CurrentBaseAddress = previousRegion.CurrentBaseAddress,
							RegionBase = previousRegion.RegionBase,
							RegionSize = previousRegion.RegionSize + (long)memInfo.RegionSize
						};

						continue;
					}
				}

				memRegionList.Add(memRegion);
			}

			Parallel.ForEach(memRegionList,
				(item, parallelLoopState, index) => { HelperMethods.CompareScanMulti(item, ref itms, _mainReference.ProcessHandle); });

			return itms.Select(itm1 => new Classes.MultiAobResultItem
			{
				Identifier = itm1.Item1.OptionalIdentifier,
				Pattern = itm1.Item1.ArrayOfBytesString,
				Results = itm1.Item2.OrderBy(c=> c).ToList(),
				FirstResult = itm1.Item2.OrderBy(c => c).ToList().FirstOrDefault() == 0L ? IntPtr.Zero : new IntPtr(itm1.Item2.OrderBy(c => c).ToList().FirstOrDefault()),
				FirstResultAsHexString = itm1.Item2.OrderBy(c => c).ToList().FirstOrDefault() == 0 ? "0x0" : $"0x{itm1.Item2.OrderBy(c => c).ToList().FirstOrDefault():X8}"
			}).ToList();
		}
		public Task<List<Classes.MultiAobResultItem>> AsyncCEFindPattern(string[][] byteArrays, bool readable = true, bool writable = false, bool executable = true, long start = 0, long end = long.MaxValue)
		{
			return Task.Run(() => CEFindPattern(byteArrays, readable, writable, executable, start, end));
		}
	}
}
