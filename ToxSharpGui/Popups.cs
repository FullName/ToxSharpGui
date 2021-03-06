
using System;
using System.Drawing;
using System.Collections.Generic;

namespace ToxSharpBasic
{
	internal class Popups
	{
		protected ToxInterface toxsharp;
		protected Interfaces.IUIReactions uireactions;
		protected Interfaces.IDataReactions datareactions;
		protected IToxGlue toxglue;

		public Popups(ToxInterface toxsharp, Interfaces.IUIReactions uireactions, Interfaces.IDataReactions datareactions, IToxGlue toxglue)
		{
			this.toxsharp = toxsharp;
			this.uireactions = uireactions;
			this.datareactions = datareactions;
			this.toxglue = toxglue;
		}

		protected void TextAdd(Interfaces.SourceType type, UInt16 id, string source, string text)
		{
			uireactions.TextAdd(type, id, source, text);
		}

		protected void TreeViewPopupNew(object o, System.EventArgs args)
		{
			string action = uireactions.PopupMenuAction(o, args);
			if (action == null)
				return;

			if (action == "new:friend")
			{
				string message = "For this action, an ID is required.\nIt's a string of 78 characters.\n\nOptionally, you can add a message to your request.";
				string name1 = "ID:";
				string name2 = "Message:";

				string friendkeystr, friendmsg;
				if (!uireactions.AskIDMessage(message, name1, name2, out friendkeystr, out friendmsg))
					return;

				if (friendkeystr.Length != 2 * ToxInterface.ID_LEN_BINARY)
					return;

				ToxKey friendkey = new ToxKey(friendkeystr);
				int friendid = toxsharp.ToxFriendAdd(friendkey, friendmsg);
				if (friendid >= 0)
				{
					int add = 4;
					string friendkeystrspaced = friendkeystr.Substring(0, add);
					for(int i = add; i < ToxInterface.ID_LEN_BINARY * 2; i += add)
						friendkeystrspaced += " " + friendkeystr.Substring(i, add);
					TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "Friend request sent to " + friendkeystrspaced + ".");
					toxsharp.ToxFriendInit(friendid);
				}

				return;
			}

			if (action == "new:group")
			{
				toxglue.GroupchatAdd();
				return;
			}

