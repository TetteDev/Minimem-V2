using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minimem.Features
{
	public class Logger
	{
		private Main _mainReference;

		public Logger(Main main)
		{
			_mainReference = main ?? throw new Exception($"Parameter \"main\" for constructor of Features.Logger cannot be null");
		}
	}
}
