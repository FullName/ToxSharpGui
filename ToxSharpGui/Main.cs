
using System;
using Gtk;

namespace ToxSharpGui
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Init();
			MainWindow win = new MainWindow ();

			ToxSharp toxsharp = new ToxSharp(win);
			win.toxsharp = toxsharp;

			int bootstrapres = toxsharp.ToxBootstrap();
			if (bootstrapres <= 0)
				return;

			win.Title = "Tox#: " + toxsharp.ToxName() + " connecting...";
			win.TextAdd(MainWindow.SourceType.System , 0, "SYSTEM", "Sent connection requests to " + bootstrapres + " other clients...");

			win.Show();
			Application.Run();
		}
	}
}
