
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpGui;

public partial class MainWindow : Gtk.Window, IToxSharpFriend, IToxSharpGroup
{
	protected class Size
	{
		// SRLSY, PPL.
		public int width, height;
	}

	protected Size size;

	public MainWindow (): base (Gtk.WindowType.Toplevel)
	{
		Build();

		ToxConnected(false);
		TreesSetup();

		// C1ECE4620571325F8211649B462EC1B3398B87FF13B363ACD682F5A27BC4FD46937EAAF221F2
		size = new Size();
		size.width = 0;
		size.height = 0;

//		ConfigureEvent += OnConfigure;
		
		entry1.KeyReleaseEvent += OnEntryKeyReleased;
		DeleteEvent += OnDeleteEvent;

		Focus = entry1;
	}

	[GLib.ConnectBefore]
	protected void OnConfigure(object o, ConfigureEventArgs args)
	{
		if (size.width != args.Event.Width)
		{
			size.width = args.Event.Width;
			WidthNew(size.width);
		}
	}

/*****************************************************************************/

	protected int CommandFriendHandle()
	{
		int len = entry1.Text.Length;

		int space1 = entry1.Text.IndexOf(' ');
		if (space1 <= 0)
		{
			TextAdd(SourceType.System, 0, "SYSTEM", "Command requires a name or ID.");
			return -1;
		}

		if ((len > 2) && (entry1.Text.Substring(0, 3) == "/fa"))
		{
			string ID, message = "";

			int spaceagain = entry1.Text.IndexOf(' ', space1 + 1);
			if (spaceagain > 0)
			{
				ID = entry1.Text.Substring(space1 + 1, spaceagain - (space1 + 1));
				message = entry1.Text.Substring(spaceagain);
			}
			else
				ID = entry1.Text.Substring(space1 + 1);

			if (ID.Length != 2 * ToxSharp.ID_LEN_BINARY)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "/fa(dd) <ID>: ID must be exactly " +
				        					   			2 * ToxSharp.ID_LEN_BINARY + " characters long. (Your input's ID was " + ID.Length + "characters long.)");
				return -1;
			}

