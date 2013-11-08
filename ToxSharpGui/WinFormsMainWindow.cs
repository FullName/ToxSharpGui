
using System;
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
				people.ExpandAll();
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
					}
			}
			else
				foreach(HolderTreeNode parent in people.Nodes)
					foreach(HolderTreeNode child in parent.Nodes)
						if (child.typeid == typeid)
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
			// TODO: add to all which match and to page0

			// add a new row to listview
			// TODO: multiple rows if source or text contain newlines
			WinForms.TabPage page = pages.SelectedTab;
			WinForms.ListView output = page.Controls[0] as WinForms.ListView;
			output.Items.Add(source).SubItems.Add(text);

			// TODO: scroll to bottom
		}

		// create and execute a popup menu
		public void PopupMenuDo(Interfaces.PopupEntry[] entries)
		{
		}

		// ask user for two strings: the ID and a message for a friend-invite
		public bool AskIDMessage(string explainID, string explainMessage, out string ID, out string message)
		{
			ID = "";
			message = "";
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
			people.MouseClick += TreeViewVoidMouseSingleClickHandler;
			people.MouseDoubleClick += TreeViewVoidMouseDoubleClickHandler;
			people.NodeMouseClick += TreeViewNodeMouseSingleClick;
			people.NodeMouseDoubleClick += TreeViewNodeMouseDoubleClick;

			people.Left = 5;
			people.Top = connectstate.Bottom + 5;
			people.Width = LeftWidth;
			people.Height = HeightMin - connectstate.Height;
			Controls.Add(people);

			WinForms.ListView output = new WinForms.ListView();
			output.View = WinForms.View.Details;
			output.Scrollable = true;
			output.Columns.Add("Source", 60);
			output.Columns.Add("Text", RightWidth - 15 - 60);
			output.HeaderStyle = WinForms.ColumnHeaderStyle.Nonclickable;

			output.Width = RightWidth;
			output.Height = HeightMin - connectstate.Height;

			WinForms.TabPage page = new WinForms.TabPage("Main");
			page.Controls.Add(output);

			pages = new WinForms.TabControl();
			pages.Alignment = WinForms.TabAlignment.Bottom;

			pages.Left = people.Right + 5;
			pages.Top = 5;
			pages.Width = RightWidth;
			pages.Height = HeightMin - connectstate.Height;
			pages.Controls.Add(page);

			Controls.Add(pages);

			input = new WinForms.TextBox();
			input.Multiline = false;
			input.KeyPress += TextBoxKeyPressHandler;

			input.Left = pages.Left;
			input.Width = RightWidth;
			input.Top = pages.Bottom + 5;
			input.Height = connectstate.Height;
			Controls.Add(input);

			Resize += ResizeHandler;
			Closed += ClosedHandler;

			// TODO: Focus => tb
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

		private void PageUpdate()
		{
			WinForms.TabPage page = pages.SelectedTab;
			WinForms.ListView output = page.Controls[0] as WinForms.ListView;
			output.Width = pages.Width;
			output.Height = pages.Height;
		}

		void TreeViewNodeMouseSingleClick(object sender, WinForms.TreeNodeMouseClickEventArgs e)
		{
		}

		void TreeViewNodeMouseDoubleClick(object sender, WinForms.TreeNodeMouseClickEventArgs e)
		{
		}

		void TreeViewVoidMouseSingleClickHandler(object sender, WinForms.MouseEventArgs e)
		{
		}

		void TreeViewVoidMouseDoubleClickHandler(object sender, WinForms.MouseEventArgs e)
		{
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

			// find the first next header to insert over
			HeaderTreeNode header = new HeaderTreeNode(typeid.entryType);
			HolderTreeNode holder = new HolderTreeNode(header);
			headers[id] = holder;
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
