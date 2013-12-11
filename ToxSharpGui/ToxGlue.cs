
using System;
using System.Drawing;
using System.Collections.Generic;

namespace ToxSharpBasic
{
	// common code for InputHandling and Popups, essentially doing
	// the same operation with the same arguments on two different paths
	internal interface IToxGlue
	{
		int RendezvousCreateOrUpdate(string text, DateTime time);
		bool GroupchatAdd();
		bool GroupchatInviteaccept(UInt16 invitenumber);
		bool GroupchatMessage(UInt16 groupnumber, string message);
	}

	internal class ToxGlue : IToxSharpBasic, IToxSharpFriend, IToxSharpGroup, IToxSharpRendezvous,
	                       Interfaces.IUIActions, IToxGlue
	{
		public int RendezvousCreateOrUpdate(string text, DateTime time)
		{
			RendezvousTreeNode rendezvous = datareactions.FindRendezvous(text);
			if (rendezvous == null)
			{
				rendezvous = new RendezvousTreeNode(text, time);
				uireactions.TreeAdd(rendezvous);
				datareactions.Add(rendezvous);
			}
			else
			{
				if (time == rendezvous.time)
				{
					uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Identical rendezvous already in place.");
					return 0;
				}

				rendezvous.time = time;
			}

			int res = toxsharp.ToxPublish(rendezvous.ids(), text, time);
			if (res > 0)
			{
				uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Set up successfully.\n");
				rendezvous.current = true;
				uireactions.TreeUpdate(rendezvous);
			}
			else if (res == 0)
				uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Failed to set up due to invalid input.\n");
			else if (res < 0)
			{
				if (res == -2)
					uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Failed to set up, function missing.\n");
				else if (res == -3)
					uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Oops. Invalid ID.\n");
				else if (res == -4)
					uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Can't set up, different rendezvous already in progress.\n");
				else
					uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Failed to set up for unknown reason.\n");
			}

			return res;
		}

		public bool GroupchatAdd()
		{
			int groupnumber;
			if (toxsharp.ToxGroupchatAdd(out groupnumber))
			{
				GroupTreeNode groupchat = new GroupTreeNode((UInt16)groupnumber, null, null);
				datareactions.Add(groupchat);
				uireactions.TreeAdd(groupchat);
				uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Created new group chat " + groupnumber + ".");
				return true;
			}
			else
			{
				uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Failed to create new group chat.");
				return false;
			}
		}

		public bool GroupchatMessage(UInt16 groupnumber, string message)
		{
			if (toxsharp.ToxGroupchatMessage(groupnumber, message))
			{
				uireactions.TextAdd(Interfaces.SourceType.Group, groupnumber, toxsharp.ToxNameGet() + " => #" + groupnumber, message);
				return true;
			}
			else
			{
				uireactions.TextAdd(Interfaces.SourceType.Group, groupnumber, "SYSTEM", "Failed to send message to group.");
				return false;
			}
		}

		public bool GroupchatInviteaccept(UInt16 invitenumber)
		{
			uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Groupchat: Accept invitation: Not yet implemented.\n");
			return false;
		}

		public void PrintDebug(string line)
		{
			MainClass.PrintDebug(line);
		}

		protected Popups popups;

		public void TreePopup(object parent, Point position, TypeIDTreeNode typeid, Interfaces.Button button, Interfaces.Click click)
		{
			if (popups == null)
			{
				popups = new Popups(toxsharp, uireactions, datareactions, this);
				if (popups == null)
				    return;
			}

			popups.TreePopup(parent, position, typeid, button, click);
		}

		protected InputHandling inputhandling;

		public bool InputLine(string text, Interfaces.InputKey key)
		{
			if (inputhandling == null)
			{
				inputhandling = new InputHandling(toxsharp, uireactions, datareactions, this);
				if (inputhandling == null)
					return false;
			}

			return inputhandling.Do(text, key);
		}

		public void QuitPrepare(string uistate)
		{
			toxsharp.ToxStopAndSave();

			string configdir = toxsharp.ToxConfigHome;
			if (System.IO.Directory.Exists(configdir))
			{
				try
				{
					string uistatename = configdir + System.IO.Path.DirectorySeparatorChar + "state.ui";
					System.IO.StreamWriter stream = new System.IO.StreamWriter(uistatename, false);
					stream.Write(uistate);
					stream.Close();
				}
				catch (Exception e)
				{
					System.Console.WriteLine("ToxSharpGui: Failed to write ui state file: >>" + e.Message + "\n<<\n");
				}
			}
		}

		protected ToxInterface toxsharp = null;
		protected Interfaces.IUIReactions uireactions = null;
		protected Interfaces.IDataReactions datareactions = null;

		public ToxGlue()
		{
		}

		public void Init(ToxInterface toxsharp, Interfaces.IUIReactions uireactions, Interfaces.IDataReactions datareactions)
		{
			this.toxsharp = toxsharp;
			this.uireactions = uireactions;
			this.datareactions = datareactions;
		}

		protected bool connected = false;

		public void ToxDo(Interfaces.CallToxDo calltoxdo, IntPtr tox)
		{
			if (uireactions != null)
				uireactions.ToxDo(calltoxdo, tox);
		}

		public void ToxConnected(bool state)
		{
			connected = state;

			string text = state ? "Up" : "Down";

			int friendsonline, friendstotal;
			datareactions.FriendCount(out friendsonline, out friendstotal);

			if (friendstotal > 0)
				text += " | F: " + friendsonline + " on, " + friendstotal + " total";

			uireactions.ConnectState(state, text);
		}

		public void ToxFriendAddRequest(ToxKey key, string message)
		{
			TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Stranger, key);
			if (typeid != null)
			{
				StrangerTreeNode strangerexisting = typeid as StrangerTreeNode;
				if (strangerexisting != null)
				{
					strangerexisting.message = message;
					uireactions.TreeUpdate(strangerexisting);
		
					uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Updated friend request: Message is [" + message + "]\n" +
															 "ID: " + strangerexisting.key.str);
					return;
				}
			}