			ToxKey key = new ToxKey(ID);
			int friendid = toxsharp.ToxFriendAdd(key, message);
			if (friendid < 0)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "Command wasn't successful: " + friendid);
				return -1;
			}

			if (message.Length > 0)
				TextAdd(SourceType.System, 0, "SYSTEM", "Friend request sent:\n" +
														"Message: \"" + message + "\n" +
														"ID: " + ID);
			else
				TextAdd(SourceType.System, 0, "SYSTEM", "Friend request sent to: " + ID);

			toxsharp.ToxFriendInit(friendid);
			return 1;
		}

		if ((len > 2) && (entry1.Text.Substring(0, 3) == "/fr"))
		{
			string keypartial = entry1.Text.Substring(space1 + 1);
			FriendTreeNode friend2delete = null;
			int candidates2deletenum = datastorage.FindFriendsWithKeyStartingWithID(keypartial, out friend2delete);
			if (candidates2deletenum == 1)
			{
				int code = toxsharp.ToxFriendDel(friend2delete.key);
				if (code != 0)
				{
					TextAdd(SourceType.System, 0, "SYSTEM", "Command wasn't successful: " + code);
					return -1;
				}
	
				StoreDelete(friend2delete);
				TextAdd(SourceType.System, 0, "SYSTEM", "No longer a friend of yours: " + friend2delete.name + "\n" + friend2delete.key.str);
	
				return 1;
			}
			else if (candidates2deletenum == 0)
				TextAdd(SourceType.System, 0, "SYSTEM", "The given ID wasn't found among your friends.");
			else if (candidates2deletenum > 1)
				TextAdd(SourceType.System, 0, "SYSTEM", "ID fragment fits to more than one friend.");
			else if (candidates2deletenum < 0)
				TextAdd(SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");

			return -1;
		}

		int space2 = entry1.Text.IndexOf(' ', space1 + 1);
		string actionstr = entry1.Text.Substring(space2 + 1);
		if ((space1 <= 0) || (space2 <= 0) || (actionstr.Length == 0))
		{
			TextAdd(SourceType.System, 0, "SYSTEM", "Not enough arguments: Need a target (name or ID) and additional text.");
			return -1;
		}

		string nameorkeypartial = entry1.Text.Substring(space1 + 1, space2 - space1 - 1);
		FriendTreeNode friend = null;
		int foundnum = datastorage.FindFriendsWithNameOrKeyStartingWithID(nameorkeypartial, out friend);
		if (foundnum == 0)
			TextAdd(SourceType.System, 0, "SYSTEM", "The intended audience wasn't found among your friends.");
		else if (foundnum > 1)
			TextAdd(SourceType.System, 0, "SYSTEM", "The name/ID fits to more than one friend.");
		else if (foundnum < 0)
			TextAdd(SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");

		if ((len > 2) && (entry1.Text.Substring(0, 3) == "/fd"))
		{
			TextAdd(SourceType.Friend, friend.id, "ACTION", toxsharp.ToxNameGet() + " " + actionstr);
			toxsharp.ToxFriendAction(friend.id, actionstr);

			return 1;
		}

		if ((len > 2) && (entry1.Text.Substring(0, 3) == "/fm"))
		{
			if (toxsharp.ToxFriendMessage(friend.id, actionstr) != 0)
			{
				TextAdd(SourceType.Friend, friend.id, toxsharp.ToxNameGet(), actionstr);
				return 1;
			}
			else
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "Failed to queue the message. Sorry.");
				return -1;
			}
		}

		return 0;
	}

	protected int CommandGroupHandle()
	{
		TextAdd(SourceType.System, 0, "DEBUG", "TODO: Group commands not implemented.");
		return 0;
	}

	protected int CommandHandle()
	{
		int len = entry1.Text.Length;
		if ((len > 1) && (entry1.Text.Substring(0, 2) == "/i"))
		{
			string id = toxsharp.ToxSelfID();
			TextAdd(SourceType.System, 0, "SYSTEM", "Your id has been copied into the clipboard:\n" + id);
			Clipboard clipboard;

			// not X11: clipboard, X11: selection (but only pasteable with middle mouse button)
			clipboard = Clipboard.Get(Gdk.Selection.Clipboard);
			clipboard.Text = id;

			// X11: pasteable
			clipboard = Clipboard.Get(Gdk.Selection.Primary);
			clipboard.Text = id;

			return 1;
		}

		if ((len > 1) && (entry1.Text.Substring(0, 2) =="/n"))
		{
			int space = entry1.Text.IndexOf(' ');
			if (space <= 0)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "/name <name>: No name given.");
				return -1;				
			}

			string namestr = entry1.Text.Substring(space + 1);
			if (namestr.Length == 0)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "/name <name>: No name given.");
				return -1;				
			}

			if (toxsharp.ToxNameSet(namestr) == 1)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "Your name is now " + namestr + ".");
				TitleSet();
				return 1;
			}

			TextAdd(SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");
			return -1;
		}

		if ((len > 1) && (entry1.Text.Substring(0, 2) == "/h"))
		{
			int extra = entry1.Text.IndexOf(' ');
			if (extra > 0)
			{
				if (entry1.Text.Substring(extra + 1, 1) == "f")
				{
					TextAdd(SourceType.System, 0, "SYSTEM", "/fa(dd) <ID>             : Sends a friend request to the given ID.");
					TextAdd(SourceType.System, 0, "SYSTEM", "/fr(emove) <ID>  : Removes the given ID from the list of friends.");
					TextAdd(SourceType.System, 0, "SYSTEM", "/fm(essage) <name or ID> : Sends a message to the given name or ID.");
					TextAdd(SourceType.System, 0, "SYSTEM", "/fd(o) <name or ID>      : Sends an action to the given name.");
					TextAdd(SourceType.System, 0, "SYSTEM", "(TODO) Name or ID can be partial as long as it expands uniquely.");
				}
				if (entry1.Text.Substring(extra + 1, 1) == "g")
				{
					TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Help for this context.");
				}
			}
			else
			{
				string message = "Tox# GUI 0.0.1: Commands start with a slash. (On the main page only commands can be entered.)\n" +
					"On any other page, any input but an 'action' will be sent as typed to the target audience.\n" +
					"/h(elp)           : this help\n" +
					"/i(d)             : copies your ID to the clipboard.\n" +
					"/n(ame) ...       : sets your name\n" +
					"/a(m) <X>           : sets you to one of 'here', 'away', busy'\n" +
					"/s(tate) ...        : sets your state (any text, e.g. 'amused')\n" +
					"/d(o) ...           : sends an action to the current conversation partner\n" +
					"/h(elp) f(riends) : commands related to friends\n" +
					"/h(elp) g(roups)  : commands related to groups";
				TextAdd(SourceType.System, 0, "SYSTEM", message);
			}
			
			return 1;
		}

		int handled = 0;
		if (len > 1)
		{
			if (entry1.Text.Substring(0, 2) == "/f")
				handled = CommandFriendHandle();
			if (entry1.Text.Substring(0, 2) == "/g")
				handled = CommandGroupHandle();
		}

		if (handled == 0)
			TextAdd(SourceType.System, 0, "SYSTEM", "Command not recognized.");

		return handled;
	}

	protected void InputHandle()
	{
		if (entry1.Text.Length == 0)
			return;

		int handled = 0;
		bool slash = entry1.Text[0] == '/';
		bool action = false;
		if (slash)
			action = (entry1.Text.Length > 4) && (entry1.Text.Substring(0, 4) == "/do ");

		if (slash && !action)
			handled = CommandHandle();
		else
		{
			// send to target
			ScrolledWindow scrollwindow = notebook1.CurrentPageWidget as ScrolledWindow;
			NodeView nodeview = scrollwindow.Child as NodeView;
			ListStoreSourceTypeID liststore = nodeview.Model as ListStoreSourceTypeID;
			if (liststore == null)
				TextAdd(SourceType.System, 0, "SYSTEM", "No target for a message on this page. Try '/h' for help.");				
			else
			{
				if (liststore.type == SourceType.Friend)
				{
					if (action)
					{
						handled = 1;
	
						string actionstr = entry1.Text.Substring(4);
						TextAdd(SourceType.Friend, liststore.id, "ACTION", toxsharp.ToxNameGet() + " " + actionstr);
						toxsharp.ToxFriendAction(liststore.id, actionstr);
					}
					else
					{
						if (toxsharp.ToxFriendMessage(liststore.id, entry1.Text) != 0)
						{
							TextAdd(SourceType.Friend, liststore.id, toxsharp.ToxNameGet(), entry1.Text);
							handled = 1;
						}
						else
						{
							TextAdd(SourceType.System, 0, "SYSTEM", "Failed to queue the message. Sorry.");
							handled = -1;
						}
					}
				}
				else if (liststore.type == SourceType.Group)
				{
					TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Sending this in this context.");
				}
				else
					TextAdd(SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");
			}
		}
			
		if (handled == 1)
			entry1.Text = "";
	}

	protected void OnEntryKeyReleased(object o, Gtk.KeyReleaseEventArgs args)
	{
		if (args.Event.Key == Gdk.Key.Up)
		{
			// Combobox, keeping the current input unless a different is selected
			TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Command history.");
		}

		if (args.Event.Key == Gdk.Key.Down)
		{
			// Combobox, keeping the current input unless a different is selected
			TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Command history.");
		}

		if (args.Event.Key == Gdk.Key.Tab)
		{
			// Combobox, popping friends, strangers or groups depending on input
			TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Support input on entering an ID.");
		}

		if (args.Event.Key == Gdk.Key.Return)
			InputHandle();
	}

/*****************************************************************************/

	/* 
	 * other stuff
	 */

	[GLib.ConnectBefore]
	protected void OnDeleteEvent(object o, Gtk.DeleteEventArgs args)
	{
		toxsharp.toxpollthreadrequestend++;
		while(toxsharp.toxpollthreadrequestend < 2)
			System.Threading.Thread.Sleep(100);

		toxsharp.ToxSave();

		Application.Quit();
		args.RetVal = true;
	}
}
