
using System;

namespace ToxSharpBasic
{
	internal class MainClass
	{
		public static void Main(string[] args)
		{
			string uiname = "gtk";
			// string uiname = "winforms";
			System.Console.WriteLine("Default UI is " + uiname + ". Use --gui=<...> to select another.");

			object uiobject = null;
			foreach(string name in args)
				if (name.Substring(0, 6) == "--gui=")
				{
					uiname = name.Substring(6);
					System.Console.WriteLine("Selected UI is " + uiname + ".");
				}

			uiname = uiname.ToLower();

			try
			{
				if (uiname == "gtk")
				{
					ToxSharpGTK.GtkMainWindow.Prepare();
					uiobject = new ToxSharpGTK.GtkMainWindow();
				}
				if (uiname == "winforms")
					uiobject = new ToxSharpWinForms.WinFormsMainWindow();
			}
			catch(Exception e)
			{
				System.Console.WriteLine("Failed to initialize UI " + uiname + ":" + e.Message + " => " + e.InnerException + ". Exiting...");
				return;
			}

			if (uiobject == null)
			{
				System.Console.WriteLine("Failed to find corresponding UI " + uiname + ". Exiting...");
				return;
			}

			Interfaces.IUIReactions uireactions = uiobject as Interfaces.IUIReactions;
			if (uireactions == null)
				return;

			foreach(string name in System.IO.Directory.EnumerateFiles(".", "*.log"))
				System.IO.File.Delete(name);

			DataStorage datastorage = new DataStorage();
			ToxInterface toxsharp = new ToxInterface(args);
			ToxGlue glue = new ToxGlue();

			uireactions.Init(glue);
			glue.Init(toxsharp, uireactions, datastorage);
			toxsharp.ToxInit(glue, glue, glue, glue);

			int bootstrapped_cnt = toxsharp.ToxBootstrap();
			if (bootstrapped_cnt <= 0)
				return;

			string selfname = toxsharp.ToxNameGet();
			if (selfname.Length > 0)
				uireactions.TitleUpdate(selfname, toxsharp.ToxSelfID());
			uireactions.TextAdd(Interfaces.SourceType.System , 0, "SYSTEM", "Sent connection requests to " + bootstrapped_cnt + " other clients...");

			uireactions.Run();
		}
	}
}
