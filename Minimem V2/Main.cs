using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace Minimem
{
    public class Main
    {
		private IntPtr _handle = IntPtr.Zero;
		private string _processName = "";
		private int _processId = -1;
		private Process _process = null;

		private bool _threadExitFlag = false;
		public Thread CallbackThread;

		public Process ProcessObject => _process;
		public IntPtr ProcessHandle => _handle;
		public int ProcessId => _processId;

		public bool IsValid =>
			_handle != IntPtr.Zero
			&& _processName != ""
			&& _processId > 0
			&& _process != default;
		public void Refresh()
		{
			if (!IsValid) throw new InvalidProgramException($"Cannot call method \"Refresh\" as there is nothing to refresh!");
			Process _processObject = Process.GetProcesses().FirstOrDefault(proc => proc.Id == _processId);
			if (_processObject == default) throw new InvalidOperationException("Could not refresh attached process data");
			_process = _processObject;
			_processName = _processObject.ProcessName;
			_processId = _processObject.Id;
		}

		public ProcessModule FindProcessModule(string moduleName)
		{
			if (string.IsNullOrEmpty(moduleName)) throw new Exception($"Module Name cannot be null or empty!");
			if (!IsValid) throw new Exception($"Reference to Main Class reported an Invalid State");

			foreach (ProcessModule pm in ProcessObject.Modules)
			{
				if (pm.ModuleName.ToLower() == moduleName)
					return pm;
			}
			throw new Exception($"Cannot find any process module with name \"{moduleName}\"");
		}

		public Features.Reader Reader;
		public Features.Writer Writer;
		public Features.Logger Logger;
		public Features.Allocator Allocator;
		public Features.Assembler Assembler;
		public Features.Detouring Detours;
		public Features.Injector Injector;
		public Features.Patterns Patterns;
		public Features.Executor Executor;

		public Main(string processName)
		{
			if (string.IsNullOrEmpty(processName)) throw new InvalidOperationException($"Parameter \"processName\" for constructor of Minimem.Main cannot be empty!");
			int processId = HelperMethods.TranslateProcessNameIntoProcessId(processName);
			if (processId == -1) throw new Exception($"Cannot find a process with process name \"{processName}\"");
			IntPtr handle = Win32.PInvoke.OpenProcess(Enumerations.ProcessAccessFlags.Enumeration.All, false, processId);
			if (handle == IntPtr.Zero) throw new InvalidOperationException("OpenProcess(uint,IntPtr) returned zero");
			_handle = handle;
			_process = Process.GetProcesses().First(proc => proc.Id == processId);
			_processId = processId;
			_processName = processName;

			Reader = new Features.Reader(this);
			Writer = new Features.Writer(this);
			Logger = new Features.Logger(this);
			Allocator = new Features.Allocator(this);
			Assembler = new Features.Assembler(this);
			Detours = new Features.Detouring(this);
			Injector = new Features.Injector(this);
			Patterns = new Features.Patterns(this);
			Executor = new Features.Executor(this);

			CallbackThread = new Thread(CallbackLoop)
			{
				IsBackground = true
			};
			CallbackThread.Start();
		}

		public Main(int processId)
		{
			if (processId < 0) throw new InvalidOperationException($"Parameter \"processId\" for constructor of Minimem.Main cannot be less or equal to zero!");
			IntPtr handle = Win32.PInvoke.OpenProcess(Enumerations.ProcessAccessFlags.Enumeration.All, false, processId);
			if (handle == IntPtr.Zero) throw new InvalidOperationException("OpenProcess(uint,IntPtr) returned zero");
			_process = Process.GetProcesses().First(proc => proc.Id == processId);
			_processName = _process.ProcessName;
			_handle = handle;
			_processId = processId;

			Reader = new Features.Reader(this);
			Writer = new Features.Writer(this);
			Logger = new Features.Logger(this);
			Allocator = new Features.Allocator(this);
			Assembler = new Features.Assembler(this);
			Detours = new Features.Detouring(this);
			Injector = new Features.Injector(this);
			Patterns = new Features.Patterns(this);
			Executor = new Features.Executor(this);

			CallbackThread = new Thread(CallbackLoop)
			{
				IsBackground = true
			};
			CallbackThread.Start();
		}

		[Obsolete("Note To Developer: Dont forget to null out all feature instances")]
		public void Detach(bool clearCallbacks = true)
		{
			if (IsValid)
			{
				_processName = "";
				_process = null;
				_processId = -1;

				bool flag = Win32.PInvoke.CloseHandle(_handle);
				_handle = IntPtr.Zero;

				_threadExitFlag = true;
				bool hasJoined = CallbackThread.IsAlive ? CallbackThread.Join(1000) : true;
				if (hasJoined)
					Debug.WriteLine("Callback thread joined successfully!");
				else
					throw new TimeoutException("Callback Thread did not join within 1000 ms");

				if (clearCallbacks)
				{
					if (CallbackThread.IsAlive)
						CallbackThread.Abort();

					// Restore stuff here
				}
			} else
			{
				_processName = "";
				_process = null;
				_processId = -1;
				_handle = IntPtr.Zero;
			}
		}

		public void CallbackLoop()
		{
			Debug.WriteLine("CallbackThread been started");
			while (!_threadExitFlag)
			{
				// Loop here

				Thread.Sleep(100);
			}
			Debug.WriteLine($"CallbackThread has returned");
		}
    }
}