			if (action == "new:rendezvous")
			{
				string input1, input2;
				if (!uireactions.AskIDMessage("You can enter a date&time, if missing, 'now' is assumed.\nYou must enter a text of at least 16 characters.",
											  "date/time:", "text:", out input1, out input2))
					return;

				if (input2.Length < 16)
					return;

				DateTime time = DateTime.Now;
				if (input1.Length > 0)
				{
					if (!DateTime.TryParse(input1, out time))
					{
						uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Failed to parse input for date/time.");
						return;
					}

					if (time < DateTime.Now)
					{
						uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Input for date/time is in the past.");
						return;
					}
				}

				toxglue.RendezvousCreateOrUpdate(input2, time);
				return;
			}

			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "Unhandled new something: " + action);
		}

		protected void TreeViewPopupFriend(object o, System.EventArgs args)
		{
			string action = uireactions.PopupMenuAction(o, args);
			if (action == null)
				return;

			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "friend action: " + action);
			if (action.Substring(0, 7) == "remove:")
			{
				FriendTreeNode friend = null;
				int foundnum = datareactions.FindFriendsWithKeyStartingWithID(action.Substring(7), out friend);
				if (foundnum == 1)
				{
					int code = toxsharp.ToxFriendDel(friend.key);
					if (code == 0)
					{
						datareactions.Delete(friend);
						uireactions.TreeDel(friend);
					}
				}
			}

			if (action.Substring(0, 7) == "invite:")
			{
				string friendkey_groupid_extra = action.Substring(7);
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

		protected void TreeViewPopupStranger(object o, System.EventArgs args)
		{
			string action = uireactions.PopupMenuAction(o, args);
			if (action == null)
				return;

			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "stranger action: " + action);
			if (action.Substring(0, 7) == "accept:")
			{
				string keystr = action.Substring(7);
				TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "stranger action: ACCEPT => [" + keystr + "]");
				ToxKey key = new ToxKey(keystr);
				int i = toxsharp.ToxFriendAddNoRequest(key);
				if (i >= 0)
				{
					TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Stranger, key);
					if (typeid != null)
					{
						datareactions.Delete(typeid);
						uireactions.TreeDel(typeid);
					}
				}
			}
			else if (action.Substring(0, 8) == "decline:")
			{
				string id = action.Substring(8);
				TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "stranger action: DECLINE => [" + id + "]");
			}
		}

		protected void TreeViewPopupGroup(object o, System.EventArgs args)
		{
			string action = uireactions.PopupMenuAction(o, args);
			if (action == null)
				return;

			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "group action: " + action);
			if (action.Substring(0, 7) == "delete:")
			{
				int colon2 = action.IndexOf(':', 7);
				if (colon2 < 8)
					return;

				int groupnumber;
				string groupnumstr = action.Substring(7, colon2 - 7);
				groupnumber = Convert.ToUInt16(groupnumstr);
/*
				ToxKey groupkey = null;
				string keystr = action.Substring(colon2 + 1);
				if (keystr.Length > 0)
					groupkey = new ToxKey(keystr);
*/
				if (toxsharp.ToxGroupchatDel(groupnumber))
				{
					TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Group, (UInt16)groupnumber);
					if (typeid != null)
					{
						datareactions.Delete(typeid);
						uireactions.TreeDel(typeid);
					}
				}
			}
		}

		protected void TreeViewPopupInvite(object o, System.EventArgs args)
		{
			string action = uireactions.PopupMenuAction(o, args);
			if (action == null)
				return;

			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "invite action: " + action);
			if (action.Substring(0, 7) == "accept:")
			{
				int colon2 = action.IndexOf(':', 7);
				if (colon2 < 8)
					return;

				int friendnumber;
				string friendnumstr = action.Substring(7, colon2 - 7);
				friendnumber = Convert.ToUInt16(friendnumstr);

				string keystr = action.Substring(colon2 + 1);
				TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "invite action: ACCEPT => [" + keystr + "]");
				ToxKey groupkey = new ToxKey(keystr);

				int groupnumber;
				if (toxsharp.ToxGroupchatJoin(friendnumber, groupkey, out groupnumber))
				{
					TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Invitation, groupkey);
					if (typeid != null)
					{
						datareactions.Delete(typeid);
						uireactions.TreeDel(typeid);
					}

					GroupTreeNode group = new GroupTreeNode((UInt16)groupnumber, groupkey, null);
					datareactions.Add(group);
					uireactions.TreeAdd(group);

					UInt16 i = 0;
					string membername;
					while(toxsharp.ToxGroupchatPeername(group.ids(), i, out membername))
					{
						GroupMemberTreeNode member = datareactions.Find(TypeIDTreeNode.EntryType.GroupMember, group.MemberID(i)) as GroupMemberTreeNode;
						if (member == null)
						{
							member = new GroupMemberTreeNode(group, i, membername);
							uireactions.TreeAddSub(member, group);
							datareactions.Add(member);
						}

						member.name = membername;
						i++;
					}
				}
			}
			else if (action.Substring(0, 8) == "decline:")
			{
				int colon2 = action.IndexOf(':', 7);
				if (colon2 < 8)
					return;
/*
				int friendnumber;
				string friendnumstr = action.Substring(7, colon2 - 7);
				friendnumber = Convert.ToUInt16(friendnumstr);
*/
				string keystr = action.Substring(colon2 + 1);
				TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "invite action: DECLINE => [" + keystr + "]: TODO.");
			}
		}

		protected void TreeViewPopupPublish(object o, System.EventArgs args)
		{
			string action = uireactions.PopupMenuAction(o, args);
			if (action == null)
				return;

			TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "publish action: " + action);
		}

		public void TreePopup(object parent, Point position, TypeIDTreeNode typeid, Interfaces.Button button, Interfaces.Click click)
		{
			if (typeid != null)
			{
				// dbl-click/middle-click handled in caller

				if (button == Interfaces.Button.Right)
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

						uint groupcnt = 0;
						Dictionary<UInt32, TypeIDTreeNode> groups;
						if (datareactions.GroupEnumerator(out groups) && (groups.Count > 0))
							foreach(KeyValuePair<UInt32, TypeIDTreeNode> pair in groups)
							{
								GroupTreeNode group = pair.Value as GroupTreeNode;
								if (group != null)
									groupcnt++;
							}

						bool submenu = false;

						Interfaces.PopupEntry[] entries;
						Interfaces.PopupEntry entry;

						if (groupcnt > 0)
						{
							if (submenu)
								entries = new Interfaces.PopupEntry[groupcnt + 2];
							else
								entries = new Interfaces.PopupEntry[groupcnt + 1];
						}
						else
							entries = new Interfaces.PopupEntry[1];

						if (entries == null)
							return;

						if (submenu && (groupcnt > 0))
						{
							entry = new Interfaces.PopupEntry();
							entry.parent = -1;
							entry.title = "Invite to group";
							entry.action = "invite:" + friend.key.str;
							// no handler
							entries[0] = entry;
						}

						if (groupcnt > 0)
						{
							uint groupcurr = 0;
							foreach(KeyValuePair<UInt32, TypeIDTreeNode> pair in groups)
							{
								GroupTreeNode group = pair.Value as GroupTreeNode;
								if (group != null)
								{
									string text = "Group #" + group.id;
									if (group.name != null)
										text += ": " + group.name;
									if (group.key != null)
										text += " (" + group.key.str.Substring(0, 8) + "...)";
									if (!submenu)
										text = "Invite to " + text;

									string name = "invite:" + friend.key.str + ":" + group.id;
									if (group.key != null)
										name += ":" + group.key.str;

									entry = new Interfaces.PopupEntry();
									entry.parent = -1;
									entry.title = text;
									entry.action = name;
									entry.handle = TreeViewPopupFriend;
									if (submenu)
									{
										entry.parent = 0;
										entries[groupcurr + 1] = entry;
									}
									else
										entries[groupcurr] = entry;
								}
							}
						}

						entry = new Interfaces.PopupEntry();
						entry.parent = -1;
						entry.title = "Remove from list";
						entry.action = "remove:" + friend.key.str;
						entry.handle = TreeViewPopupFriend;
						if (groupcnt == 0)
							entries[0] = entry;
						else if (submenu)
							entries[groupcnt + 1] = entry;
						else
							entries[groupcnt] = entry;

						uireactions.PopupMenuDo(parent, position, entries);
					}

					if (typeid.entryType == TypeIDTreeNode.EntryType.Group)
					{
						GroupTreeNode group = typeid as GroupTreeNode;
						if (group == null)
							return;

						Interfaces.PopupEntry[] entries = new Interfaces.PopupEntry[1];

						Interfaces.PopupEntry entry = new Interfaces.PopupEntry();
						entry.parent = -1;
						entry.title = "Delete group (membership)";
						entry.action = "delete:" + group.id + ":";
						if (group.key != null)
							entry.action += group.key.str;
						entry.handle += TreeViewPopupGroup;
						entries[0] = entry;

						uireactions.PopupMenuDo(parent, position, entries);
					}

					if (typeid.entryType == TypeIDTreeNode.EntryType.Stranger)
					{
						StrangerTreeNode stranger = typeid as StrangerTreeNode;
						if (stranger == null)
							return;

						Interfaces.PopupEntry[] entries = new Interfaces.PopupEntry[2];

						Interfaces.PopupEntry entry = new Interfaces.PopupEntry();
						entry.parent = -1;
						entry.title = "Accept as friend";
						entry.action = "accept:" + stranger.key.str;
						entry.handle += TreeViewPopupStranger;
						entries[0] = entry;

						entry = new Interfaces.PopupEntry();
						entry.parent = -1;
						entry.title = "Decline as friend";
						entry.action = "decline:" + stranger.key.str;
						entry.handle += TreeViewPopupStranger;
						entries[1] = entry;

						uireactions.PopupMenuDo(parent, position, entries);
					}

					if (typeid.entryType == TypeIDTreeNode.EntryType.Invitation)
					{
						InvitationTreeNode invite = typeid as InvitationTreeNode;
						if (invite == null)
							return;

						Interfaces.PopupEntry[] entries = new Interfaces.PopupEntry[2];

						Interfaces.PopupEntry entry = new Interfaces.PopupEntry();
						entry.parent = -1;
						entry.title = "Accept invite";
						entry.action = "accept:" + invite.inviterid + ":" + invite.key.str;
						entry.handle += TreeViewPopupInvite;
						entries[0] = entry;

						entry = new Interfaces.PopupEntry();
						entry.parent = -1;
						entry.title = "Decline invite";
						entry.action = "decline:" + invite.inviterid + ":" + invite.key.str;
						entry.handle += TreeViewPopupInvite;
						entries[1] = entry;

						uireactions.PopupMenuDo(parent, position, entries);
					}

					if (typeid.entryType == TypeIDTreeNode.EntryType.Rendezvous)
					{
						RendezvousTreeNode rendezvous = typeid as RendezvousTreeNode;
						if (rendezvous == null)
							return;

						Interfaces.PopupEntry[] entries = new Interfaces.PopupEntry[2];

						Interfaces.PopupEntry entry = new Interfaces.PopupEntry();
						entry.parent = -1;
						entry.title = "Publish again";
						entry.action = "publish:again";
						entry.handle += TreeViewPopupPublish;
						entries[0] = entry;

						entry = new Interfaces.PopupEntry();
						entry.parent = -1;
						entry.title = "Publish next";
						entry.action = "publish:next";
						entry.handle += TreeViewPopupPublish;
						entries[1] = entry;

						uireactions.PopupMenuDo(parent, position, entries);
					}
				}
			}
			else
			{
				// only popup here
				if (button != Interfaces.Button.Right)
					return;

				Interfaces.PopupEntry[] entries = new Interfaces.PopupEntry[3];

				Interfaces.PopupEntry entry = new Interfaces.PopupEntry();
				entry.parent = -1;
				entry.title = "new friend";
				entry.action = "new:friend";
				entry.handle += TreeViewPopupNew;
				entries[0] = entry;

				entry = new Interfaces.PopupEntry();
				entry.parent = -1;
				entry.title = "new group";
				entry.action = "new:group";
				entry.handle += TreeViewPopupNew;
				entries[1] = entry;

				entry = new Interfaces.PopupEntry();
				entry.parent = -1;
				entry.title = "new rendezvous";
				entry.action = "new:rendezvous";
				entry.handle += TreeViewPopupNew;
				entries[2] = entry;

				uireactions.PopupMenuDo(parent, position, entries);
			}
		}
	}
}
