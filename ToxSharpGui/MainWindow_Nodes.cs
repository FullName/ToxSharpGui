
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpGui;

public partial class MainWindow /* : Gtk.Window, IToxSharpFriend, IToxSharpGroup */
{
    [Gtk.TreeNode(ListOnly=true)]
	protected class PersonTreeNode : Gtk.TreeNode
	{
		public enum EntryType { Header, Friend, Stranger, Group };

		public EntryType entryType;

		public string title;

		public PersonTreeNode(string title)
		{
			this.entryType = EntryType.Header;
			this.title = title;
		}

		public UInt16 id;
		public string name;
		public string state;
		public bool online;

		public PersonTreeNode(int id, string name, string state, bool online)
		{
			this.entryType = EntryType.Friend;
			this.id = (UInt16)id;
			this.name = name;
			this.state = state;
			this.online = online;
		}
		
		public string key;
		public string message;
		
		public PersonTreeNode(string key, string message)
		{
			this.entryType = EntryType.Stranger;
			this.key = key;
			this.message = message;
		}
	};

	protected Gtk.TreeStore _store;
	protected Gtk.TreeIter _frienditer;
	protected Gtk.TreeIter _strangeriter;
	protected Gtk.TreeIter _groupiter;

	protected Gtk.TreeStore store
	{
		get
		{
			if (_store == null)
				_store = new Gtk.TreeStore(typeof(PersonTreeNode));
			
			return _store;
		}
	}
	protected Gtk.TreeIter frienditer
	{
		get
		{
			if (_frienditer.Equals(Gtk.TreeIter.Zero))
			{
				_frienditer = _store.AppendValues(new PersonTreeNode("Friends"));
				if (!_strangeriter.Equals(Gtk.TreeIter.Zero))
					_store.MoveBefore(_frienditer, _strangeriter);
				else if (!_groupiter.Equals(Gtk.TreeIter.Zero))
					_store.MoveBefore(_frienditer, _groupiter);
			}

			return _frienditer;
		}
	}
	protected Gtk.TreeIter strangeriter
	{
		get
		{
			if (_strangeriter.Equals(Gtk.TreeIter.Zero))
			{
				_strangeriter = _store.AppendValues(new PersonTreeNode("Strangers"));
				if (!_groupiter.Equals(Gtk.TreeIter.Zero))
					_store.MoveBefore(_strangeriter, _groupiter);
			}

			return _strangeriter;
		}
	}
	protected Gtk.TreeIter groupiter
	{
		get
		{
			if (_groupiter.Equals(Gtk.TreeIter.Zero))
				_groupiter = _store.AppendValues(new PersonTreeNode("Group"));

			return _groupiter;
		}
	}

