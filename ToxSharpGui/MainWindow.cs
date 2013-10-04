
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
	}

/*****************************************************************************/

	protected void OnEntryKeyReleased(object o, Gtk.KeyReleaseEventArgs args)
	{
		if (args.Event.Key == Gdk.Key.Tab)
		{
			// TODO: if we are on the first page, parse for command/target and expand/suggest
		}

		if (args.Event.Key == Gdk.Key.Return)
		{
			if (notebook1.Page == 0)
			{
				// parse as command
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
				}
			}
			else
			{
				// send to target
				ScrolledWindow scrollwindow = notebook1.GetNthPage(notebook1.Page) as ScrolledWindow;
				NodeView nodeview = scrollwindow.Child as NodeView;
				ListStoreEx liststore = nodeview.Model as ListStoreEx;
				if (liststore.type == SourceType.Friend)
				{
					TextAdd(SourceType.Friend, liststore.id, toxsharp.ToxName(), entry1.Text);
					toxsharp.ToxFriendMessage(liststore.id, entry1.Text);
				}
			}

			entry1.Text = "";
		}
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
