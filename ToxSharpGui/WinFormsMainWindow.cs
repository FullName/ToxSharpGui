
using System;
using System.Drawing;
using WinForms = System.Windows.Forms;

using ToxSharpBasic;

namespace ToxSharpWinForms
{
	public class WinFormsMainWindow : WinForms.Form, Interfaces.IUIReactions, IMainWindow
	{
		// Interfaces.IUIReactions
		public void ConnectState(bool state, string text)
		{
			connectstate.Checked = state;
			connectstate.Text = text;
		}

		public void TitleUpdate()
		{
			string name = toxif.ToxNameGet();
			string selfid = toxif.ToxSelfID();
			Text = "Tox# - " + name + " [" + selfid + "]";
		}

		public void TreeAdd(TypeIDTreeNode typeid)
		{
			HolderTreeNode parent = TreeParent(typeid);
			if (parent != null)
			{
				parent.Nodes.Add(new HolderTreeNode(typeid));
				parent.Expand();
				TreeUpdate(typeid);
			}
		}

		public void TreeDel(TypeIDTreeNode typeid)
		{
			HolderTreeNode parent = TreeParent(typeid);
			if (parent == null)
				return;

			foreach(HolderTreeNode child in parent.Nodes)
				if (child.typeid == typeid)
				{
					parent.Nodes.Remove(child);
					if (parent.Nodes.Count == 0)
						people.Nodes.Remove(parent);

					break;
				}

			people.Refresh();
			TreeUpdate(null);
		}

		public void TreeUpdate(TypeIDTreeNode typeid)
		{
			if (typeid != null)
			{
				HolderTreeNode parent = TreeParent(typeid);
				foreach(HolderTreeNode child in parent.Nodes)
					if (child.typeid == typeid)
					{
						child.Text = child.typeid.Text();
						child.ToolTipText = child.typeid.TooltipText();
					    break;
					}
			}
			else
				foreach(HolderTreeNode parent in people.Nodes)
					foreach(HolderTreeNode child in parent.Nodes)
					{
						child.Text = child.typeid.Text();
						child.ToolTipText = child.typeid.TooltipText();
					}

			people.Refresh();
		}

		// external: clipboard
		public void ClipboardSend(string text)
		{
			WinForms.Clipboard.SetText(text);
		}

		// right side: multi-tab
		public bool CurrentTypeID(out Interfaces.SourceType type, out System.UInt16 id)
		{
			type = Interfaces.SourceType.Debug;
			id = 0;
			return false;
		}

		public void TextAdd(Interfaces.SourceType type, UInt16 id, string source, string text)
		{
			// TODO: multiple rows if source or text contain newlines
			// TODO: split across multiple rows, mark as continuation, in resize recalc

			WinForms.TabPage main = pages.TabPages[0];
			WinForms.ListView output = main.Controls[0] as WinForms.ListView;
			WinForms.ListViewItem item = output.Items.Add(source);
			item.SubItems.Add(text);
			output.EnsureVisible(item.Index);

			if ((type == Interfaces.SourceType.Friend) ||
			    (type == Interfaces.SourceType.Group))
				foreach(WinForms.TabPage wfpage in pages.TabPages)
				{
					TabbedPage page = wfpage as TabbedPage;
					if (page.Is(type, id))
					{
						output = wfpage.Controls[0] as WinForms.ListView;
						item = output.Items.Add(source);
						item.SubItems.Add(text);
						output.EnsureVisible(item.Index);
					}
				}
		}

