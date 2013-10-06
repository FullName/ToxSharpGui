
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpGui;

public partial class MainWindow /* : Gtk.Window, IToxSharpFriend, IToxSharpGroup */
{
	protected DataStorage _datastorage;
	protected DataStorage datastorage
	{
		get
		{
			if (_datastorage == null)
				_datastorage = new DataStorage();

			return _datastorage;
		}
	}

	protected Gtk.TreeStore _store;
	protected Gtk.TreeStore store
	{
		get
		{
			if (_store == null)
				_store = new Gtk.TreeStore(typeof(TypeIDTreeNode));
			
			return _store;
		}
	}

	protected StoreIterators _storeiterators;
	protected StoreIterators storeiterators
	{
		get
		{
			if (_storeiterators == null)
				_storeiterators = new StoreIterators(store);

			return _storeiterators;
		}
	}

	private void ExtractMark(Gtk.TreeViewColumn column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		HolderTreeNode holder = model.GetValue(iter, 0) as HolderTreeNode;
		if (holder == null)
			return;

		TypeIDTreeNode typeid = holder.typeid;

		string text;
		switch (typeid.entryType)
		{
			case TypeIDTreeNode.EntryType.Header:
				text = "";
				break;
			case TypeIDTreeNode.EntryType.Friend:
				text = (typeid as FriendTreeNode).online ? "*" : "O";
				break;
			case TypeIDTreeNode.EntryType.Stranger:
				text = "+?";
				break;
			default:
				text = "???";
				break;
		}
        
		(cell as Gtk.CellRendererText).Text = text;
	}

	private void ExtractPerson(Gtk.TreeViewColumn column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		HolderTreeNode holder = model.GetValue(iter, 0) as HolderTreeNode;
		if (holder == null)
			return;

		TypeIDTreeNode typeid = holder.typeid;

		string text = null;
		string tip = null;
		string color = "black";
		switch (typeid.entryType)
		{
			case TypeIDTreeNode.EntryType.Header:
				text = (typeid as HeaderTreeNode).title;
				break;

						case TypeIDTreeNode.EntryType.Friend:
				FriendTreeNode friend = typeid as FriendTreeNode;
				if (friend.name.Length > 0)
					text = friend.name;
				else
					text = "{" + friend.key.str.Substring(0, 8) + "...}";

				if (friend.state.Length > 0)
					text += " (" + friend.state + ")";

				color = friend.online ? "green" : "red";
				break;

			case TypeIDTreeNode.EntryType.Stranger:
				StrangerTreeNode stranger = typeid as StrangerTreeNode;
				text = stranger.key.str.Substring(0, 7) + "...";
				tip = "Message: " + stranger.message + "\nID: " + stranger.key.str;
				break;

			default:
				text = "<undefined>";
				break;
		}
        
		(cell as Gtk.CellRendererText).Text = text;
		(cell as Gtk.CellRendererText).Foreground = color;
//		if(tip != null)
//			(cell as Gtk.CellRendererText).Tooltip = tip;
	}

	protected Gtk.ListStore _liststoreall;
	protected Gtk.ListStore liststoreall
	{
		get
		{
			// source, message, source-type, source-id, timestamp
			if (_liststoreall == null)
				_liststoreall = new ListStore(typeof(string), typeof(string), typeof(byte), typeof(UInt16), typeof(Int64));

			return _liststoreall;
		}
	}

	protected List<ListStoreSourceTypeID> _liststorepartial;
	protected List<ListStoreSourceTypeID> liststorepartial
	{
		get
		{
			if (_liststorepartial == null)
				_liststorepartial = new List<ListStoreSourceTypeID>();

			return _liststorepartial;
		}
	}		

/*
 *
 *
 *
 */
	protected void TreesSetup()
	{
		TreeViewColumn markcol = new TreeViewColumn();
		markcol.Title = "Mark";
		Gtk.CellRendererText markrender = new Gtk.CellRendererText();
		markcol.PackStart(markrender, false);
		markcol.SetCellDataFunc(markrender, new Gtk.TreeCellDataFunc(ExtractMark));
		treeview1.AppendColumn(markcol);

		TreeViewColumn personcol = new TreeViewColumn();
		personcol.Title = "Person";
		Gtk.CellRendererText personrender = new Gtk.CellRendererText();
		personcol.PackStart(personrender, false);
		personcol.SetCellDataFunc(personrender, new Gtk.TreeCellDataFunc(ExtractPerson));
		treeview1.AppendColumn(personcol);

		treeview1.Model = store;
		treeview1.ExpandAll();

		treeview1.ButtonReleaseEvent += OnTreeview1ButtonReleaseEvent;
		treeview1.KeyReleaseEvent += OnTreeview1KeyReleaseEvent;

		nodeview1.AppendColumn("Source", new Gtk.CellRendererText(), "text", 0);

		Gtk.CellRendererText renderer1 = new Gtk.CellRendererText();
		// utterly braindamaged: useless without width. WTF.
		renderer1.WrapMode = Pango.WrapMode.WordChar;
		// and width cannot be set sanely, or adjusted sanely. WAICF.
		renderer1.WrapWidth = 400;

		nodeview1.AppendColumn("Text", renderer1, "text", 1);

		nodeview1.Model = liststoreall;
		// on resize of nodeview1, set renderer1.wrapwidth
	}

