
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpBasic;

namespace ToxSharpGTK
{
	public class HolderTreeNode : Gtk.TreeNode
	{
		public TypeIDTreeNode typeid;

		public HolderTreeNode(TypeIDTreeNode typeid)
		{
			this.typeid = typeid;
		}

		[Obsolete]
		public static HolderTreeNode Create(TypeIDTreeNode typeid)
		{
			return new HolderTreeNode(typeid);
		}

		public static HolderTreeNode HeaderNew(TypeIDTreeNode.EntryType type)
		{
			TypeIDTreeNode typeid = new HeaderTreeNode(type);
			return new HolderTreeNode(typeid);
		}
	}

	public partial class GtkMainWindow : Gtk.Window, IMainWindow,
										Interfaces.IUIReactions
	{
		protected class Size
		{
			// SRLSY, PPL.
			public int width, height;
		}

		protected Size size;

		protected ToxInterface toxsharp;
		protected InputHandling inputhandling;
		protected Popups popups;

		protected Interfaces.IDataReactions datareactions;

		static public void Prepare()
		{
			Application.Init();
		}

		public GtkMainWindow() : base (Gtk.WindowType.Toplevel)
		{
		}

		public void Init(ToxInterface toxsharp, Interfaces.IDataReactions datareactions, InputHandling inputhandling, Popups popups)
		{
			this.toxsharp = toxsharp;
			this.datareactions = datareactions;
			this.inputhandling = inputhandling;
			this.popups = popups;

			Build();

			ConnectState(false, null);
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

		public void Do()
		{
			Show();
			Application.Run();
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

		public void ConnectState(bool state, string text)
		{
			checkbutton1.Active = state;
			checkbutton1.Label = text;
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

	/*
	 *
	 *
	 *
	 */

		public void PopupMenuDo(Interfaces.PopupEntry[] entries)
		{
		}

	/*
	 *
	 *
	 *
	 */

		public bool AskIDMessage(string explainID, string explainMessage, out string ID, out string message)
		{
			ID = null;
			message = null;

			InputOneLine dlg = new InputOneLine();
			if (dlg.Do(explainID, out ID))
				if (dlg.Do(explainMessage, out message))
					return true;

			return false;
		}

	/*
	 *
	 *
	 *
	 */

		public void TextAdd(Interfaces.SourceType type, UInt16 id, string source, string text)
		{
			long ticks = DateTime.Now.Ticks;
			liststoreall.AppendValues(source, text, (byte)type, id, ticks);

			if ((type == Interfaces.SourceType.Friend) || (type == Interfaces.SourceType.Group))
				foreach(ListStoreSourceTypeID liststore in liststorepartial)
					if ((liststore.type == type) && (liststore.id == id))
						liststore.AppendValues(source, text, (byte)type, id, ticks);
			
			Gtk.Adjustment adjust = nodescroll1.Vadjustment;
			adjust.Value = adjust.Upper - adjust.PageSize;
		}

	/*
	 *
	 *
	 *
	 */

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
			InputKey key = InputKey.None;
			switch(args.Event.Key) {
				case Gdk.Key.Up: key = InputKey.Up; break;
				case Gdk.Key.Down: key = InputKey.Down; break;
				case Gdk.Key.Tab: key = InputKey.Tab; break;
				case Gdk.Key.Return: key = InputKey.Return; break;
				default: return;
			}

			if (inputhandling.Do(entry1.Text, key))
				entry1.Text = "";
		}

	/*****************************************************************************/

		public void Quit()
		{
			toxsharp.ToxStopAndSave();
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
}