		// create and execute a popup menu
		public void PopupMenuDo(object parent, Point position, Interfaces.PopupEntry[] entries)
		{
			WinForms.Control parentcontrol = parent as WinForms.Control;
			if ((parentcontrol == null) || (entries.Length == 0))
				return;

			for(int i = 0; i < entries.Length; i++)
				if (entries[i].parent >= 0)
				{
				    // first parents, then children, no self-ref
					if (entries[i].parent >= i)
						return;

					// out of bounds
				    if (entries[i].parent >= entries.Length)
						return;
				}

			// generate lowest children
			// generate their parents
			// until all is tied in
			WinForms.ContextMenu[] menus = new WinForms.ContextMenu[entries.Length];
			WinForms.MenuItem[] items = new WinForms.MenuItem[entries.Length];

			for(int i = 0; i < entries.Length; i++)
			{
				Interfaces.PopupEntry entry = entries[i];

				items[i] = new WinForms.MenuItem(entry.title, entry.handle);
				items[i].Name = entry.action;
				items[i].Visible = true;

				if (entry.parent >= 0)
				{
					if (menus[entry.parent] == null)
					{
						menus[entry.parent] = new WinForms.ContextMenu();
						items[entry.parent].Enabled = false;
						items[entry.parent].MergeMenu(menus[entry.parent]);
					}

					menus[entries[i].parent].MenuItems.Add(items[i]);
				}
			}

			WinForms.ContextMenu menu = new WinForms.ContextMenu();

			for(int i = 0; i < entries.Length; i++)
				menu.MenuItems.Add(items[i]);

			menu.Show(parentcontrol, position);
		}

		public string PopupMenuAction(object o, System.EventArgs args)
		{
			WinForms.MenuItem item = o as WinForms.MenuItem;
			if (item != null)
				return item.Name;
			else
				return null;
		}

		// ask user for two strings: the ID and a message for a friend-invite
		public bool AskIDMessage(string message, string name1, string name2, out string input1, out string input2)
		{
			// TODO: dialog
			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "Not yet implemented. Use the command line. Sorry!");

			input1 = null;
			input2 = null;
			return false;
		}

		// close down application
		public void Quit()
		{
			ClosedHandler(null, null);
		}


		private WinForms.CheckBox connectstate;
		private WinForms.TreeView people;

		private WinForms.TabControl pages;
		private WinForms.TextBox  input;

		private const int WidthMin = 400;
		private const int HeightMin = 300;
		private const int LeftWidth = 160;

		public WinFormsMainWindow()
		{
			toxif = null;
			inputhandling = null;
			datareactions = null;

			SetClientSizeCore(WidthMin + 15, HeightMin + 15);

			int RightWidth = WidthMin - LeftWidth;

			connectstate = new WinForms.CheckBox();
			connectstate.Text = "Disconnected.";
			connectstate.Enabled = false;

			connectstate.Left = 5;
			connectstate.Width = LeftWidth;
			Controls.Add(connectstate);

			people = new WinForms.TreeView();
			people.ShowNodeToolTips = true;
			people.MouseClick += TreeViewMouseSingleClickHandler;
			people.MouseDoubleClick += TreeViewMouseDoubleClickHandler;
			people.KeyUp += TreeViewKeyUp;
			people.MouseUp += TreeViewMouseUp;

			people.Left = 5;
			people.Top = connectstate.Bottom + 5;
			people.Width = LeftWidth;
			people.Height = HeightMin - connectstate.Height;
			Controls.Add(people);

			pages = new WinForms.TabControl();
			pages.Alignment = WinForms.TabAlignment.Bottom;

			pages.Left = people.Right + 5;
			pages.Top = 5;
			pages.Width = RightWidth;
			pages.Height = HeightMin - connectstate.Height;

			Controls.Add(pages);

			input = new WinForms.TextBox();
			input.Multiline = false;
			input.KeyPress += TextBoxKeyPressHandler;

			input.Left = pages.Left;
			input.Width = RightWidth;
			input.Top = pages.Bottom + 5;
			input.Height = connectstate.Height;
			Controls.Add(input);

			PageAdd(null, "Main");

			Resize += ResizeHandler;
			Closed += ClosedHandler;

			// TODO: Focus => tb
			input.Select();
		}

