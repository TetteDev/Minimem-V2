using Minimem.Extension_Methods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
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

		public IntPtr FindPattern(string processModule, string pattern, bool resultAbsolute = true)
		{
			if (_mainReference.ProcessHandle == IntPtr.Zero) throw new Exception("Read/Write Handle cannot be Zero");
			if (_mainReference == null) throw new Exception("Reference to Main Class cannot be null");
			if (!_mainReference.IsValid) throw new Exception("Reference to Main Class reported an Invalid State");
			if (string.IsNullOrEmpty(pattern)) return IntPtr.Zero;

			var pm = string.IsNullOrEmpty(processModule) ? _mainReference.ProcessObject.MainModule : _mainReference.ProcessObject.FindProcessModule(processModule, true);

			var tmpSplitPattern = pattern.TrimStart(' ').TrimEnd(' ').Split(' ');

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

			if (tmpMask.Length != tmpPattern.Length)
				throw new ArgumentException($"{nameof(pattern)}.Length != {nameof(tmpMask)}.Length");

			byte[] buff = _mainReference.Reader.ReadBytes(pm.BaseAddress, (IntPtr)pm.ModuleMemorySize);
			int result = 0 - tmpPattern.Length;
			unsafe
			{
				fixed (byte* pPacketBuffer = buff)
				{
					do
					{
						result = HelperMethods.FindPattern(pPacketBuffer, buff.Length, tmpPattern, tmpMask, result + tmpPattern.Length);
						if (result >= 0)
							return resultAbsolute ? IntPtr.Add(pm.BaseAddress, result) : new IntPtr(result);
					} while (result != -1);
				}
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
			if (string.IsNullOrEmpty(pattern)) return new List<IntPtr>();

			var pm = string.IsNullOrEmpty(processModule) ? _mainReference.ProcessObject.MainModule : _mainReference.ProcessObject.FindProcessModule(processModule, true);

			var tmpSplitPattern = pattern.TrimStart(' ').TrimEnd(' ').Split(' ');

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

			if (tmpMask.Length != tmpPattern.Length)
				throw new ArgumentException($"{nameof(pattern)}.Length != {nameof(tmpMask)}.Length");

			byte[] buff = _mainReference.Reader.ReadBytes(pm.BaseAddress, (IntPtr)pm.ModuleMemorySize, Classes.MemoryProtection.ExecuteReadWrite);

			List<IntPtr> results = new List<IntPtr>();
			int result = 0 - tmpPattern.Length;
			unsafe
			{
				fixed (byte* pPacketBuffer = buff)
				{
					do
					{
						result = HelperMethods.FindPattern(pPacketBuffer, buff.Length, tmpPattern, tmpMask, result + tmpPattern.Length);
						if (result >= 0)
							results.Add(resultAbsolute ? IntPtr.Add(pm.BaseAddress, result) : new IntPtr(result));
					} while (result != -1);
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
