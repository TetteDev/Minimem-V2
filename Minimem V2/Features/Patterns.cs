using System;

namespace Minimem.Features
{
	public class Patterns
	{
		private Main _mainReference;

		public Patterns(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Patterns cannot be null");
		}
	}
}
