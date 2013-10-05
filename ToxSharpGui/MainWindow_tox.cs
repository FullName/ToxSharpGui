
using System;
using System.Collections.Generic;
using Gtk;

using ToxSharpGui;

public partial class MainWindow /* : Gtk.Window, IToxSharpFriend, IToxSharpGroup */
{
	public ToxSharp toxsharp;

	public void ToxConnected(bool state)
	{
		if (state)
		{
			string name = toxsharp.ToxName();
			string selfid = toxsharp.ToxSelfID();
			if (selfid != "")
				Title = "Tox# - " + name + " [" + selfid.Substring(0, 12) + "]";
			else
				Title = "Tox# - " + name + " [?]";
		}

		checkbutton1.Active = state;
		checkbutton1.Label = state ? "Up" : "Down";

		int friendsonline, friendstotal;
		datastorage.FriendCount(out friendsonline, out friendstotal);

		if (friendstotal > 0)
			checkbutton1.Label += " | F: " + friendsonline + " on, " + friendstotal + " total";
	}

	public void ToxFriendAddRequest(ToxSharpGui.Key key, string message)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Stranger, key);
		if (typeid != null)
		{
			StrangerTreeNode strangerexisting = typeid as StrangerTreeNode;
			if (strangerexisting != null)
			{
				strangerexisting.message = message;
				treeview1.QueueDraw();
	
				TextAdd(SourceType.System, 0, "SYSTEM", "Updated friend request: Message is [" + message + "]\n" +
														"ID: " + strangerexisting.key.str);
				return;
			}
		}

		StrangerTreeNode strangernew = new StrangerTreeNode(key, message);
		store.AppendValues(storeiterators.strangeriter, HolderTreeNodeNew(strangernew));
		treeview1.ExpandAll();

		TextAdd(SourceType.System, 0, "SYSTEM", "New friend request: Message is [" + message + "]\n" +
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

	public void ToxFriendInit(int id, ToxSharpGui.Key key, string name, string state, bool online)
	{
		TypeIDTreeNode typeid = datastorage.Find(TypeIDTreeNode.EntryType.Friend, (UInt16)id);
		if (typeid != null)
		{
			FriendTreeNode friend = typeid as FriendTreeNode;
			if (friend != null)
			{
				friend.key = key;
				friend.name = name;
				friend.state = state;
				friend.online = online;

				treeview1.QueueDraw();
				return;
			}
		}

		store.AppendValues(storeiterators.frienditer, HolderTreeNodeNew(new FriendTreeNode((UInt16)id, key, name, state, online)));
		treeview1.ExpandAll();
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

		TextAdd(SourceType.Friend, friend.id, handle, message);
	}
}
