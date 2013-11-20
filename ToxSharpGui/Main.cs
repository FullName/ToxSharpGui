
using System;
using System.Reflection;

namespace ToxSharpBasic
{
	internal class MainClass
	{
		public static Interfaces.IUIFactory UILoad(string uiname)
		{
			string fullname = null;
			if (uiname == "gtk")
				fullname = "ToxSharpGTK.ToxSharpGTK,ToxSharpGuiGTK";
			else if (uiname == "winforms")
				fullname = "ToxSharpWinForms.ToxSharpWinForms,ToxSharpGuiWinForms";
			else
				fullname = "ToxSharp" + uiname + ".ToxSharp" + uiname + ",ToxSharpGui" + uiname;

			Type assemblyType = Type.GetType(fullname);
			if (assemblyType == null)
				return null;

			Type[] argTypes = new Type[] {};
			ConstructorInfo cInfo = assemblyType.GetConstructor(argTypes);
			if (cInfo == null)
				return null;

			object o = cInfo.Invoke(null);
			if (o == null)
				return null;

			return o as Interfaces.IUIFactory;
		}

		public static void Main(string[] args)
		{
			string uiname = "gtk";
			// string uiname = "winforms";
			System.Console.WriteLine("Default UI is " + uiname + ". Use --gui=<...> to select another.");

			Interfaces.IUIFactory uifactory = null;
			Interfaces.IUIReactions uiobject = null;
			foreach(string name in args)
			{
				System.Console.Write(" arg[] = \"" + name + "\"|");
				if (name.Substring(0, 6) == "--gui=")
					uiname = name.Substring(6);
			}

			System.Console.WriteLine(" => Selected UI is " + uiname + ".");
			uiname = uiname.ToLower();

			try
			{
				uifactory = UILoad(uiname);
				if (uifactory != null)
				  uiobject = uifactory.Create();
			}
			catch(Exception e)
			{
				System.Console.WriteLine("Failed to initialize UI " + uiname + ", details following...\n" + e.Message + "\nMore details:\n" + e.InnerException + "\n\nExiting...");
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
			uifactory.Quit();
		}
	}
}