		void TreeViewKeyUp (object sender, WinForms.KeyEventArgs e)
		{
			if ((e.Modifiers == WinForms.Keys.None) &&
			    (e.KeyData == WinForms.Keys.Return))
			{
				WinForms.TreeView view = sender as WinForms.TreeView;
				if (view != null)
				{
					WinForms.TreeNode node = view.SelectedNode;
					if (node != null)
					{
						HolderTreeNode holder = node as HolderTreeNode;
						if (holder != null)
							PageAdd(holder.typeid, holder.typeid.Text());
					}
				}
			}
			else
				TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "KEYUP: " + e.KeyValue);
		}

		protected ToxInterface toxif;
		protected Interfaces.IDataReactions datareactions;
		protected InputHandling inputhandling;
		protected Popups popups;

		public void Init(ToxInterface toxif, Interfaces.IDataReactions datareactions, InputHandling inputhandling, Popups popups)
		{
			this.toxif = toxif;
			this.datareactions = datareactions;
			this.inputhandling = inputhandling;
			this.popups = popups;
		}

		public void Do()
		{
			WinForms.Application.Run(this);
		}

		void ClosedHandler(object sender, EventArgs e)
		{
			toxif.ToxStopAndSave();
			WinForms.Application.Exit();
		}

		private void ResizeHandler(object sender, EventArgs e)
		{
			int HeightNow = Height - 40;
			people.Height = HeightNow - connectstate.Height;
			pages.Height  = people.Height;
			input.Top     = pages.Bottom + 5;

			pages.Width = Width - 25 - LeftWidth;
			input.Width = pages.Width;

			PageUpdate();
		}

		class TabbedPage : WinForms.TabPage
		{
			protected Interfaces.SourceType type;
			protected UInt16 id;

			protected TabbedPage(WinForms.TabControl pages, WinForms.TextBox input, Interfaces.SourceType type, UInt16 id, string title) : base(title)
			{
				this.type = type;
				this.id = id;

				int RightWidth = pages.Width;

				WinForms.ListView output = new WinForms.ListView();
				output.View = WinForms.View.Details;
				output.Scrollable = true;
				output.Columns.Add("Source", 60);
				output.Columns.Add("Text", RightWidth - 24 - 60);
				output.HeaderStyle = WinForms.ColumnHeaderStyle.Nonclickable;

				output.Width = RightWidth;
				output.Height = HeightMin - input.Height - 28;

				Controls.Add(output);
			}

			public bool Is(Interfaces.SourceType type, UInt16 id)
			{
				return ((this.type == type) && (this.id == id));
			}


			public static void PageAdd(WinForms.TabControl pages, WinForms.TextBox input, TypeIDTreeNode typeid, string title)
			{
				Interfaces.SourceType type = Interfaces.SourceType.System;
				UInt16 id = 0;
				if (pages.Controls.Count > 0)
				{
					if (typeid == null)
						return;

					if (typeid.entryType == TypeIDTreeNode.EntryType.Friend)
						type = Interfaces.SourceType.Friend;
					else if (typeid.entryType == TypeIDTreeNode.EntryType.Group)
						type = Interfaces.SourceType.Group;
					else
						return;

					foreach(WinForms.TabPage wfpage in pages.TabPages)
					{
						TabbedPage page = wfpage as TabbedPage;
						if (page != null)
							if (page.Is(type, typeid.id))
							{
								pages.SelectedTab = page;
								return;
							}
					}

					id = typeid.id;
				}

				TabbedPage tabbedpage = new TabbedPage(pages, input, type, id, title);
				pages.Controls.Add(tabbedpage);
				pages.SelectedTab = tabbedpage;
			}
		}

		protected void PageAdd(TypeIDTreeNode typeid, string title)
		{
			if (pages.Controls.Count > 0)
				if ((typeid.entryType != TypeIDTreeNode.EntryType.Friend) &&
			        (typeid.entryType != TypeIDTreeNode.EntryType.Group))
					return;

			TabbedPage.PageAdd(pages, input, typeid, title);
		}

		protected void PageUpdate()
		{
			WinForms.TabPage page = pages.SelectedTab;
			WinForms.ListView output = page.Controls[0] as WinForms.ListView;
			output.Width = pages.Width;
			output.Height = pages.Height - 28;
		}

		void TreeViewMouseUp(object sender, WinForms.MouseEventArgs e)
		{
			// non-node area handling must be done here
			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "MouseUp@" + e.X + ":" + e.Y);
			TreeViewMouseClick(sender, e.Location, e.Button, Popups.Click.Single);
		}

		void TreeViewMouseClick(object sender, Point location, WinForms.MouseButtons wfbutton, Popups.Click click)
		{
			string dbgmsg = "TVNMC: ";

			WinForms.TreeView view = sender as WinForms.TreeView;
			WinForms.TreeNode node = view.GetNodeAt(location);
			HolderTreeNode holder = node as HolderTreeNode;
			TypeIDTreeNode typeid = null;
			if (holder != null)
			{
				typeid = holder.typeid;
				dbgmsg += "[^" + typeid.entryType.ToString() + ":" + typeid.id + "] ";
			}
			else if (node != null)
				return;

			Popups.Button button = Popups.Button.None;
			switch(wfbutton)
			{
				case WinForms.MouseButtons.Left:
					button = Popups.Button.Left;
					dbgmsg += "L";
					break;
				case WinForms.MouseButtons.Middle:
					button = Popups.Button.Middle;
					dbgmsg += "M";
					break;
				case WinForms.MouseButtons.Right:
					button = Popups.Button.Right;
					dbgmsg += "R";
					break;
			}

			dbgmsg += click.ToString();
			// TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", dbgmsg);

			if (typeid == null)
				popups.TreePopup(this, Location, typeid, button, click);
			else
				popups.TreePopup(sender, Location, typeid, button, click);
		}

		void TreeViewMouseSingleClickHandler(object sender, WinForms.MouseEventArgs e)
		{
			// TreeViewMouseClick(sender, e.Location, e.Button, Popups.Click.Single);
		}

		void TreeViewMouseDoubleClickHandler(object sender, WinForms.MouseEventArgs e)
		{
			// TreeViewMouseClick(sender, e.Location, e.Button, Popups.Click.Double);
			WinForms.TreeView view = sender as WinForms.TreeView;
			if (view == null)
				return;

			WinForms.TreeNode node = view.GetNodeAt(e.Location);
			HolderTreeNode holder = node as HolderTreeNode;
			if (holder == null)
				return;

			TypeIDTreeNode typeid = holder.typeid;
			PageAdd(typeid, typeid.Text());
		}

		void TextBoxKeyPressHandler(object sender, WinForms.KeyPressEventArgs e)
		{
			switch((WinForms.Keys)e.KeyChar)
			{
				case WinForms.Keys.Up:
				    inputhandling.Do(input.Text, InputKey.Up);
					break;
				case WinForms.Keys.Down:
					inputhandling.Do(input.Text, InputKey.Down);
					break;
				case WinForms.Keys.Tab:
					inputhandling.Do(input.Text, InputKey.Tab);
					break;
				case WinForms.Keys.Return:
					inputhandling.Do(input.Text, InputKey.Return);
					break;
			}
		}

		protected HolderTreeNode[] headers;

		protected HolderTreeNode TreeParent(TypeIDTreeNode typeid)
		{
			if (headers == null)
			{
				int idmax = -1;
				Array valueAry = Enum.GetValues(typeof(TypeIDTreeNode.EntryType));
				foreach (int idenum in valueAry)
					if (idenum > idmax)
						idmax = idenum;

				idmax++;
				headers = new HolderTreeNode[idmax];
			}

			UInt16 id = (UInt16)typeid.entryType;
			if (headers[id] != null)
				return headers[id];

			HeaderTreeNode header = new HeaderTreeNode(typeid.entryType);
			HolderTreeNode holder = new HolderTreeNode(header);
			holder.Text = header.Text();
			holder.ToolTipText = header.TooltipText();
			headers[id] = holder;

			// find the first next header to insert over
			for(int next = id + 1; next < headers.Length; next++)
				if (headers[next] != null)
				{
					people.Nodes.Insert(headers[next].Index, holder);
					return holder;
				}

			people.Nodes.Add(holder);
			return holder;
		}
	}

	public class HolderTreeNode : WinForms.TreeNode
	{
		public TypeIDTreeNode typeid;

		public HolderTreeNode(TypeIDTreeNode typeid)
		{
			this.typeid = typeid;
		}
	}
}