	private void ExtractMark(Gtk.TreeViewColumn column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
	{
		PersonTreeNode person = (PersonTreeNode)model.GetValue(iter, 0);
		string text;
		switch (person.entryType)
		{
			case PersonTreeNode.EntryType.Header:
				text = "";
				break;
			case PersonTreeNode.EntryType.Friend:
				text = person.online ? "*" : "O";
				break;
			case PersonTreeNode.EntryType.Stranger:
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
		PersonTreeNode person = (PersonTreeNode)model.GetValue (iter, 0);
		string text = null;
		string tip = null;
		string color = "black";
		switch (person.entryType)
		{
			case PersonTreeNode.EntryType.Header:
				text = person.title;
				break;
			case PersonTreeNode.EntryType.Friend:
				text = person.name;
				if (person.state.Length > 0)
					text += " (" + person.state + ")";
				color = person.online ? "green" : "red";
				break;
			case PersonTreeNode.EntryType.Stranger:
				text = person.key.Substring(0, 7) + "...";
				tip = person.message + " (" + person.key + ")";
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
/*
		treeview1.AppendColumn("Mark", new Gtk.CellRendererText(), "text", 0);
		treeview1.AppendColumn("Person", new Gtk.CellRendererText(), "text", 1, "foreground", 2);
*/

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

		/* DEBUG >> a few fake entries */
		/*
		PersonTreeNode person = new PersonTreeNode(42, "Checker", "Busy", true);
		store.AppendValues(frienditer, person);
		person = new PersonTreeNode(69, "Mater", "", false);
		store.AppendValues(frienditer, person);

		treeview1.ExpandAll();
		*/
		/* << DEBUG */
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
		
//		Gtk.TreeIter iter;
//		Gtk.ITreeNode node = 
//		treeview.Selection.GetSelected(out iter);

		if (path != null)
			text += " -- " + path.ToString();
		if (col != null)
			text += " :: " + col.ToString();
		TextAdd(0, 256, "DEBUG", text);
	}

	protected void TreeViewPopupNew(object o, System.EventArgs args)
	{
		Gtk.MenuItem item = o as Gtk.MenuItem;
		TextAdd(SourceType.Debug, 0, "DEBUG", "new something: " + item.Name);
	}

	protected void TreeViewPopupStranger(object o, System.EventArgs args)
	{
		Gtk.MenuItem item = o as Gtk.MenuItem;
		TextAdd(SourceType.Debug, 0, "DEBUG", "stranger action: " + item.Name);
		if (item.Name.Substring(0, 7) == "accept:")
		{
			string key = item.Name.Substring(7);
			TextAdd(SourceType.Debug, 0, "DEBUG", "stranger action: ACCEPT => [" + key + "]");
			int i = toxsharp.ToxFriendAddNoRequest(key);
			if (i >= 0)
			{
				TreeIter iter;
				int[] strangerpos = new int[2] { 1, 0 };
				if (_frienditer.Equals(Gtk.TreeIter.Zero))
					strangerpos[0] = 0;

				TreePath path = new TreePath(strangerpos);
				if (store.GetIter(out iter, path))
				{
					while(!iter.Equals(TreeIter.Zero))
					{
						PersonTreeNode person = store.GetValue(iter, 0) as PersonTreeNode;
						if (person == null)
							break;
						if (person.entryType != PersonTreeNode.EntryType.Stranger)
							break;

						if (person.key == key)
						{
							TreeIter parent;
							store.IterParent(out parent, iter);
							store.Remove(ref iter);
							if (!store.IterHasChild(parent))
							{
								store.Remove(ref parent);
								_strangeriter = TreeIter.Zero;
							}

							break;
						}

						store.IterNext(ref iter);
					}
				}
			}
		}
		else if (item.Name.Substring(0, 8) == "decline:")
		{
			string id = item.Name.Substring(8);
			TextAdd(SourceType.Debug, 0, "DEBUG", "stranger action: DECLINE => [" + id + "]");
		}
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

				PersonTreeNode person = treeview1.Model.GetValue(iter, 0) as PersonTreeNode;
				NotebookAddPage(person);
			}

			if (args.Event.Button == 3)
			{
				TreeIter iter;
				if (!treeview1.Model.GetIterFromString(out iter, path.ToString()))
					return;

				PersonTreeNode person = treeview1.Model.GetValue(iter, 0) as PersonTreeNode;
				if (person == null)
					return;
				if (person.entryType == PersonTreeNode.EntryType.Header)
					return;

				// friend:
				// - delete, invite to group
				// stranger:
				// - accept, decline
				// group:
				// - delete

				if (person.entryType == PersonTreeNode.EntryType.Friend)
					return;
				if (person.entryType == PersonTreeNode.EntryType.Group)
					return;

				if (person.entryType == PersonTreeNode.EntryType.Stranger)
				{
					Gtk.Menu menu = new Gtk.Menu();
					Gtk.MenuItem itemfriend = new Gtk.MenuItem("Accept as friend");
					itemfriend.Name = "accept:" + person.key;
					itemfriend.Activated += TreeViewPopupStranger;
					itemfriend.Show();
					menu.Append(itemfriend);
	
					Gtk.MenuItem itemgroup = new Gtk.MenuItem("Decline as friend");
					itemgroup.Name = "decline:" + person.key;
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
				PersonTreeNode person = model.GetValue(iter, 0) as PersonTreeNode;
			    NotebookAddPage(person);
			}
		}
	}

	protected void NotebookAddPage(PersonTreeNode person)
	{
		if (person == null)
			return;
		
		if ((person.entryType != PersonTreeNode.EntryType.Friend) &&
		    (person.entryType != PersonTreeNode.EntryType.Group))
			return;

		SourceType type = person.entryType == PersonTreeNode.EntryType.Friend ? SourceType.Friend : SourceType.Group;
		foreach(ListStoreEx liststore in liststorepartial)
			if ((liststore.type == type) &&
			    (liststore.id == person.id))
				return;

		Gtk.NodeView nodeview = new Gtk.NodeView();
		nodeview.AppendColumn("Source", new Gtk.CellRendererText(), "text", 0);

		Gtk.CellRendererText renderer = new Gtk.CellRendererText();
		renderer.WrapMode = Pango.WrapMode.Word;
		nodeview.AppendColumn("Text", renderer, "text", 1);
		
		// source, message, source-type, source-id, timestamp
		ListStoreEx liststorenew = new ListStoreEx(type, person.id, typeof(string), typeof(string), typeof(byte), typeof(UInt16), typeof(Int64));
		nodeview.Model = liststorenew;

		liststorepartial.Add(liststorenew);

		ScrolledWindow scrolledwindow = new ScrolledWindow();
		scrolledwindow.Add(nodeview);

		notebook1.AppendPage(scrolledwindow, new Gtk.Label((person.entryType == PersonTreeNode.EntryType.Friend) ? person.name : ("#" + person.name)));
		notebook1.ShowAll(); // required to make nodeview, label and notebook display
		notebook1.CurrentPage = notebook1.NPages - 1; // requires ShowAll before
	}
}