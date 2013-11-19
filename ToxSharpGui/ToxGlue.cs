
using System;
using System.Collections.Generic;

namespace ToxSharpBasic
{
	public class ToxGlue : IToxSharpBasic, IToxSharpFriend, IToxSharpGroup, IToxSharpRendezvous
	{
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

			uireactions.TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "New friend request: Message is [" + message + "]\n" +
													 "ID: " + strangernew.key.str);
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

			friend.name = name;
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
					friendexisting.key = key;
					friendexisting.name = name;
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
