using System;
using Minimem;
using static Minimem.Classes;

namespace Testing
{
	class Program
	{
		public static Main Library;

		static void Main(string[] args)
		{

			Library = new Main("notepad");
			if (!Library.IsValid) return;

			Library.Detach();
			Console.ReadLine();
		}
	}
}
