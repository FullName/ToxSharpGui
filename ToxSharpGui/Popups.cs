
using System;
using System.Collections.Generic;

namespace ToxSharpGui
{
	public class Popups
	{
		protected Interfaces.IReactions reactions;
		protected ToxSharp toxsharp;
		protected DataStorage datastorage;

		public Popups(Interfaces.IReactions reactions, ToxSharp toxsharp, DataStorage datastorage)
		{
			this.reactions = reactions;
			this.toxsharp = toxsharp;
			this.datastorage = datastorage;
		}

		protected void TextAdd(Interfaces.SourceType type, UInt16 id, string source, string text)
		{
			reactions.TextAdd(type, id, source, text);
		}

		public void TreeViewPopupNew(object o, System.EventArgs args)
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
							TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "Friend request sent to " + friendkey.str + ".");
							toxsharp.ToxFriendInit(friendid);
						}
					}
				}

				return;
			}

			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "new something: " + item.Name);
		}

		public void TreeViewPopupFriend(object o, System.EventArgs args)
		{
			Gtk.MenuItem item = o as Gtk.MenuItem;
			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "friend action: " + item.Name);
			if (item.Name.Substring(0, 7) == "remove:")
			{
				FriendTreeNode friend = null;
				int foundnum = datastorage.FindFriendsWithKeyStartingWithID(item.Name.Substring(7), out friend);
				if (foundnum == 1)
				{
					int code = toxsharp.ToxFriendDel(friend.key);
					if (code == 0)
					{
						datastorage.StoreDelete(friend);
						reactions.TreeUpdate();
					}
				}
			}
		}

		public void TreeViewPopupStranger(object o, System.EventArgs args)
		{
			Gtk.MenuItem item = o as Gtk.MenuItem;
			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "stranger action: " + item.Name);
			if (item.Name.Substring(0, 7) == "accept:")
			{
				string keystr = item.Name.Substring(7);
				TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "stranger action: ACCEPT => [" + keystr + "]");
				ToxKey key = new ToxKey(keystr);
				int i = toxsharp.ToxFriendAddNoRequest(key);
				if (i >= 0)
				{
					TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Stranger, key);
					if (typeid != null)
					{
						datastorage.StoreDelete(typeid);
						reactions.TreeUpdate();
					}
				}
			}
			else if (item.Name.Substring(0, 8) == "decline:")
			{
				string id = item.Name.Substring(8);
				TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "stranger action: DECLINE => [" + id + "]");
			}
		}

		public void TreePopup(TypeIDTreeNode typeid, Gdk.EventButton eventbutton)
		{
			if (typeid != null)
			{
				// dbl-click/middle-click handled in caller

				if (eventbutton.Button == 3)
				{
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
				if (eventbutton.Button != 3)
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
	}
}

