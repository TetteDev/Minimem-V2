using System;

namespace Minimem.Features
{
	public class Executor
	{
		private Main _mainReference;

		public Executor(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Executor cannot be null");
		}
	}
}
