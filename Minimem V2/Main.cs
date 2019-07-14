using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using Minimem.Extension_Methods;

namespace Minimem
{
    public class Main
    {
		private IntPtr _handle = IntPtr.Zero;
		private string _processName = "";
		private int _processId = -1;
		private Process _process = null;

		private bool _threadExitFlag = false;
		public Thread CallbackThread { get; private set; }

		public Process ProcessObject
		{
			get => _process;
			private set { }
		}

		public IntPtr ProcessHandle
		{
			get => _handle;
			private set { }
		}

		public int ProcessId
		{
			get => _processId;
			private set { }
		}

		public bool Is64Bit
		{
			get => ProcessObject.Is64Bit();
			private set { }
		}

		public bool IsValid
		{
			get =>
				_handle != IntPtr.Zero
				&& _processName != ""
				&& _processId > 0
				&& _process != default;
			private set { }
		}

		public bool IsRunning
		{
			get
			{
				if (ProcessObject == null) return false;
				return Process.GetProcesses().FirstOrDefault(x => x.Id == ProcessObject.Id) != default;
			}
			private set { }
		}

		public void Refresh()
		{
			if (!IsValid) throw new InvalidProgramException($"Cannot call method \"Refresh\" as there is nothing to refresh!");
			Process _processObject = Process.GetProcesses().FirstOrDefault(proc => proc.Id == _processId);
			_process = _processObject ?? throw new InvalidOperationException("Could not refresh attached process data");
			_processName = _processObject.ProcessName;
			_processId = _processObject.Id;
		}
		public void Suspend()
		{
			if (IsValid)
			{
				bool suspendResult = ProcessObject.Suspend();
			}
		}
		public void Resume()
		{
			if (IsValid)
			{
				bool resumeResult = ProcessObject.Resume();
			}
		}
		
		public Features.Reader Reader { get; private set; }
		public Features.Writer Writer { get; private set; }
		public Features.Logger Logger { get; private set; }
		public Features.Allocator Allocator { get; private set; }
		public Features.Assembler Assembler { get; private set; }
		public Features.Detouring Detours { get; private set; }
		public Features.Injector Injector { get; private set; }
		public Features.Patterns Patterns { get; private set; }
		public Features.Executor Executor { get; private set; }

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
			Extensions._mainReference = this;

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
			Extensions._mainReference = this;

			CallbackThread = new Thread(CallbackLoop)
			{
				IsBackground = true
			};
			CallbackThread.Start();
		}

		public void Detach(bool clearCallbacks = true)
		{
			if (IsValid)
			{
				_processName = "";
				_process = null;
				_processId = -1;

				bool flag = Win32.PInvoke.CloseHandle(_handle);
				if (!flag)
				{
					// Closing of handle failed
				}
				_handle = IntPtr.Zero;

				_threadExitFlag = true;
				bool hasJoined = CallbackThread.IsAlive ? CallbackThread.Join(1000) : true;
				if (hasJoined)
					Debug.WriteLine("Callback thread joined successfully!");
				else
				{
					Debug.WriteLine($"Callback thread did not join successfully, attemping to force abort it");
					if (CallbackThread.IsAlive)
						CallbackThread.Abort();
					Debug.WriteLine($"Abortion Status: {(CallbackThread.IsAlive ? "Failed!" : "Success!")}");
				}

				if (clearCallbacks)
				{
					// Restore stuff here
				}
			} else
			{
				_processName = "";
				_process = null;
				_processId = -1;
				_handle = IntPtr.Zero;

				_threadExitFlag = true;
				bool hasJoined = CallbackThread.IsAlive ? CallbackThread.Join(1000) : true;
				if (hasJoined)
					Debug.WriteLine("Callback thread joined successfully!");
				else
				{
					Debug.WriteLine($"Callback thread did not join successfully, attemping to force abort it");
					if (CallbackThread.IsAlive)
						CallbackThread.Abort();
					Debug.WriteLine($"Abortion Status: {(CallbackThread.IsAlive ? "Failed!" : "Success!")}");
				}

				if (clearCallbacks)
				{
					// Restore stuff here
				}
			}

			Reader = null;
			Writer = null;
			Logger = null;
			Allocator = null;
			Assembler = null;
			Detours = null;
			Injector = null;
			Patterns = null;
			Executor = null;
			Extensions._mainReference = null;
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
