
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpGui;

public partial class MainWindow : Gtk.Window, IToxSharpFriend, IToxSharpGroup
{
	public MainWindow (): base (Gtk.WindowType.Toplevel)
	{
		Build();

		TreesSetup();

		// C1ECE4620571325F8211649B462EC1B3398B87FF13B363ACD682F5A27BC4FD46937EAAF221F2

		entry1.KeyReleaseEvent += OnEntryKeyReleased;
		DeleteEvent += OnDeleteEvent;

		Focus = entry1;
	}

/*****************************************************************************/

	protected bool CommandFriendHandle()
	{
		int len = entry1.Text.Length;

		if ((len > 2) && (entry1.Text.Substring(0, 3) == "/fa"))
		{
			int space = entry1.Text.IndexOf(' ');
			if (space <= 0)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "/fa(dd) <ID>: No ID?");
				return false;
			}

			string ID, message = "";

			int spaceagain = entry1.Text.IndexOf(' ', space + 1);
			if (spaceagain > 0)
			{
				ID = entry1.Text.Substring(space + 1, spaceagain - (space + 1));
				message = entry1.Text.Substring(spaceagain);
			}
			else
				ID = entry1.Text.Substring(space + 1);

			if (ID.Length != 2 * ToxSharp.ID_LEN_BINARY)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "/fa(dd) <ID>: ID must be exactly " +
				        					   			ToxSharp.ID_LEN_BINARY + " characters long.");
				return false;
			}

			ToxSharpGui.Key key = new ToxSharpGui.Key(ID);
			int friendid = toxsharp.ToxFriendAdd(key, message);
			if (friendid < 0)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "Command wasn't successful: " + friendid);
				return false;
			}

			if (message.Length > 0)
				TextAdd(SourceType.System, 0, "SYSTEM", "Friend request sent:\n" +
														"Message: \"" + message + "\n" +
														"ID: " + ID);
			else
				TextAdd(SourceType.System, 0, "SYSTEM", "Friend request sent to: " + ID);

			toxsharp.ToxFriendInit(friendid);
			return true;
		}

		if ((len > 2) && ((entry1.Text.Substring(0, 3) == "/fd") || (entry1.Text.Substring(0, 3) == "/fr")))
		{
			int space = entry1.Text.IndexOf(' ');
			if (space <= 0)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "/fr(emove) <ID>: No ID?");
				return false;
			}

			string keypartial = entry1.Text.Substring(space + 1);
			FriendTreeNode friend = null;
			int foundnum = datastorage.FindFriendsWithKeyStartingWithID(keypartial, out friend);
			if (foundnum == 1)
			{
				int code = toxsharp.ToxFriendDel(friend.key);
				if (code != 0)
				{
					TextAdd(SourceType.System, 0, "SYSTEM", "Command wasn't successful: " + code);
					return false;
				}
	
				StoreDelete(friend);
				TextAdd(SourceType.System, 0, "SYSTEM", "No longer a friend of yours: " + friend.name);
	
				return true;
			}
			else if (foundnum == 0)
				TextAdd(SourceType.System, 0, "SYSTEM", "The given ID wasn't found among your friends.");
			else if (foundnum > 1)
				TextAdd(SourceType.System, 0, "SYSTEM", "ID fragment fits to more than one friend.");
			else if (foundnum < 0)
				TextAdd(SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");

			return false;
		}

		TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Not implemented.");
		return false;
	}

	protected bool CommandGroupHandle()
	{
		TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Not implemented.");
		return false;
	}

	protected bool CommandHandle()
	{
		int len = entry1.Text.Length;
		if (entry1.Text[0] != '/')
		{
			TextAdd(SourceType.System, 0, "SYSTEM", "Only commands allowed on this page.");
			return false;
		}

		if (entry1.Text == "/myid")
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

			return true;
		}

		if ((len > 1) && (entry1.Text.Substring(0, 2) == "/h"))
		{
			int extra = entry1.Text.IndexOf(' ');
			if (extra > 0)
			{
				if (entry1.Text.Substring(extra + 1) == "friend")
				{
					TextAdd(SourceType.System, 0, "SYSTEM", "/fa(dd) <ID>: Sends a friend request to the given ID.");
					TextAdd(SourceType.System, 0, "SYSTEM", "/fd(el)|/fr(emove) <ID>: Removes the given ID from the list of friends.");
					TextAdd(SourceType.System, 0, "SYSTEM", "/fm(essage) <ID>: Sends a message to the given ID.");
					TextAdd(SourceType.System, 0, "SYSTEM", "/fm(essage) <name>: Sends a message to the given name.");
					TextAdd(SourceType.System, 0, "SYSTEM", "TODO: ID can be partial as long as it expands uniquely.");
				}
				if (entry1.Text.Substring(extra + 1) == "group")
				{
					TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Help for this context.");
				}
			}
			else
			{
				string message = "Tox# GUI 0.0.1: Commands start with a slash and can only be entered on the first page.\n" +
					"On any other page, any input will be sent as typed to the target audience.\n" +
					"/h(elp) : this help\n" +
					"/myid : copies your ID to the clipboard.\n" +
					"/h(elp) friend(s) : commands related to friends\n" +
					"/h(elp) group(s) : commands related to groups";
				TextAdd(SourceType.System, 0, "SYSTEM", message);
			}
			
			return true;
		}

		bool handled = false;
		if (len > 1)
		{
			if (entry1.Text.Substring(0, 2) == "/f")
				handled = CommandFriendHandle();
			if (entry1.Text.Substring(0, 2) == "/g")
				handled = CommandGroupHandle();
		}

		if (!handled)
			TextAdd(SourceType.System, 0, "SYSTEM", "Command not recognized.");

		return handled;
	}

	protected void InputHandle()
	{
		if (entry1.Text.Length == 0)
			return;

		bool handled = false;
		if (notebook1.Page == 0)
			handled = CommandHandle();
		else
		{
			// send to target
			ScrolledWindow scrollwindow = notebook1.GetNthPage(notebook1.Page) as ScrolledWindow;
			NodeView nodeview = scrollwindow.Child as NodeView;
			ListStoreEx liststore = nodeview.Model as ListStoreEx;
			if (liststore.type == SourceType.Friend)
			{
				handled = true;

				TextAdd(SourceType.Friend, liststore.id, toxsharp.ToxName(), entry1.Text);
				toxsharp.ToxFriendMessage(liststore.id, entry1.Text);
			}

			if (liststore.type == SourceType.Group)
			{
				TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Sending this in this context.");
			}
		}
			
		if (handled)
			entry1.Text = "";
	}

	protected void OnEntryKeyReleased(object o, Gtk.KeyReleaseEventArgs args)
	{
		if (args.Event.Key == Gdk.Key.Up)
		{
			TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Command history.");
		}

		if (args.Event.Key == Gdk.Key.Down)
		{
			TextAdd(SourceType.System, 0, "SYSTEM", "TODO: Command history.");
		}

		if (args.Event.Key == Gdk.Key.Tab)
		{
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
