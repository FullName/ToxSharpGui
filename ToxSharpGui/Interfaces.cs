
using System;
using System.Collections.Generic;

namespace ToxSharpBasic
{
	public class Interfaces
	{
		public enum SourceType { Friend, Group, System, Debug };

		public class PopupEntry
		{
			public uint parent;
			public string title;
			public string action;

			public delegate void Handle(string action);
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
			void PopupMenuDo(PopupEntry[] entries);

			// ask user for two strings: the ID and a message for a friend-invite
			bool AskIDMessage(string explainID, string explainMessage, out string ID, out string message);

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

			// creates a tree node, typeid is stored in local store
			void Add(TypeIDTreeNode typeid);

			// deletes from datastorage
			void Delete(TypeIDTreeNode typeid);
		}
	}
}

