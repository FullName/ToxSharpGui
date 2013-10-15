
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

			if (item.Name == "new:group")
			{
				int groupnumber;
				if (toxsharp.ToxGroupchatAdd(out groupnumber))
				{
					GroupTreeNode groupchat = new GroupTreeNode((UInt16)groupnumber, null, null);
					HolderTreeNode holder = datastorage.HolderTreeNodeNew(groupchat);
					reactions.TreeAdd(holder);
				}

				return;
			}

			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "Unhandled new something: " + item.Name);
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

			if (item.Name.Substring(0, 7) == "invite:")
			{
				string friendkey_groupid_extra = item.Name.Substring(7);
				int poscolon2 = friendkey_groupid_extra.IndexOf(':');
				if (poscolon2 < 0)
					return;

				string friendkeystr = friendkey_groupid_extra.Substring(0, poscolon2 - 1);

				string groupid_extra = friendkey_groupid_extra.Substring(poscolon2 + 1);
				UInt16 groupid;
				int poscolon3 = groupid_extra.IndexOf(':');
				if (poscolon3 > 0)
					groupid = Convert.ToUInt16(groupid_extra.Substring(0, poscolon3));
				else
					groupid = Convert.ToUInt16(groupid_extra);

				ToxKey friendkey = new ToxKey(friendkeystr);
				if (toxsharp.ToxGroupchatInvite(groupid, friendkey))
					TextAdd(Interfaces.SourceType.Group, groupid, "GROUPINVITE", "Friend {" + friendkey.str.Substring(0, 8) + "...} invited to group #" + groupid + ".");
				else
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Failed to invite friend {" + friendkey.str.Substring(0, 8) + "...} to group #" + groupid + ".");
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


		public void TreeViewPopupInvite(object o, System.EventArgs args)
		{
			Gtk.MenuItem item = o as Gtk.MenuItem;
			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "invite action: " + item.Name);
			if (item.Name.Substring(0, 7) == "accept:")
			{
				int colon2 = item.Name.IndexOf(':', 7);
				if (colon2 < 8)
					return;

				int friendnumber;
				string friendnumstr = item.Name.Substring(7, colon2 - 7);
				friendnumber = Convert.ToUInt16(friendnumstr);

				string keystr = item.Name.Substring(colon2 + 1);
				TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "stranger action: ACCEPT => [" + keystr + "]");
				ToxKey groupkey = new ToxKey(keystr);

				int groupnumber;
				if (toxsharp.ToxGroupchatJoin(friendnumber, groupkey, out groupnumber))
				{
					TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Invitation, groupkey);
					if (typeid != null)
					{
						datastorage.StoreDelete(typeid);
						reactions.TreeUpdate();
					}

					GroupTreeNode group = new GroupTreeNode((UInt16)groupnumber, groupkey, null);
					datastorage.Add(group);
					HolderTreeNode holder = HolderTreeNode.Create(group);
					reactions.TreeAdd(holder);
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
					// - delete, invite to group, name, hint
					// stranger:
					// - accept, decline
					// group:
					// - delete
					// invite:
					// - accept, decline

					if (typeid.entryType == TypeIDTreeNode.EntryType.Friend)
					{
						FriendTreeNode friend = typeid as FriendTreeNode;
						if (friend == null)
							return;

						Gtk.Menu menu = new Gtk.Menu();

						Gtk.MenuItem itemfriend = new Gtk.MenuItem("Invite to group");
						itemfriend.Name = "invite:" + friend.key.str;
						itemfriend.Sensitive = false;

						bool submenu = false;

						Dictionary<UInt16, TypeIDTreeNode> groups;
						if (datastorage.GroupEnumerator(out groups) && (groups.Count > 0))
						{
							bool gotone = false;

							Gtk.Menu menugroups;
							if (!submenu)
								menugroups = null;
							else
								menugroups = new Gtk.Menu();

							Gtk.MenuItem itemgroup;
							foreach(KeyValuePair<UInt16, TypeIDTreeNode> pair in groups)
							{
								GroupTreeNode group = pair.Value as GroupTreeNode;
								if (group != null)
								{
									gotone = true;

									string text = "Group #" + group.id;
									if (group.name != null)
										text += " " + group.name;
									if (group.key != null)
										text += " (" + group.key.str.Substring(0, 8) + "...)";
									if (!submenu)
										text = "Invite to " + text;
									itemgroup = new Gtk.MenuItem(text);
									string name = "invite:" + friend.key.str + ":" + group.id;
									if (group.key != null)
										name += ":" + group.key.str;
									itemgroup.Name = name;
									itemgroup.Activated += TreeViewPopupFriend;
									itemgroup.Show();

									if (submenu)
										menugroups.Append(itemgroup);
									else
										menu.Append(itemgroup);
								}
							}

							if (gotone && submenu)
							{
								// TODO: submenu fails to fire on selection
								itemfriend.Sensitive = true;
								itemfriend.Submenu = menugroups;
							}
						}

						if (submenu)
						{
							itemfriend.Show();
							menu.Append(itemfriend);
						}

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

					if (typeid.entryType == TypeIDTreeNode.EntryType.Invitation)
					{
						InvitationTreeNode invite = typeid as InvitationTreeNode;
						if (invite == null)
							return;

						Gtk.Menu menu = new Gtk.Menu();
						Gtk.MenuItem inviteaccept = new Gtk.MenuItem("Accept invite");
						inviteaccept.Name = "accept:" + invite.inviterid + ":" + invite.key.str;
						inviteaccept.Activated += TreeViewPopupInvite;
						inviteaccept.Show();
						menu.Append(inviteaccept);

						Gtk.MenuItem invitedecline = new Gtk.MenuItem("Decline invite");
						invitedecline.Name = "decline:" + invite.key.str;
						invitedecline.Activated += TreeViewPopupInvite;
						invitedecline.Show();
						menu.Append(invitedecline);

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

