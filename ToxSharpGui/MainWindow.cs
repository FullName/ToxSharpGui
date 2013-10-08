
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpGui;

public partial class MainWindow : Gtk.Window, IToxSharpFriend, IToxSharpGroup, Interfaces.IReactions
{
	protected class Size
	{
		// SRLSY, PPL.
		public int width, height;
	}

	protected Size size;

	protected InputHandling inputhandling;
	protected Popups popups;

	public MainWindow (): base (Gtk.WindowType.Toplevel)
	{
		inputhandling = new InputHandling(this, toxsharp, datastorage);
		popups = new Popups(this, toxsharp, datastorage);

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

	public void TitleUpdate()
	{
		string name = toxsharp.ToxNameGet();
		string selfid = toxsharp.ToxSelfID();
		Title = "Tox# - " + name + " [" + selfid + "]";
	}

	public void ClipboardSend(string text)
	{
		Clipboard clipboard;

		// not X11: clipboard, X11: selection (but only pasteable with middle mouse button)
		clipboard = Clipboard.Get(Gdk.Selection.Clipboard);
		clipboard.Text = text;

		// X11: pasteable
		clipboard = Clipboard.Get(Gdk.Selection.Primary);
		clipboard.Text = text;
	}

	public void TreeUpdate()
	{
		treeview1.QueueDraw();
	}

	public bool CurrentTypeID(out Interfaces.SourceType type, out UInt16 id)
	{
		// send to target
		ScrolledWindow scrollwindow = notebook1.CurrentPageWidget as ScrolledWindow;
		NodeView nodeview = scrollwindow.Child as NodeView;
		ListStoreSourceTypeID liststore = nodeview.Model as ListStoreSourceTypeID;
		if (liststore == null)
		{
			type = 0;
			id = 0;
			return false;
		}

		type = liststore.type;
		id = liststore.id;
		return true;
	}

	protected void OnEntryKeyReleased(object o, Gtk.KeyReleaseEventArgs args)
	{
		inputhandling.Do(entry1.Text, args.Event.Key);
	}

/*****************************************************************************/

	public void Quit()
	{
		toxsharp.toxpollthreadrequestend++;
		while(toxsharp.toxpollthreadrequestend < 2)
			System.Threading.Thread.Sleep(100);

		toxsharp.ToxSave();

		Application.Quit();
	}

/*****************************************************************************/

	/* 
	 * other stuff
	 */

	[GLib.ConnectBefore]
	protected void OnDeleteEvent(object o, Gtk.DeleteEventArgs args)
	{
		Quit();
		args.RetVal = true;
	}
}
