
using System;
using Gtk;

using ToxSharpGui;

namespace ToxSharpBasic
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			foreach(string name in System.IO.Directory.EnumerateFiles(".", "*.log"))
				System.IO.File.Delete(name);

			Application.Init();

			ToxSharp toxsharp = new ToxSharp(args);

			MainWindow win = new MainWindow(toxsharp);
			toxsharp.ToxInit(win, win, win);

			int bootstrapres = toxsharp.ToxBootstrap();
			if (bootstrapres <= 0)
				return;

			win.TitleUpdate();
			win.TextAdd(Interfaces.SourceType.System , 0, "SYSTEM", "Sent connection requests to " + bootstrapres + " other clients...");

			win.Show();
			Application.Run();
		}
	}
}
