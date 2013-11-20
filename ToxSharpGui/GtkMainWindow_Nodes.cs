
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
			iter = GetByType(type, true);
			return !iter.Equals(TreeIter.Zero);
		}

		public bool GetByTypeRaw(TypeIDTreeNode.EntryType type, out TreeIter iter)
		{
			iter = GetByType(type, false);
			return !iter.Equals(TreeIter.Zero);
		}

		public void SetByTypeRaw(TypeIDTreeNode.EntryType type, TreeIter iter)
		{
			if (_iter == null)
				return;

			uint at = (uint)(type - 1);
			if (at >= 5)
				return;

			_iter[at] = iter;
		}

		protected TreeIter[] _iter;

		protected TreeIter GetByType(TypeIDTreeNode.EntryType type, bool create)
		{
			if (type == TypeIDTreeNode.EntryType.Header)
				return TreeIter.Zero;

			if (_iter == null)
				_iter = new TreeIter[5];

			uint at = (uint)(type - 1);
			if (at >= 5)
				return TreeIter.Zero;

			if (create && _iter[at].Equals(TreeIter.Zero))
			{
				_iter[at] = store.AppendValues(HolderTreeNode.HeaderNew(type));
				for(uint next = at + 1; next < 5; next++)
					if (!_iter[next].Equals(TreeIter.Zero))
					{
					    store.MoveBefore(_iter[at], _iter[next]);
						break;
					}
			}

			return _iter[at];
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
				case TypeIDTreeNode.EntryType.Rendezvous:
					text = "^?";
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

			Gtk.CellRendererText renderer1 = new Gtk.CellRendererText();

			nodeview1.AppendColumn("Source", renderer1, "text", 0);

			Gtk.CellRendererText renderer2 = new Gtk.CellRendererText();
			renderer2.WrapMode = Pango.WrapMode.WordChar;
			renderer2.WrapWidth = 128;

			nodeview1.AppendColumn("Text", renderer2, "text", 1);

			nodeview1.HeadersVisible = true;

			nodeview1.Model = liststoreall;

			nodeview1.SizeAllocated += NodeViewSizeAllocated;
			// on resize of nodeview1, set renderer1.wrapwidth
		}

		int Renderer2WrapWidth = -1;

		void NodeViewSizeAllocated(object o, SizeAllocatedArgs args)
		{
			NodeView nodeview = o as NodeView;

			TreeViewColumn column1 = nodeview.Columns[0] as TreeViewColumn;

			TreeViewColumn column2 = nodeview.Columns[1] as TreeViewColumn;
			CellRendererText renderer2 = column2.CellRenderers[0] as CellRendererText;


			if (Renderer2WrapWidth != args.Allocation.Width - column1.Width)
			{
				// string info = "WW: " + Renderer2WrapWidth + " => " + (args.Allocation.Width - column1.Width);
				// TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", info);

				Renderer2WrapWidth = args.Allocation.Width - column1.Width;
				renderer2.WrapWidth = Renderer2WrapWidth;
			}
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

		public void TreeAddSub(TypeIDTreeNode typeid, TypeIDTreeNode parenttypeid)
		{
			if ((parenttypeid == null) || (typeid == null))
				return;

			TreeIter iter;
			if (!storeiterators.GetByTypeRaw(parenttypeid.entryType, out iter))
				return;

			TreeIter itersub;
			HolderTreeNode parent = null;
			for(int i = store.IterNChildren(iter); i > 0; i--)
				if (store.IterNthChild(out itersub, iter, i - 1))
				{
					parent = store.GetValue(itersub, 0) as HolderTreeNode;
					if ((parent.typeid.entryType == parenttypeid.entryType) &&
						(parent.typeid.id == parenttypeid.id))
						break;

					parent = null;
				}

			if (parent == null)
				return;

			TreeIter itersubsub;
			HolderTreeNode holder;
			for(int i = store.IterNChildren(itersub); i > 0; i--)
				if (store.IterNthChild(out itersubsub, itersub, i - 1))
				{
					holder = store.GetValue(itersubsub, 0) as HolderTreeNode;
				    if ((holder.typeid.entryType == typeid.entryType) &&
				        (holder.typeid.id == typeid.id))
					    return;
				}

			holder = new HolderTreeNode(typeid);

			store.AppendValues(itersub, holder);
			treeview1.ExpandAll();
			treeview1.QueueDraw();
		}
			
		public void TreeDel(TypeIDTreeNode typeid)
		{
			storeiterators.Delete(typeid);
			treeview1.QueueDraw();
		}

		public void TreeDelSub(TypeIDTreeNode typeid, TypeIDTreeNode parenttypeid)
		{
		}

		public void TreeUpdate(TypeIDTreeNode typeid)
		{
			// potentially update Holder if Mark/Text/Tip moves there
			treeview1.QueueDraw();
		}

		public void TreeUpdateSub(TypeIDTreeNode typeid, TypeIDTreeNode parenttypeid)
		{
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
			ListStoreSourceTypeID liststorenew = new ListStoreSourceTypeID(type, typeid.ids(), typeof(string), typeof(string), typeof(byte), typeof(UInt16), typeof(Int64));
			nodeview.Model = liststorenew;

			liststorepartial.Add(liststorenew);

			ScrolledWindow scrolledwindow = new ScrolledWindow();
			scrolledwindow.Add(nodeview);

			string label = "???";
			if (typeid.entryType == TypeIDTreeNode.EntryType.Friend)
			{
				FriendTreeNode friend = typeid as FriendTreeNode;
				if (friend.name.Length > 0)
					label = "{" + friend.name + "}";
				else
					label = ":[" + friend.id + "]:";
			}
			else if (typeid.entryType == TypeIDTreeNode.EntryType.Group)
			{
				GroupTreeNode group = typeid as GroupTreeNode;
				if (group.name.Length > 0)
					label = "#" + group.name;
				else
					label = ":#" + group.id + ":";
			}

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
			System.Drawing.Point pos;
			pos.X = (int)args.Event.X;
			pos.Y = (int)args.Event.Y;

			Interfaces.Button button = Interfaces.Button.None;
			switch(args.Event.Button)
			{
				case 1: button = Interfaces.Button.Left;
						break;
				case 2: button = Interfaces.Button.Middle;
						break;
				case 3: button = Interfaces.Button.Right;
						break;
			}

			Interfaces.Click click = Interfaces.Click.None;
			switch (args.Event.Type)
			{
				case Gdk.EventType.ButtonRelease:
					click = Interfaces.Click.Single;
					break;
				case Gdk.EventType.TwoButtonPress:
					click = Interfaces.Click.Double;
					break;
			}

			TreePath path;
			TreeViewColumn col;
			if (treeview1.GetPathAtPos(pos.X, pos.Y, out path, out col))
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

				uiactions.TreePopup(o, pos, typeid, button, click);
			}
			else
				uiactions.TreePopup(o, pos, null, button, click);
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
