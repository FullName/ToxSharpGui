
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpGui;

public partial class MainWindow /* : Gtk.Window, IToxSharpFriend, IToxSharpGroup */
{
	public ToxSharp toxsharp;

	public void ToxConnected(bool state)
	{
		checkbutton1.Active = state;
		checkbutton1.Label = state ? "Up" : "Down";

		int friendsonline, friendstotal;
		datastorage.FriendCount(out friendsonline, out friendstotal);

		if (friendstotal > 0)
			checkbutton1.Label += " | F: " + friendsonline + " on, " + friendstotal + " total";
	}

	public void ToxFriendAddRequest(ToxKey key, string message)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Stranger, key);
		if (typeid != null)
		{
			StrangerTreeNode strangerexisting = typeid as StrangerTreeNode;
			if (strangerexisting != null)
			{
				strangerexisting.message = message;
				treeview1.QueueDraw();
	
				TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Updated friend request: Message is [" + message + "]\n" +
														 "ID: " + strangerexisting.key.str);
				return;
			}
		}

		StrangerTreeNode strangernew = new StrangerTreeNode(key, message);
		HolderTreeNode holder = datastorage.HolderTreeNodeNew(strangernew);
		TreeAdd(holder);

		TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "New friend request: Message is [" + message + "]\n" +
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
		
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
		if (typeid == null)
			return;

		FriendTreeNode friend = typeid as FriendTreeNode;
		if (friend == null)
			return;

		if ((int)state < FriendPresenceStateToString.Capacity)
			friend.state = FriendPresenceStateToString[(int)state];
		else
			friend.state = FriendPresenceStateToString[3];

		treeview1.QueueDraw();
	}

	public void ToxFriendPresenceState(int id, string state)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
		if (typeid == null)
			return;

		FriendTreeNode friend = typeid as FriendTreeNode;
		if (friend == null)
			return;

		friend.state = state;
		treeview1.QueueDraw();
	}

	public void ToxFriendName(int id, string name)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
		if (typeid == null)
			return;

		FriendTreeNode friend = typeid as FriendTreeNode;
		if (friend == null)
			return;

		friend.name = name;
		treeview1.QueueDraw();
	}

	public void ToxFriendConnected(int id, bool connected)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
		if (typeid == null)
			return;

		FriendTreeNode friend = typeid as FriendTreeNode;
		if (friend == null)
			return;

		friend.online = connected;
		treeview1.QueueDraw();

		ToxConnected(checkbutton1.Active);
	}

	public void ToxFriendInit(int id, ToxKey key, string name, bool online, FriendPresenceState presence, string state)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
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

				treeview1.QueueDraw();
				return;
			}
		}

		FriendTreeNode friendnew = new FriendTreeNode((UInt16)id, key, name, online, presence, state);
		HolderTreeNode holder = datastorage.HolderTreeNodeNew(friendnew);
		TreeAdd(holder);
	}

	public void ToxFriendMessage(int id, string message)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
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

		TextAdd(Interfaces.SourceType.Friend, friend.id, handle, message);
	}

	public void ToxFriendAction(int id, string action)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
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

		TextAdd(Interfaces.SourceType.Friend, friend.id, "ACTION", handle + " " + action);
	}

/****************************************************************************************/
/****************************************************************************************/
/****************************************************************************************/

	public void ToxGroupchatInvite(int friendnumber, string friendname, ToxKey friend_groupkey)
	{
		TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Group chat invite received by friend [" + friendnumber + "] " + friendname + ":\n" + friend_groupkey.str);

		InvitationTreeNode invitation = new InvitationTreeNode(friend_groupkey, (UInt16)friendnumber, friendname);
		HolderTreeNode holder = datastorage.HolderTreeNodeNew(invitation);
		TreeAdd(holder);
	}

	public void ToxGroupchatMessage(int groupnumber, int friendgroupnumber, string message)
	{
		TextAdd(Interfaces.SourceType.Group, (UInt16)groupnumber, "#" + groupnumber + " - " + friendgroupnumber, message);
	}
}
