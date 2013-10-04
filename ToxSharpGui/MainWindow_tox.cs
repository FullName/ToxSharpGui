
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

		int friendsonline = 0, friendstotal = 0;
		if (!store.IterHasChild(_frienditer))
			return;

		TreeIter iter;
		store.IterChildren(out iter, _frienditer);
		do
		{
			PersonTreeNode friend = store.GetValue(iter, 0) as PersonTreeNode;
			if (friend.entryType != PersonTreeNode.EntryType.Friend)
				break;

			friendstotal++;
			if (friend.online)
				friendsonline++;
		} while (store.IterNext(ref iter));

		if (friendstotal > 0)
			checkbutton1.Label += " | F: " + friendsonline + " on, " + friendstotal + " total";
	}

	public void ToxFriendRequest(string key, string message)
	{
		TextAdd(SourceType.System, 0, "SYSTEM", "Friend request: Message is [" + message + "]\nID: " + key);
		if (store.IterHasChild(_strangeriter))
		{
			TreeIter iter;
			store.IterChildren(out iter, _strangeriter);
			do
			{
				PersonTreeNode stranger = store.GetValue(iter, 0) as PersonTreeNode;
				if (stranger.entryType != PersonTreeNode.EntryType.Stranger)
					break;
	
				if (stranger.key == key)
				{
					stranger.message = message;
					treeview1.QueueDraw();
					return;
				}
			} while (store.IterNext(ref iter));
		}

		store.AppendValues(strangeriter, new PersonTreeNode(key, message));
		treeview1.ExpandAll();
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
		
		if (!store.IterHasChild(_frienditer))
			return;

		TreeIter iter;
		store.IterChildren(out iter, _frienditer);
		do
		{
			PersonTreeNode friend = store.GetValue(iter, 0) as PersonTreeNode;
			if (friend.entryType != PersonTreeNode.EntryType.Friend)
				break;

			if (friend.id == id)
			{
				if ((int)state < FriendPresenceStateToString.Capacity)
					friend.state = FriendPresenceStateToString[(int)state];
				else
					friend.state = FriendPresenceStateToString[3];

				treeview1.QueueDraw();
				return;
			}
		} while (store.IterNext(ref iter));
	}

	public void ToxFriendPresenceState(int id, string state)
	{
		if (!store.IterHasChild(_frienditer))
			return;

		TreeIter iter;
		store.IterChildren(out iter, _frienditer);
		do
		{
			PersonTreeNode friend = store.GetValue(iter, 0) as PersonTreeNode;
			if (friend.entryType != PersonTreeNode.EntryType.Friend)
				break;

			if (friend.id == id)
			{
				friend.state = state;
				treeview1.QueueDraw();
				return;
			}
		} while (store.IterNext(ref iter));
	}

	public void ToxFriendName(int id, string name)
	{
		if (!store.IterHasChild(_frienditer))
			return;

		TreeIter iter;
		store.IterChildren(out iter, _frienditer);
		do
		{
			PersonTreeNode friend = store.GetValue(iter, 0) as PersonTreeNode;
			if (friend.entryType != PersonTreeNode.EntryType.Friend)
				break;

			if (friend.id == id)
			{
				friend.name = name;
				treeview1.QueueDraw();
				return;
			}
		} while (store.IterNext(ref iter));
	}

	public void ToxFriendConnected(int id, bool connected)
	{
		if (!store.IterHasChild(_frienditer))
			return;

		TreeIter iter;
		store.IterChildren(out iter, _frienditer);
		do
		{
			PersonTreeNode friend = store.GetValue(iter, 0) as PersonTreeNode;
			if (friend.entryType != PersonTreeNode.EntryType.Friend)
				break;

			if (friend.id == id)
			{
				friend.online = connected;
				treeview1.QueueDraw();
				break;
			}
		} while (store.IterNext(ref iter));

		ToxConnected(checkbutton1.Active);
	}

	public void ToxFriendInit(int id, string name, string state, bool online)
	{
		if (store.IterHasChild(_frienditer))
		{
			TreeIter iter;
			store.IterChildren(out iter, _frienditer);
			do
			{
				PersonTreeNode friend = store.GetValue(iter, 0) as PersonTreeNode;
				if (friend.entryType != PersonTreeNode.EntryType.Friend)
					break;
	
				if (friend.id == id)
				{
					friend.name = name;
					friend.state = state;
					friend.online = online;

					treeview1.QueueDraw();
					return;
				}
			} while (store.IterNext(ref iter));
		}

		store.AppendValues(frienditer, new PersonTreeNode(id, name, state, online));
		treeview1.ExpandAll();
	}

	public void ToxFriendMessage(int friendId, string message)
	{
		if (!store.IterHasChild(_frienditer))
			return;

		TreeIter iter;
		store.IterChildren(out iter, _frienditer);
		do
		{
			PersonTreeNode friend = store.GetValue(iter, 0) as PersonTreeNode;
			if (friend.entryType != PersonTreeNode.EntryType.Friend)
				break;

			if (friend.id == friendId)
			{
				TextAdd(SourceType.Friend, (UInt16)friendId, friend.name, message);
				return;
			}
		} while (store.IterNext(ref iter));
	}
}