			StrangerTreeNode strangernew = new StrangerTreeNode(key, message);
			datareactions.Add(strangernew);
			uireactions.TreeAdd(strangernew);

			string keysplit = strangernew.key.str.Substring( 0, 19) + " "
				            + strangernew.key.str.Substring(19, 19) + " "
				            + strangernew.key.str.Substring(38, 19) + " "
				            + strangernew.key.str.Substring(57, 19);
			uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "New friend request: Message is [" + message + "]\n" +
													 "ID: " + keysplit);
		}
			
		protected List<string> FriendPresenceStateToString;

		public void ToxFriendPresenceState(int id, FriendPresenceState state)
		{
			if (FriendPresenceStateToString == null)
			{
				FriendPresenceStateToString = new List<string>(4);
				FriendPresenceStateToString.Add("unknown");
				FriendPresenceStateToString.Add("away");
				FriendPresenceStateToString.Add("busy");
				FriendPresenceStateToString.Add("invalid");
			}
			
			TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
			if (typeid == null)
				return;

			FriendTreeNode friend = typeid as FriendTreeNode;
			if (friend == null)
				return;

			if ((int)state < FriendPresenceStateToString.Capacity)
				friend.state = FriendPresenceStateToString[(int)state];
			else
				friend.state = FriendPresenceStateToString[3];

			uireactions.TreeUpdate(typeid);
		}

		public void ToxFriendPresenceState(int id, string state)
		{
			TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
			if (typeid == null)
				return;

			FriendTreeNode friend = typeid as FriendTreeNode;
			if (friend == null)
				return;

			friend.state = state;
			uireactions.TreeUpdate(typeid);
		}

		public void ToxFriendName(int id, string name)
		{
			TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
			if (typeid == null)
				return;

			FriendTreeNode friend = typeid as FriendTreeNode;
			if (friend == null)
				return;

			friend.NameUpdate(name);
			uireactions.TreeUpdate(typeid);
		}

		public void ToxFriendConnected(int id, bool connected)
		{
			TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
			if (typeid == null)
				return;

			FriendTreeNode friend = typeid as FriendTreeNode;
			if (friend == null)
				return;

			friend.online = connected;
			uireactions.TreeUpdate(typeid);

			ToxConnected(connected);
		}

		public void ToxFriendInit(int id, ToxKey key, string name, bool online, FriendPresenceState presence, string state)
		{
			TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
			if (typeid != null)
			{
				FriendTreeNode friendexisting = typeid as FriendTreeNode;
				if (friendexisting != null)
				{
					friendexisting.NameUpdate(name);
					friendexisting.key = key;
					friendexisting.online = online;
					friendexisting.presence = presence;
					friendexisting.state = state;

					uireactions.TreeUpdate(typeid);
					return;
				}
			}

			FriendTreeNode friendnew = new FriendTreeNode((UInt16)id, key, name, online, presence, state);
			datareactions.Add(friendnew);
			uireactions.TreeAdd(friendnew);
		}

		public void ToxFriendMessage(int id, string message)
		{
			TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
			if (typeid == null)
				return;

			FriendTreeNode friend = typeid as FriendTreeNode;
			if (friend == null)
				return;

			string handle = "[" + friend.id + "] ";
			if (friend.name.Length > 0)
				handle += friend.name;
			else
				handle += friend.key.str.Substring(0, 8) + "...";

			uireactions.TextAdd(Interfaces.SourceType.Friend, friend.id, handle, message);
		}

		public void ToxFriendAction(int id, string action)
		{
			TypeIDTreeNode typeid = datareactions.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
			if (typeid == null)
				return;

			FriendTreeNode friend = typeid as FriendTreeNode;
			if (friend == null)
				return;

			string handle = "[" + friend.id + "] ";
			if (friend.name.Length > 0)
				handle += friend.name;
			else
				handle += friend.key.str.Substring(0, 8) + "...";

			uireactions.TextAdd(Interfaces.SourceType.Friend, friend.id, "ACTION", handle + " " + action);
		}

	/****************************************************************************************/
	/****************************************************************************************/
	/****************************************************************************************/

		public void ToxGroupchatInit(UInt16 groupchatnum)
		{
			GroupTreeNode group = new GroupTreeNode(groupchatnum, null, null);
			datareactions.Add(group);
			uireactions.TreeAdd(group);
		}

		public void ToxGroupchatInvite(int friendnumber, string friendname, ToxKey friend_groupkey)
		{
			uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Group chat invite received by friend [" + friendnumber + "] " + friendname + ":\n" + friend_groupkey.str);

			InvitationTreeNode invitation = new InvitationTreeNode(friend_groupkey, (UInt16)friendnumber, friendname);
			datareactions.Add(invitation);
			uireactions.TreeAdd(invitation);
		}

		public void ToxGroupchatMessage(int groupnumber, int friendgroupnumber, string message)
		{
			string name = "" + friendgroupnumber;
			GroupTreeNode group = datareactions.Find(TypeIDTreeNode.EntryType.Group, (UInt32)groupnumber) as GroupTreeNode;
			if (group != null)
			{
				GroupMemberTreeNode member = datareactions.Find(TypeIDTreeNode.EntryType.GroupMember, group.MemberID((UInt16)friendgroupnumber)) as GroupMemberTreeNode;
				if (member != null)
				{
					name = member.name;
					if (name.Length == 0)
					{
						toxsharp.ToxGroupchatPeername((UInt16)groupnumber, (UInt16)friendgroupnumber, out name);
						if (name.Length > 0)
						{
							member.name = name;
							uireactions.TreeUpdateSub(member, group);
						}
					}
				}
			}

			uireactions.TextAdd(Interfaces.SourceType.Group, (UInt16)groupnumber, "#" + groupnumber + " - <" + name + ">", message);
		}

		public void ToxGroupNamelistChange(int _group, int _peer, ToxGroupNamelistChangeType change)
		{
			// uireactions.TextAdd(Interfaces.SourceType.Debug, 0, "DEBUG", "Group::Namelist::Change(" + _group + "." + _peer + ", " + (uint)change + ")");
			UInt16 group, peer;
			try
			{
				group = (UInt16)_group;
				peer = (UInt16)_peer;
			}
			catch
			{
				return;
			}

			GroupTreeNode groupnode = datareactions.Find(TypeIDTreeNode.EntryType.Group, group) as GroupTreeNode;
			if (groupnode == null)
				return;

			GroupMemberTreeNode membernode;
			switch(change)
			{
				case ToxGroupNamelistChangeType.PeerAdded:
					membernode = datareactions.Find(TypeIDTreeNode.EntryType.GroupMember, groupnode.MemberID(peer)) as GroupMemberTreeNode;
					if (membernode == null)
					{
						membernode = new GroupMemberTreeNode(groupnode, peer, null);
						datareactions.Add(membernode);
						uireactions.TreeAddSub(membernode, groupnode);
						uireactions.TextAdd(Interfaces.SourceType.Group, group, "GROUP", "Peer " + peer + " joined.");
					}

					break;

				case ToxGroupNamelistChangeType.PeerRemoved:
					membernode = datareactions.Find(TypeIDTreeNode.EntryType.GroupMember, groupnode.MemberID(peer)) as GroupMemberTreeNode;
					if (membernode != null)
					{
						uireactions.TreeDelSub(membernode, groupnode);
						datareactions.Delete(membernode);
						uireactions.TextAdd(Interfaces.SourceType.Group, group, "GROUP", "Peer " + peer + " \"" + membernode.Text() + "\" left.");
					}

					break;

				case ToxGroupNamelistChangeType.PeerNamechange:
					membernode =  datareactions.Find(TypeIDTreeNode.EntryType.GroupMember, groupnode.MemberID(peer)) as GroupMemberTreeNode;
					if (membernode != null)
					{
						string namestr;
						if (toxsharp.ToxGroupchatPeername(group, peer, out namestr))
						{
							membernode.name = namestr;
							uireactions.TreeUpdateSub(membernode, groupnode);
							uireactions.TextAdd(Interfaces.SourceType.Group, group, "GROUP", "Peer " + peer + " changed their name to: \"" + membernode.Text() + "\"");
						}
					}

					break;
			}
		}

	/****************************************************************************************/
	/****************************************************************************************/
	/****************************************************************************************/

		public void ToxRendezvousFound(ushort ID, ToxKey friendaddress)
		{
			uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Match! [" + friendaddress.str + "]");
		}

		public byte ToxRendezvousTimeout(ushort ID)
		{
			uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Timeout.");
			RendezvousTreeNode rendezvous = datareactions.Find(TypeIDTreeNode.EntryType.Rendezvous, ID) as RendezvousTreeNode;
			if (rendezvous != null)
			{
				rendezvous.current = false;
				uireactions.TreeUpdate(rendezvous);
			}

			return 0;
		}
	}
}
