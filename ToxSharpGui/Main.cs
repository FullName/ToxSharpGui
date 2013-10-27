
using System;
using Gtk;

namespace ToxSharpGui
{
	public class Interfaces
	{
		public enum SourceType { Friend, Group, System, Debug };

		// independence of GUI toolkit: required reactions
		public interface IReactions
		{
			// main window: title
			void TitleUpdate();

			// left side: tree
			void TreeAdd(HolderTreeNode holder);
			void TreeUpdate();

			// external: clipboard
			void ClipboardSend(string text);

			// right side: multi-tab
			bool CurrentTypeID(out SourceType type, out UInt16 id);
			void TextAdd(SourceType type, UInt16 id, string source, string text);

			void Quit();
		}
	}

	class MainClass
	{
		public static void Main(string[] args)
		{
			foreach(string name in System.IO.Directory.EnumerateFiles(".", "*.log"))
				System.IO.File.Delete(name);

			Application.Init();

			ToxSharp toxsharp = new ToxSharp(args);

			MainWindow win = new MainWindow(toxsharp);
			toxsharp.ToxInit(win);

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
