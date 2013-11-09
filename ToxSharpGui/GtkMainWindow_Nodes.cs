
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpBasic;

namespace ToxSharpGTK
{
	public class StoreIterators
	{
		protected TreeStore store;

		public StoreIterators(TreeStore store)
		{
			this.store = store;
		}

		public bool GetByTypeCreate(TypeIDTreeNode.EntryType type, out TreeIter iter)
		{
			switch(type)
			{
				case TypeIDTreeNode.EntryType.Friend:
					iter = frienditer;
					break;

				case TypeIDTreeNode.EntryType.Stranger:
					iter = strangeriter;
					break;

				case TypeIDTreeNode.EntryType.Group:
					iter = groupiter;
					break;

				case TypeIDTreeNode.EntryType.Invitation:
					iter = invitationiter;
					break;
			}

			return !iter.Equals(TreeIter.Zero);
		}

		public bool GetByTypeRaw(TypeIDTreeNode.EntryType type, out TreeIter iter)
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

				case TypeIDTreeNode.EntryType.Invitation:
					iter = _invitationiter;
					break;
			}

			return !iter.Equals(TreeIter.Zero);
		}

		public void SetByTypeRaw(TypeIDTreeNode.EntryType type, TreeIter iter)
		{
			if (type == TypeIDTreeNode.EntryType.Friend)
				_frienditer = iter;
			if (type == TypeIDTreeNode.EntryType.Stranger)
				_strangeriter = iter;
			if (type == TypeIDTreeNode.EntryType.Group)
				_groupiter = iter;
			if (type == TypeIDTreeNode.EntryType.Invitation)
				_invitationiter = iter;
		}

		protected TreeIter _frienditer;
		protected TreeIter _strangeriter;
		protected TreeIter _groupiter;
		protected TreeIter _invitationiter;

		public TreeIter frienditer
		{
			get
			{
				if (_frienditer.Equals(TreeIter.Zero))
				{
					_frienditer = store.AppendValues(HolderTreeNode.HeaderNew(TypeIDTreeNode.EntryType.Friend));
					if (!_strangeriter.Equals(TreeIter.Zero))
						store.MoveBefore(_frienditer, _strangeriter);
					else if (!_groupiter.Equals(TreeIter.Zero))
						store.MoveBefore(_frienditer, _groupiter);
					else if (!_invitationiter.Equals(TreeIter.Zero))
						store.MoveBefore(_frienditer, _invitationiter);
				}
	
				return _frienditer;
			}
		}

		public TreeIter strangeriter
		{
			get
			{
				if (_strangeriter.Equals(TreeIter.Zero))
				{
					_strangeriter = store.AppendValues(HolderTreeNode.HeaderNew(TypeIDTreeNode.EntryType.Stranger));
					if (!_groupiter.Equals(TreeIter.Zero))
						store.MoveBefore(_strangeriter, _groupiter);
					else if (!_invitationiter.Equals(TreeIter.Zero))
						store.MoveBefore(_strangeriter, _invitationiter);
				}
	
				return _strangeriter;
			}
		}
	
		public TreeIter groupiter
		{
			get
			{
				if (_groupiter.Equals(TreeIter.Zero))
					_groupiter = store.AppendValues(HolderTreeNode.HeaderNew(TypeIDTreeNode.EntryType.Group));
				if (!_invitationiter.Equals(TreeIter.Zero))
					store.MoveBefore(_groupiter, _invitationiter);

				return _groupiter;
			}
		}

		public TreeIter invitationiter
		{
			get
			{
				if (_invitationiter.Equals(TreeIter.Zero))
					_invitationiter = store.AppendValues(HolderTreeNode.HeaderNew(TypeIDTreeNode.EntryType.Invitation));

				return _invitationiter;
			}
		}

		public void Delete(TypeIDTreeNode typeid)
		{
			TreeIter parent;
			if (!GetByTypeRaw(typeid.entryType, out parent))
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
							Delete(typeid);
							break;
						}
					}
				}
			}

			if (!store.IterHasChild(parent))
			{
				store.Remove(ref parent);
				SetByTypeRaw(typeid.entryType, TreeIter.Zero);
			}
		}
	}

	public class ListStoreSourceTypeID : Gtk.ListStore
	{
		public Interfaces.SourceType type;
		public UInt16 id;
		public ListStoreSourceTypeID(Interfaces.SourceType type, UInt16 id, params Type[] args) : base(args)
		{
			this.type = type;
			this.id = id;
		}
	}

	public partial class GtkMainWindow /* : Gtk.Window, IToxSharpFriend, IToxSharpGroup */
	{
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

		private void ExtractMark(Gtk.TreeViewColumn column, Gtk.CellRenderer cell, Gtk.TreeModel model, TreeIter iter)
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
				case TypeIDTreeNode.EntryType.Group:
					text = "#";
					break;
				case TypeIDTreeNode.EntryType.Invitation:
					text = "+#?";
					break;
				default:
					text = "??";
					break;
			}
	        
			(cell as Gtk.CellRendererText).Text = text;
		}

		private void ExtractPerson(Gtk.TreeViewColumn column, Gtk.CellRenderer cell, Gtk.TreeModel model, TreeIter iter)
		{
			HolderTreeNode holder = model.GetValue(iter, 0) as HolderTreeNode;
			if (holder == null)
				return;

			TypeIDTreeNode typeid = holder.typeid;
		
			// string tip = null;

			(cell as Gtk.CellRendererText).Text = typeid.Text();
			ushort check = typeid.Check();
			switch(check)
			{
				case 1:
					(cell as Gtk.CellRendererText).Foreground = "red";
					break;
				case 2:
					(cell as Gtk.CellRendererText).Foreground = "green";
					break;
				default:
					(cell as Gtk.CellRendererText).Foreground = "black";
					break;
			}
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
			treeview1.PopupMenu += OnTreeview1PopupMenu;

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

		public void TreeAdd(TypeIDTreeNode typeid)
		{
			if (typeid == null)
				return;

			HolderTreeNode holder = new HolderTreeNode(typeid);

			TreeIter iter;
			if (storeiterators.GetByTypeCreate(holder.typeid.entryType, out iter))
			{
				store.AppendValues(iter, holder);
				treeview1.ExpandAll();
				treeview1.QueueDraw();
			}
		}

		public void TreeDel(TypeIDTreeNode typeid)
		{
			storeiterators.Delete(typeid);
			treeview1.QueueDraw();
		}

		public void TreeUpdate(TypeIDTreeNode typeid)
		{
			// potentially update Holder if Mark/Text/Tip moves there
			treeview1.QueueDraw();
		}

		public void TreeUpdate()
		{
			treeview1.QueueDraw();
		}

	/*
	 *
	 *
	 *
	 */

		protected void NotebookAddPage(TypeIDTreeNode typeid)
		{
			if (typeid == null)
				return;
			
			if ((typeid.entryType != TypeIDTreeNode.EntryType.Friend) &&
			    (typeid.entryType != TypeIDTreeNode.EntryType.Group))
				return;

			Interfaces.SourceType type = typeid.entryType == TypeIDTreeNode.EntryType.Friend ? Interfaces.SourceType.Friend : Interfaces.SourceType.Group;
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

		protected void OnTreeview1ButtonReleaseEvent(object o, Gtk.ButtonReleaseEventArgs args)
		{
			// path is wrong here
			// DumpAction(o, "ButtonUp: " + args.Event.Button + " @ " + args.Event.X + ", " + args.Event.Y);
			Popups.Button button = Popups.Button.None;
			switch(args.Event.Button)
			{
				case 1: button = Popups.Button.Left;
						break;
				case 2: button = Popups.Button.Middle;
						break;
				case 3: button = Popups.Button.Right;
						break;
			}

			Popups.Click click = Popups.Click.None;
			switch (args.Event.Type)
			{
				case Gdk.EventType.ButtonRelease:
					click = Popups.Click.Single;
					break;
				case Gdk.EventType.TwoButtonPress:
					click = Popups.Click.Double;
					break;
			}

			int x = (int)args.Event.X, y = (int)args.Event.Y;
			TreePath path;
			TreeViewColumn col;
			if (treeview1.GetPathAtPos(x, y, out path, out col))
			{
				DumpAction(o, "ButtonUp: (inside) " + args.Event.Button + ":" + args.Event.Type + " @ " + args.Event.X + ", " + args.Event.Y);

				TreeIter iter;
				if (!treeview1.Model.GetIterFromString(out iter, path.ToString()))
					return;

				HolderTreeNode holder = treeview1.Model.GetValue(iter, 0) as HolderTreeNode;
				if (holder == null)
					return;

				TypeIDTreeNode typeid = holder.typeid;

				// DblClk broken in various versions of GTK - fallback to middle click
				if (((args.Event.Button == 1) &&
				    (args.Event.Type == Gdk.EventType.TwoButtonPress)) ||
				    (args.Event.Button == 2))
				{
					NotebookAddPage(typeid);
					return;
				}

				popups.TreePopup(typeid, button, click);
			}
			else
				popups.TreePopup(null, button, click);
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

		protected void OnTreeview1PopupMenu(object o, Gtk.PopupMenuArgs args)
		{
			// pretty much useless? would be nice, align-wise
		}
	}
}