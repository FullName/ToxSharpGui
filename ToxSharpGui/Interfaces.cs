
using System;
using System.Drawing;
using System.Collections.Generic;

namespace ToxSharpBasic
{
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

		// independence of GUI toolkit: required reactions
		public interface IUIReactions
		{
			// main window: title
			void TitleUpdate();

			// left side: connect "button"
			void ConnectState(bool connected, string text);

			// left side: tree
			void TreeAdd(TypeIDTreeNode typeid);
			void TreeDel(TypeIDTreeNode typeid);
			void TreeUpdate(TypeIDTreeNode typeid);

			// external: clipboard
			void ClipboardSend(string text);

			// right side: multi-tab
			bool CurrentTypeID(out SourceType type, out System.UInt16 id);
			void TextAdd(SourceType type, System.UInt16 id, string source, string text);

			// create and execute a popup menu (added Do due to name clash for GTK)
			void PopupMenuDo(object parent, Point position, PopupEntry[] entries);
			string PopupMenuAction(object o, System.EventArgs args);

			// used to ask user for two strings: the ID and a message for a friend-invite
			bool AskIDMessage(string message, string name1, string name2, out string input1, out string input2);

			// close down application
			void Quit();
		}

		public interface IDataReactions
		{
			void FriendCount(out int friendsonline, out int friendstotal);

			TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, UInt16 id);
			TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, ToxKey key);

			// enumerator for all groups
			bool GroupEnumerator(out Dictionary<UInt16, TypeIDTreeNode> groups);

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
