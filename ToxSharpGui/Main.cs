
using System;

namespace ToxSharpBasic
{
	interface IMainWindow
	{
		void Init(ToxInterface toxsharp, Interfaces.IDataReactions datareactions, InputHandling inputhandling, Popups popups);
		void Do();
	}

	class MainClass
	{
		public static void Main(string[] args)
		{
			string uiname = "winforms";
			object uiobject = null;
			foreach(string name in args)
				if (name.Substring(0, 6) == "--gui=")
					uiname = name.Substring(6);

			uiname = uiname.ToLower();
			if (uiname == "gtk")
			{
				ToxSharpGTK.GtkMainWindow.Prepare();
				uiobject = new ToxSharpGTK.GtkMainWindow();
			}
			if (uiname == "winforms")
				uiobject = new ToxSharpWinForms.WinFormsMainWindow();

			if (uiobject == null)
				return;

			Interfaces.IUIReactions uireactions = uiobject as Interfaces.IUIReactions;
			IMainWindow uiwindow = uiobject as IMainWindow;
			if ((uireactions == null) || (uiwindow == null))
				return;

			foreach(string name in System.IO.Directory.EnumerateFiles(".", "*.log"))
				System.IO.File.Delete(name);


			DataStorage datastorage = new DataStorage();
			ToxInterface toxsharp = new ToxInterface(args);
			ToxGlue glue = new ToxGlue();

			InputHandling inputhandling = new InputHandling(toxsharp, uireactions, datastorage);
			Popups popups = new Popups(toxsharp, uireactions, datastorage);

			uiwindow.Init(toxsharp, datastorage, inputhandling, popups);
			glue.Init(toxsharp, uireactions, datastorage);
			toxsharp.ToxInit(glue, glue, glue);

			int bootstrapped_cnt = toxsharp.ToxBootstrap();
			if (bootstrapped_cnt <= 0)
				return;

			uireactions.TitleUpdate();
			uireactions.TextAdd(Interfaces.SourceType.System , 0, "SYSTEM", "Sent connection requests to " + bootstrapped_cnt + " other clients...");

			uiwindow.Do();
		}
	}
}
