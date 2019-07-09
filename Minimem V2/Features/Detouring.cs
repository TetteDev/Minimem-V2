using System;
using System.Threading;

namespace Minimem.Features
{
	public class Detouring
	{
		private Main _mainReference;

		public Detouring(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Detouring cannot be null");
		}
	}
}
