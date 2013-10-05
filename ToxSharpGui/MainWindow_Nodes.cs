
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpGui;

public partial class MainWindow /* : Gtk.Window, IToxSharpFriend, IToxSharpGroup */
{
	protected class DataStorageSub
	{
	}

	protected class DataStorageSubKeyKey : DataStorageSub
	{
		public Dictionary<ToxSharpGui.Key, TypeIDTreeNode> element;

		public DataStorageSubKeyKey()
		{
			element = new Dictionary<ToxSharpGui.Key, TypeIDTreeNode>();
		}
	}

	protected class DataStorageSubKeyUInt16 : DataStorageSub
	{
		public Dictionary<UInt16, TypeIDTreeNode> element;

		public DataStorageSubKeyUInt16()
		{
			element = new Dictionary<ushort, TypeIDTreeNode>();
		}
	}

	protected class DataStorage
	{
		// Dictionary: Type
		//    Dictionary ID | List
		protected Dictionary<TypeIDTreeNode.EntryType, DataStorageSub> data;

		public DataStorage()
		{
			data = new Dictionary<TypeIDTreeNode.EntryType, DataStorageSub>();

			data.Add(TypeIDTreeNode.EntryType.Friend, new DataStorageSubKeyUInt16());
			data.Add(TypeIDTreeNode.EntryType.Stranger, new DataStorageSubKeyKey());
			data.Add(TypeIDTreeNode.EntryType.Group, new DataStorageSubKeyUInt16());
		}

		public void Add(TypeIDTreeNode typeid)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(typeid.entryType, out sub))
				return;