	public void WidthNew(int Width)
	{
/*
		 * ScrolledWindow scrollwindow = notebook1.CurrentPageWidget as ScrolledWindow;
		NodeView nodeview = scrollwindow.Child as NodeView;

		TreeViewColumn column1 = nodeview.Columns[0] as TreeViewColumn;
		CellRendererText renderer1 = column1.CellRenderers[0] as CellRendererText;

		TreeViewColumn column2 = nodeview.Columns[1] as TreeViewColumn;
		CellRendererText renderer2 = column2.CellRenderers[0] as CellRendererText;
*/
//		renderer2.WrapWidth = Width - hbox1.WidthRequest - 30 - renderer1.Width;
//		renderer2.Width = Width - hbox1.WidthRequest - 25 - renderer1.Width;
	}
/*
 *
 *
 *
 */

	public enum SourceType { Friend, Stranger, Group, System, Debug };

	public void TextAdd(SourceType type, UInt16 id, string source, string text)
	{
		long ticks = DateTime.Now.Ticks;
		liststoreall.AppendValues(source, text, (byte)type, id, ticks);

		if ((type == SourceType.Friend) || (type == SourceType.Group))
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

	protected void DumpAction(object o, string text)
	{
		Gtk.TreeView treeview = (Gtk.TreeView)o;
		Gtk.TreePath path;
		Gtk.TreeViewColumn col;
		treeview.GetCursor(out path, out col);
		
		if (path != null)
			text += " -- " + path.ToString();
		if (col != null)
			text += " :: " + col.ToString();
		TextAdd(0, 256, "DEBUG", text);
	}

	protected void TreeViewPopupNew(object o, System.EventArgs args)
	{
		Gtk.MenuItem item = o as Gtk.MenuItem;
		if (item.Name == "new:friend")
		{
			string friendnew, friendmsg;
			InputOneLine dlg = new InputOneLine();
			if (dlg.Do("For this action, an ID is required.\nIt's a string of 78 characters.\nPlease insert it below:", out friendnew))
			{
				if (friendnew.Length != 2 * ToxSharp.ID_LEN_BINARY)
					return;

				if (dlg.Do("You can add a message to your request:", out friendmsg))
				{
					ToxKey friendkey = new ToxKey(friendnew);
					int friendid = toxsharp.ToxFriendAdd(friendkey, friendmsg);
					if (friendid >= 0)
					{
						TextAdd(SourceType.Debug, 0, "SYSTEM", "Friend request sent to " + friendkey.str + ".");
						toxsharp.ToxFriendInit(friendid);
					}
				}
			}
	
			return;
		}

		TextAdd(SourceType.Debug, 0, "DEBUG", "new something: " + item.Name);
	}

	protected void TreeViewPopupFriend(object o, System.EventArgs args)
	{
		Gtk.MenuItem item = o as Gtk.MenuItem;
		TextAdd(SourceType.Debug, 0, "DEBUG", "friend action: " + item.Name);
		if (item.Name.Substring(0, 7) == "remove:")
		{
			FriendTreeNode friend = null;
			int foundnum = datastorage.FindFriendsWithKeyStartingWithID(item.Name.Substring(7), out friend);
			if (foundnum == 1)
			{
				int code = toxsharp.ToxFriendDel(friend.key);
				if (code == 0)
					StoreDelete(friend);
			}
		}
	}

	protected void TreeViewPopupStranger(object o, System.EventArgs args)
	{
		Gtk.MenuItem item = o as Gtk.MenuItem;
		TextAdd(SourceType.Debug, 0, "DEBUG", "stranger action: " + item.Name);
		if (item.Name.Substring(0, 7) == "accept:")
		{
			string keystr = item.Name.Substring(7);
			TextAdd(SourceType.Debug, 0, "DEBUG", "stranger action: ACCEPT => [" + keystr + "]");
			ToxKey key = new ToxKey(keystr);
			int i = toxsharp.ToxFriendAddNoRequest(key);
			if (i >= 0)
			{
				TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Stranger, key);
				if (typeid != null)
					StoreDelete(typeid);
			}
		}
		else if (item.Name.Substring(0, 8) == "decline:")
		{
			string id = item.Name.Substring(8);
			TextAdd(SourceType.Debug, 0, "DEBUG", "stranger action: DECLINE => [" + id + "]");
		}
	}
	
