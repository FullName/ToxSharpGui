
using System;
using Gtk;

namespace ToxSharpGui
{
	public class Interfaces
	{
		public enum SourceType { Friend, Group, System, Debug };

		public interface IReactions
		{
			void TitleUpdate();
			void TreeAdd(HolderTreeNode holder);
			void TreeUpdate();

			void ClipboardSend(string text);
			bool CurrentTypeID(out SourceType type, out UInt16 id);
			void TextAdd(SourceType type, UInt16 id, string source, string text);

			void Quit();
		}
	}

	class MainClass
	{
		public static void Main(string[] args)
		{
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