			if ((typeid.entryType == TypeIDTreeNode.EntryType.Friend) ||
				(typeid.entryType == TypeIDTreeNode.EntryType.Group))
			{
				DataStorageSubKeyUInt16 subint = sub as DataStorageSubKeyUInt16;
				if (subint != null)
					subint.element.Add(typeid.id, typeid);
			}
			else if (typeid.entryType == TypeIDTreeNode.EntryType.Stranger)
			{
				DataStorageSubKeyKey subkey = sub as DataStorageSubKeyKey;
				StrangerTreeNode stranger = typeid as StrangerTreeNode;
				if ((subkey != null) && (stranger != null))
					subkey.element.Add(stranger.key, typeid);
			}
		}

		public void Del(TypeIDTreeNode typeid)
		{
			// TODO
		}

		public TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, UInt16 id)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(entrytype, out sub))
				return null;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return null;
			
			TypeIDTreeNode typeid = null;
			subactual.element.TryGetValue(id, out typeid);
			return typeid;
		}

		public TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, ToxSharpGui.Key key)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(entrytype, out sub))
				return null;

			DataStorageSubKeyKey subactual = sub as DataStorageSubKeyKey;
			if (subactual == null)
				return null;
			
			TypeIDTreeNode typeid = null;
			subactual.element.TryGetValue(key, out typeid);
			return typeid;
		}

		public int FindFriendsWithKeyStartingWithID(string keyfragment, out FriendTreeNode friend)
		{
			friend = null;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Friend, out sub))
				return -1;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return -1;

			int rc = 0;
			foreach(TypeIDTreeNode typeid in subactual.element.Values)
			{
				FriendTreeNode check = typeid as FriendTreeNode;
				if (check == null)
					continue;

				if (0 == string.Compare(check.key.str.Substring(0, keyfragment.Length), keyfragment, true))
				{
					rc++;
					friend = check;
				}
			}

			return rc;
		}

		public void FriendCount(out int online, out int total)
		{
			online = 0;
			total = 0;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Friend, out sub))
				return;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return;

			foreach(TypeIDTreeNode typeid in subactual.element.Values)
			{
				FriendTreeNode friend = typeid as FriendTreeNode;
				if (friend != null)
				{
					total++;
					if (friend.online)
						online++;
				}
			}
		}

		public bool GroupEnumerator(out Dictionary<UInt16, TypeIDTreeNode> groups)
		{
			groups = null;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Group, out sub))
				return false;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return false;

			groups = subactual.element;
			return true;
		}
	}

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

	protected class HolderTreeNode : Gtk.TreeNode
	{
		public TypeIDTreeNode typeid;

		protected HolderTreeNode(TypeIDTreeNode typeid)
		{
			this.typeid = typeid;
		}

		public static HolderTreeNode Create(TypeIDTreeNode typeid)
		{
			return new HolderTreeNode(typeid);
		}
	}

	protected HolderTreeNode HolderTreeNodeNew(TypeIDTreeNode typeid)
	{
		datastorage.Add(typeid);
		return HolderTreeNode.Create(typeid);
	}

	protected static HolderTreeNode HolderTreeNodeHeaderNew(string text)
	{
		TypeIDTreeNode typeid = new HeaderTreeNode(text);
		return HolderTreeNode.Create(typeid);
	}

	protected class TypeIDTreeNode
	{
		public enum EntryType { Header, Friend, Stranger, Group };

		public EntryType entryType;
		public UInt16 id;

		public TypeIDTreeNode(EntryType entryType, UInt16 id)
		{
			this.entryType = entryType;
			this.id = id;
		}

	}

	protected class HeaderTreeNode : TypeIDTreeNode
	{
		public string title;
		
		public HeaderTreeNode(string title) : base(EntryType.Header, 0)
		{
			this.title = title;
		}
	}

	protected class KeyTreeNode : TypeIDTreeNode
	{
		public ToxSharpGui.Key key;

		public KeyTreeNode(EntryType entrytype, UInt16 id, ToxSharpGui.Key key) : base(entrytype, id)
		{
			this.key = key;
		}
	}

	protected class FriendTreeNode : KeyTreeNode
	{
		public string name;
		public string state;
		public bool online;

		public FriendTreeNode(UInt16 id, ToxSharpGui.Key key, string name, string state, bool online) : base(EntryType.Friend, id, key)
		{
			this.name = name;
			this.state = state;
			this.online = online;
		}
	}

	protected class StrangerTreeNode : KeyTreeNode
	{
		public string message;
		
		public StrangerTreeNode(ToxSharpGui.Key key, string message) : base(EntryType.Stranger, 0, key)
		{
			this.message = message;
		}
	}

	protected class GroupTreeNode : KeyTreeNode
	{
		public string name;

		public GroupTreeNode(UInt16 id, ToxSharpGui.Key key, string name) : base(EntryType.Group, id, key)
		{
			this.name = name;
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

	protected class StoreIterators
	{
		protected Gtk.TreeStore store;

		public StoreIterators(Gtk.TreeStore store)
		{
			this.store = store;
		}

		public bool GetByTypeRaw(TypeIDTreeNode.EntryType type, out Gtk.TreeIter iter)
		{
			switch(type)
			{
				case TypeIDTreeNode.EntryType.Friend:
					iter = _frienditer;
					break;
					
				case TypeIDTreeNode.EntryType.Stranger:
					iter = _strangeriter;
					break;

				case TypeIDTreeNode.EntryType.Group:
					iter = _groupiter;
					break;
			}

			return !iter.Equals(Gtk.TreeIter.Zero);
		}

		public void SetByTypeRaw(TypeIDTreeNode.EntryType type, Gtk.TreeIter iter)
		{
			if (type == TypeIDTreeNode.EntryType.Friend)
				_frienditer = iter;
			if (type == TypeIDTreeNode.EntryType.Stranger)
				_strangeriter = iter;
			if (type == TypeIDTreeNode.EntryType.Group)
				_groupiter = iter;
		}

		protected Gtk.TreeIter _frienditer;
		protected Gtk.TreeIter _strangeriter;
		protected Gtk.TreeIter _groupiter;

		public Gtk.TreeIter frienditer
		{
			get
			{
				if (_frienditer.Equals(Gtk.TreeIter.Zero))
				{
					_frienditer = store.AppendValues(HolderTreeNodeHeaderNew("Friends"));
					if (!_strangeriter.Equals(Gtk.TreeIter.Zero))
						store.MoveBefore(_frienditer, _strangeriter);
					else if (!_groupiter.Equals(Gtk.TreeIter.Zero))
						store.MoveBefore(_frienditer, _groupiter);
				}
	
				return _frienditer;
			}
		}

		public Gtk.TreeIter strangeriter
		{
			get
			{
				if (_strangeriter.Equals(Gtk.TreeIter.Zero))
				{
					_strangeriter = store.AppendValues(HolderTreeNodeHeaderNew("Strangers"));
					if (!_groupiter.Equals(Gtk.TreeIter.Zero))
						store.MoveBefore(_strangeriter, _groupiter);
				}
	
				return _strangeriter;
			}
		}
	
		public Gtk.TreeIter groupiter
		{
			get
			{
				if (_groupiter.Equals(Gtk.TreeIter.Zero))
					_groupiter = store.AppendValues(HolderTreeNodeHeaderNew("Group"));
	
				return _groupiter;
			}
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

	protected Gtk.ListStore liststoreall;

	protected class ListStoreEx : Gtk.ListStore
	{
		public SourceType type;
		public UInt16 id;
		public ListStoreEx(SourceType type, UInt16 id, params Type[] args) : base(args)
		{
			this.type = type;
			this.id = id;
		}
	}

	protected List<ListStoreEx> liststorepartial;

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
		renderer1.WrapMode = Pango.WrapMode.Word;
		nodeview1.AppendColumn("Text", renderer1, "text", 1);

		// source, message, source-type, source-id, timestamp
		liststoreall = new ListStore(typeof(string), typeof(string), typeof(byte), typeof(UInt16), typeof(Int64));
		nodeview1.Model = liststoreall;

		liststorepartial = new List<ListStoreEx>();
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
		foreach(ListStoreEx liststore in liststorepartial)
			if (liststore.type == type)
				// Friend and Group have IDs
				if (((type != SourceType.Friend) && (type != SourceType.Group)) || (liststore.id == id))
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
					ToxSharpGui.Key friendkey = new ToxSharpGui.Key(friendnew);
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
			ToxSharpGui.Key key = new ToxSharpGui.Key(keystr);
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
		foreach(ListStoreEx liststore in liststorepartial)
			if ((liststore.type == type) &&
			    (liststore.id == typeid.id))
				return;

		Gtk.NodeView nodeview = new Gtk.NodeView();
		nodeview.AppendColumn("Source", new Gtk.CellRendererText(), "text", 0);

		Gtk.CellRendererText renderer = new Gtk.CellRendererText();
		renderer.WrapMode = Pango.WrapMode.Word;
		nodeview.AppendColumn("Text", renderer, "text", 1);
		
		// source, message, source-type, source-id, timestamp
		ListStoreEx liststorenew = new ListStoreEx(type, typeid.id, typeof(string), typeof(string), typeof(byte), typeof(UInt16), typeof(Int64));
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