	protected void StoreDelete(TypeIDTreeNode typeid)
	{
		TreeIter parent;
		if (!storeiterators.GetByTypeRaw(typeid.entryType, out parent))
			return;

		int num = store.IterNChildren(parent);
		for(int i = 0; i < num; i++)
		{
			TreeIter iter;
			if (store.IterNthChild(out iter, parent, i))
			{
				HolderTreeNode holder = store.GetValue(iter, 0) as HolderTreeNode;
				if (holder != null)
				{
					if (holder.typeid == typeid)
					{
						store.Remove(ref iter);
						datastorage.Del(typeid);
						break;
					}
				}
			}
		}

		if (!store.IterHasChild(parent))
		{
			store.Remove(ref parent);
			storeiterators.SetByTypeRaw(typeid.entryType, Gtk.TreeIter.Zero);
		}

		treeview1.QueueDraw();
	}

	protected void OnTreeview1ButtonReleaseEvent (object o, Gtk.ButtonReleaseEventArgs args)
	{
		// path is wrong here
		// DumpAction(o, "ButtonUp: " + args.Event.Button + " @ " + args.Event.X + ", " + args.Event.Y);

		int x = (int)args.Event.X, y = (int)args.Event.Y;
		TreePath path;
		TreeViewColumn col;
		if (treeview1.GetPathAtPos(x, y, out path, out col))
		{
			DumpAction(o, "ButtonUp: (inside) " + args.Event.Button + ":" + args.Event.Type + " @ " + args.Event.X + ", " + args.Event.Y);
			if ((args.Event.Button == 1) &&
			    (args.Event.Type == Gdk.EventType.TwoButtonPress))
			{
				TreeIter iter;
				if (!treeview1.Model.GetIterFromString(out iter, path.ToString()))
					return;

				HolderTreeNode holder = treeview1.Model.GetValue(iter, 0) as HolderTreeNode;
				if (holder == null)
					return;

				TypeIDTreeNode typeid = holder.typeid;
				NotebookAddPage(typeid);
			}

			if (args.Event.Button == 3)
			{
				TreeIter iter;
				if (!treeview1.Model.GetIterFromString(out iter, path.ToString()))
					return;

				HolderTreeNode holder = treeview1.Model.GetValue(iter, 0) as HolderTreeNode;
				if (holder == null)
					return;

				TypeIDTreeNode typeid = holder.typeid;

				if (typeid.entryType == TypeIDTreeNode.EntryType.Header)
					return;

				// friend:
				// - delete, invite to group
				// stranger:
				// - accept, decline
				// group:
				// - delete

				if (typeid.entryType == TypeIDTreeNode.EntryType.Friend)
				{
					FriendTreeNode friend = typeid as FriendTreeNode;
					if (friend == null)
						return;
					
					Gtk.Menu menu = new Gtk.Menu();

					Gtk.MenuItem itemfriend = new Gtk.MenuItem("Invite to group");
					itemfriend.Name = "invite:" + friend.key.str;
					itemfriend.Sensitive = false;
					itemfriend.Activated += TreeViewPopupFriend;

					Dictionary<UInt16, TypeIDTreeNode> groups;
					if (datastorage.GroupEnumerator(out groups) && (groups.Count > 0))
					{
						bool gotone = false;
	
						Gtk.Menu menugroups = new Gtk.Menu();

						Gtk.MenuItem itemgroup;
						foreach(KeyValuePair<UInt16, TypeIDTreeNode> pair in groups)
						{
							GroupTreeNode group = pair.Value as GroupTreeNode;
							if (group != null)
							{
								gotone = true;

								itemgroup = new Gtk.MenuItem("[" + group.id + "] " + group.name + " (" + group.key.str.Substring(0, 8) + "...)");
								itemgroup.Name = "invite:" + friend.key.str + ":" + group.key;
								itemgroup.Activated += TreeViewPopupFriend;
								itemgroup.Show();
							}
						}
						
						if (gotone)
						{
							itemfriend.Sensitive = gotone;
							itemfriend.Submenu = menugroups;
						}
					}

					itemfriend.Show();
					menu.Append(itemfriend);
	
					Gtk.MenuItem itemremove = new Gtk.MenuItem("Remove from list");
					itemremove.Name = "remove:" + friend.key.str;
					itemremove.Activated += TreeViewPopupFriend;
					itemremove.Show();
					menu.Append(itemremove);
		
					menu.Popup();
				}

				if (typeid.entryType == TypeIDTreeNode.EntryType.Group)
					return;

				if (typeid.entryType == TypeIDTreeNode.EntryType.Stranger)
				{
					StrangerTreeNode stranger = typeid as StrangerTreeNode;
					if (stranger == null)
						return;

					Gtk.Menu menu = new Gtk.Menu();
					Gtk.MenuItem itemfriend = new Gtk.MenuItem("Accept as friend");
					itemfriend.Name = "accept:" + stranger.key.str;
					itemfriend.Activated += TreeViewPopupStranger;
					itemfriend.Show();
					menu.Append(itemfriend);
	
					Gtk.MenuItem itemgroup = new Gtk.MenuItem("Decline as friend");
					itemgroup.Name = "decline:" + stranger.key.str;
					itemgroup.Activated += TreeViewPopupStranger;
					itemgroup.Show();
					menu.Append(itemgroup);
		
					menu.Popup();
				}
			}
		}
		else
		{		
			// only popup here
			if (args.Event.Button != 3)
				return;

			Gtk.Menu menu = new Gtk.Menu();
			Gtk.MenuItem itemfriend = new Gtk.MenuItem("new friend");
			itemfriend.Activated += TreeViewPopupNew;
			itemfriend.Name = "new:friend";
			itemfriend.Show();
			menu.Append(itemfriend);

			Gtk.MenuItem itemgroup = new Gtk.MenuItem("new group");
			itemgroup.Activated += TreeViewPopupNew;
			itemgroup.Name  = "new:group";
			itemgroup.Show();
			menu.Append(itemgroup);

			menu.Popup();
		}
	}

