
using System;
using System.Reflection;

namespace ToxSharpBasic
{
	internal class MainClass
	{
		public static Interfaces.IUIFactory UILoad(string uiname)
		{
			string fullname = null;
			if (uiname.ToLower() == "gtk")
				fullname = "ToxSharpGTK.ToxSharpGTK,ToxSharpGuiGTK";
			else if (uiname.ToLower() == "winforms")
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

		protected static bool printdebug = false;

		public static void PrintDebug(string line)
		{
			if (printdebug)
				System.Console.WriteLine("[DBG] " + line);
		}

		public static void Main(string[] args)
		{
			for(int i = 0; i < args.Length; i++)
				if ((args[i] == "-v") || (args[i] == "--verbose"))
					printdebug = true;

			if ((args.Length == 1) && ((args[0].Substring(0, 2) == "-h") || (args[0].Substring(0, 2) == "/h")))
			{
				System.Console.WriteLine("Tox#Gui: C#-based UI for Tox");
				System.Console.WriteLine("Command line: " + System.Environment.CommandLine + " [-h] [-v|--verbose] [--gui=<gtk|winforms|...>] [-d directory] [-f datafile]");
				System.Console.WriteLine("The default or with -d overwritten configuration directory shall contain a DHTservers file to bootstreap the client.");
				System.Console.WriteLine("The datafile for -f must include the complete path, i.e. the default or overwritten configuration directory is NOT prepended.");
				return;
			}

			ToxInterface toxsharp = null;
			try
			{
				toxsharp = new ToxInterface(args);
				MainActual(args, toxsharp);
			}
			catch(Exception e)
			{
				System.Console.WriteLine("Tripped. Unhandled exception follows:\n" + e.Message + "\n\nInner data:\n" + e.InnerException + "\n");
			}
			finally
			{
				if (toxsharp != null)
					toxsharp.ToxStopAndSave();
			}
		}

		internal static void MainActual(string[] args, ToxInterface toxsharp)
		{
			string uinamedefault = "gtk";
			// string uinamedefault = "winforms";

			string uiname = null;
			Interfaces.IUIFactory uifactory = null;
			Interfaces.IUIReactions uiobject = null;
			foreach(string name in args)
				if (name.Substring(0, 6) == "--gui=")
					uiname = name.Substring(6);

			if (uiname != null)
				System.Console.WriteLine("Default UI is " + uinamedefault + ", selected UI is " + uiname + ".");
			else
				uiname = uinamedefault;

			uiname = uiname.ToLower();

			try
			{
				uifactory = UILoad(uiname);
				if (uifactory == null)
				{
					uiname = uiname.ToUpper();
					uifactory = UILoad(uiname);
				}

				if (uifactory != null)
				  uiobject = uifactory.Create();
			}
			catch(Exception e)
			{
				PrintDebug("Failed to initialize UI " + uiname + ", details following...\n" + e.Message + "\nMore details:\n" + e.InnerException + "\n\nExiting...");
				System.Console.WriteLine("Failed to initialize UI " + uiname + ". Exiting...");
				return;
			}

			if (uiobject == null)
			{
				System.Console.WriteLine("Failed to find corresponding UI " + uiname + ". Exiting...");
				return;
			}

			PrintDebug("UI created successfully. Setting all up.\n");
			Interfaces.IUIReactions uireactions = uiobject as Interfaces.IUIReactions;
			if (uireactions == null)
				return;

			PrintDebug("Cleaning old logfiles.\n");
			foreach(string name in System.IO.Directory.EnumerateFiles(".", "*.log"))
				System.IO.File.Delete(name);

			PrintDebug("Creating remaining objects.\n");
			DataStorage datastorage = new DataStorage();
			ToxGlue glue = new ToxGlue();

			PrintDebug("Linking it all together.\n");
			uireactions.Init(glue);
			glue.Init(toxsharp, uireactions, datastorage);
			toxsharp.ToxInit(glue, glue, glue, glue);

			PrintDebug("Bootstrapping into the 'net.\n");
			int bootstrapped_cnt = toxsharp.ToxBootstrap();
			if (bootstrapped_cnt > 0)
				uireactions.TextAdd(Interfaces.SourceType.System , 0, "SYSTEM", "Sent connection requests to " + bootstrapped_cnt + " other clients...");
			else
			{
				if (bootstrapped_cnt == 0)
					uireactions.TextAdd(Interfaces.SourceType.System , 0, "SYSTEM", "No servers found in DHTservers: File is missing or malformed.");
				else
					uireactions.TextAdd(Interfaces.SourceType.System , 0, "SYSTEM", "Failed to send any connection requests to other clients: " + bootstrapped_cnt);

				uireactions.TextAdd(Interfaces.SourceType.System , 0, "SYSTEM", "Most likely, you won't be able to connect to anybody.");
			}

			string selfname = toxsharp.ToxNameGet();
			if (selfname.Length > 0)
				uireactions.TitleUpdate(selfname, toxsharp.ToxSelfID());

			PrintDebug("Running main loop.\n");
			uireactions.Run();
			uifactory.Quit();
		}
	}
}
