
using System;
using System.Drawing;
using System.Collections.Generic;

namespace ToxSharpBasic
{
	public class TypeIDTreeNode
	{
		public enum EntryType { Header, Friend, Stranger, Group, Invitation, Rendezvous, GroupMember };

		public EntryType entryType;
		public UInt32 id; // on all but GroupMember: id, else: { group.id | id }

		public UInt16 ids()
		{
			if (id < UInt16.MaxValue)
				return (UInt16)id;

			throw new ArgumentOutOfRangeException();
		}

		public TypeIDTreeNode(EntryType entryType, UInt32 id)
		{
			this.entryType = entryType;
			this.id = id;
		}

		public virtual UInt16 Check()
		{
			return 0;
		}

		public virtual string Text()
		{
			return null;
		}

		public virtual string TooltipText()
		{
			return null;
		}
	}

	public class HeaderTreeNode : TypeIDTreeNode
	{
		public string title;
		
		public HeaderTreeNode(EntryType type) : base(EntryType.Header, (UInt16)type)
		{
			switch(type)
			{
				case EntryType.Friend:
					title = "Friends";
					break;
				case EntryType.Stranger:
					title = "Strangers";
					break;
				case EntryType.Group:
					title = "Groups";
					break;
				case EntryType.Invitation:
					title = "Invites";
					break;
				case EntryType.Rendezvous:
					title = "Rendezvous";
					break;
				default:
					title = "???";
					break;
			}
		}

		public override string Text()
		{
			return title;
		}
	}

	public class Interfaces
	{
		public enum SourceType { Friend, Group, System, Debug };

		public class PopupEntry
		{
			public int parent;      // -1 for none, index to parent otherwise
			public string title;    // visible title
			public string action;   // given to handler

			public EventHandler handle;
		}

		public interface IUIFactory
		{
			// GTK barfs if Application is setup/torn down from inside a GTK object
			IUIReactions Create();

			// close down application
			void Quit();
		}

		// independence of GUI toolkit: required reactions
		public interface IUIReactions
		{
			// init main window
			void Init(Interfaces.IUIActions uiactions);

			// run application (main loop, when this comes back, program terminates)
			void Run();

			// main window: title parts
			void TitleUpdate(string name, string ID);

			// left side: connect "button"
			void ConnectState(bool connected, string text);

			// left side: tree
			void TreeAdd(TypeIDTreeNode typeid);
			void TreeDel(TypeIDTreeNode typeid);
			void TreeUpdate(TypeIDTreeNode typeid);

			// left side: subtree
			void TreeAddSub(TypeIDTreeNode typeid, TypeIDTreeNode parenttypeid);
			void TreeDelSub(TypeIDTreeNode typeid, TypeIDTreeNode parenttypeid);
			void TreeUpdateSub(TypeIDTreeNode typeid, TypeIDTreeNode parenttypeid);

			// external: clipboard
			void ClipboardSend(string text);

			// right side: multi-tab
			bool CurrentTypeID(out SourceType type, out UInt32 id);
			void TextAdd(SourceType type, UInt32 id, string source, string text);

			// create and execute a popup menu (added Do due to name clash for GTK)
			void PopupMenuDo(object parent, Point position, PopupEntry[] entries);
			string PopupMenuAction(object o, System.EventArgs args);

			// used to ask user for two strings: the ID and a message for a friend-invite
			bool AskIDMessage(string message, string name1, string name2, out string input1, out string input2);

			// request to close down application
			void Quit();
		}

		public enum Button { None, Left, Middle, Right };
		public enum Click { None, Single, Double };

		public enum InputKey { None, Up, Down, Tab, Return };

		public interface IUIActions
		{
			// click on tree item, parent object is potentially needed as popup menu parent
			void TreePopup(object parent, Point position, TypeIDTreeNode typeid, Button button, Click click);

			// if true, line was evaluated and shall be cleared
			bool InputLine(string text, InputKey key);

			// ui is almost done, stop threading and save state
			void QuitPrepare();
		}

		public interface IActions
		{
			// InputHandling and Popup share a LOT of code
			// consolidate (probably into ToxGlue?)
		}

		internal interface IDataReactions
		{
			void FriendCount(out int friendsonline, out int friendstotal);

			TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, UInt32 id);
			TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, ToxKey key);

			// enumerator for all groups
			bool GroupEnumerator(out Dictionary<UInt32, TypeIDTreeNode> groups);

			// returns number of matches
			int FindFriendsWithKeyStartingWithID(string idpartial, out FriendTreeNode friend);
			int FindFriendsWithNameOrKeyStartingWithID(string idpartial, out FriendTreeNode friend);

			RendezvousTreeNode FindRendezvous(string text);

			// creates a tree node, typeid is stored in local store
			void Add(TypeIDTreeNode typeid);

			// deletes from datastorage
			void Delete(TypeIDTreeNode typeid);
		}
	}
}