	protected void OnTreeview1KeyReleaseEvent (object o, Gtk.KeyReleaseEventArgs args)
	{
		// path is right here
		// DumpAction(o, "KeyUp: " + args.Event.Key + ":" + args.Event.State);
		if (args.Event.Key == Gdk.Key.Return)
		{
			TreeModel model;
			TreeIter iter;
			if (treeview1.Selection.GetSelected(out model, out iter))
			{
				HolderTreeNode holder = model.GetValue(iter, 0) as HolderTreeNode;
				if (holder != null)
				    NotebookAddPage(holder.typeid);
			}
		}
	}

	protected void NotebookAddPage(TypeIDTreeNode typeid)
	{
		if (typeid == null)
			return;
		
		if ((typeid.entryType != TypeIDTreeNode.EntryType.Friend) &&
		    (typeid.entryType != TypeIDTreeNode.EntryType.Group))
			return;

		SourceType type = typeid.entryType == TypeIDTreeNode.EntryType.Friend ? SourceType.Friend : SourceType.Group;
		foreach(ListStoreSourceTypeID liststore in liststorepartial)
			if ((liststore.type == type) &&
			    (liststore.id == typeid.id))
				return;

		Gtk.NodeView nodeview = new Gtk.NodeView();
		nodeview.AppendColumn("Source", new Gtk.CellRendererText(), "text", 0);

		Gtk.CellRendererText renderer = new Gtk.CellRendererText();
		renderer.WrapMode = Pango.WrapMode.WordChar;
		nodeview.AppendColumn("Text", renderer, "text", 1);
		
		// source, message, source-type, source-id, timestamp
		ListStoreSourceTypeID liststorenew = new ListStoreSourceTypeID(type, typeid.id, typeof(string), typeof(string), typeof(byte), typeof(UInt16), typeof(Int64));
		nodeview.Model = liststorenew;

		liststorepartial.Add(liststorenew);

		ScrolledWindow scrolledwindow = new ScrolledWindow();
		scrolledwindow.Add(nodeview);

		string label = "???";
		if (typeid.entryType == TypeIDTreeNode.EntryType.Friend)
			label = (typeid as FriendTreeNode).name;
		else if (typeid.entryType == TypeIDTreeNode.EntryType.Group)
			label = "#" + (typeid as GroupTreeNode).name;

		notebook1.AppendPage(scrolledwindow, new Gtk.Label(label));
		notebook1.ShowAll(); // required to make nodeview, label and notebook display
		notebook1.CurrentPage = notebook1.NPages - 1; // requires ShowAll before

		Focus = entry1;
	}
}