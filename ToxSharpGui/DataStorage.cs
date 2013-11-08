
using System;
using System.Collections.Generic;

namespace ToxSharpBasic
{
	public class TypeIDTreeNode
	{
		public enum EntryType { Header, Friend, Stranger, Group, Invitation };

		public EntryType entryType;
		public UInt16 id;

		public TypeIDTreeNode(EntryType entryType, UInt16 id)
		{
			this.entryType = entryType;
			this.id = id;
		}

		public virtual UInt16 Check()
		{
			return 0;
		}

		public virtual string Text()
		{
			return null;
		}

		public virtual string TooltipText()
		{
			return null;
		}
	}

	public class HeaderTreeNode : TypeIDTreeNode
	{
		public string title;
		
		public HeaderTreeNode(EntryType type) : base(EntryType.Header, (UInt16)type)
		{
			switch(type)
			{
				case EntryType.Friend:
					title = "Friends";
					break;
				case EntryType.Stranger:
					title = "Strangers";
					break;
				case EntryType.Group:
					title = "Groups";
					break;
				case EntryType.Invitation:
					title = "Invites";
					break;
				default:
					title = "???";
					break;
			}
		}

		public override string Text()
		{
			return title;
		}
	}

	public class KeyTreeNode : TypeIDTreeNode
	{
		public ToxKey key;

		public KeyTreeNode(EntryType entrytype, UInt16 id, ToxKey key) : base(entrytype, id)
		{
			this.key = key;
		}
	}

	public class FriendTreeNode : KeyTreeNode
	{
		public string name;
		public bool online;
		public FriendPresenceState presence;	// enum
		public string state;	// text

		public FriendTreeNode(UInt16 id, ToxKey key, string name, bool online, FriendPresenceState presence, string state) : base(EntryType.Friend, id, key)
		{
			this.name = name;
			this.online = online;
			this.presence = presence;
			this.state = state;
		}

		public override UInt16 Check()
		{
			return online ? (UInt16)2 : (UInt16)1;
		}

		public override string Text()
		{
			return name;
		}

		public override string TooltipText()
		{
			string res = name;
			if (state != null)
				res += " (" + state + ")";
			if (presence != FriendPresenceState.Invalid)
				res += " - " + FriendPresenceStateToString(presence);
			if (key != null)
				res += "\n[" + key.str + "]";

			return res;
		}

		protected static List<string> _FriendPresenceStateToString;

		public static string FriendPresenceStateToString(FriendPresenceState state)
		{
			if (_FriendPresenceStateToString == null)
			{
				_FriendPresenceStateToString = new List<string>(4);
				_FriendPresenceStateToString.Add("unknown");
				_FriendPresenceStateToString.Add("away");
				_FriendPresenceStateToString.Add("busy");
				_FriendPresenceStateToString.Add("invalid");
			}

			if ((int)state < _FriendPresenceStateToString.Capacity)
				return _FriendPresenceStateToString[(int)state];
			else
				return _FriendPresenceStateToString[3];
		}
	}

	public class StrangerTreeNode : KeyTreeNode
	{
		public string message;
		
		public StrangerTreeNode(ToxKey key, string message) : base(EntryType.Stranger, 0, key)
		{
			this.message = message;
		}

		public override string Text()
		{
			return "[" + key.str.Substring(0, 8) + "...]";
		}

		public override string TooltipText()
		{
			string res = "[" + key.str + "]";
			if ((message != null) && (message.Length > 0))
				res += "\nMessage" + message;

			return res;
		}
	}

	public class GroupTreeNode : KeyTreeNode
	{
		public string name;

		public GroupTreeNode(UInt16 id, ToxKey key, string name) : base(EntryType.Group, id, key)
		{
			this.name = name;
		}

		public override string Text()
		{
			string res = "[" + id + "]";
			if ((name != null) && (name.Length > 0))
				res += " " + name;

			return res;
		}
	}

	public class InvitationTreeNode : KeyTreeNode
	{
		public UInt16 inviterid;
		public string invitername;

		public InvitationTreeNode(ToxKey key, UInt16 inviterid, string invitername) : base(EntryType.Invitation, 0, key)
		{
			this.inviterid = inviterid;
			this.invitername = invitername;
		}

		public override string Text()
		{
			string res = "[" + inviterid + "]";
			if ((invitername != null) && (invitername.Length > 0))
				res += " " + invitername;

			return res;
		}
	}

	public class DataStorage : Interfaces.IDataReactions
	{
		protected class DataStorageSub
		{
		}
	
		protected class DataStorageSubKeyKey : DataStorageSub
		{
			public Dictionary<ToxKey, TypeIDTreeNode> element;
	
			public DataStorageSubKeyKey()
			{
				element = new Dictionary<ToxKey, TypeIDTreeNode>();
			}
		}
	
		protected class DataStorageSubKeyUInt16 : DataStorageSub
		{
			public Dictionary<UInt16, TypeIDTreeNode> element;
	
			public DataStorageSubKeyUInt16()
			{
				element = new Dictionary<ushort, TypeIDTreeNode>();
			}
		}

		// Dictionary: Type
		//    Dictionary ID | List
		protected Dictionary<TypeIDTreeNode.EntryType, DataStorageSub> data;

		public DataStorage()
		{
			data = new Dictionary<TypeIDTreeNode.EntryType, DataStorageSub>();

			data.Add(TypeIDTreeNode.EntryType.Friend, new DataStorageSubKeyUInt16());
			data.Add(TypeIDTreeNode.EntryType.Stranger, new DataStorageSubKeyKey());
			data.Add(TypeIDTreeNode.EntryType.Group, new DataStorageSubKeyUInt16());
			data.Add(TypeIDTreeNode.EntryType.Invitation, new DataStorageSubKeyKey());
		}

		public void Add(TypeIDTreeNode typeid)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(typeid.entryType, out sub))
				return;

			if ((typeid.entryType == TypeIDTreeNode.EntryType.Friend) ||
				(typeid.entryType == TypeIDTreeNode.EntryType.Group))
			{
				DataStorageSubKeyUInt16 subint = sub as DataStorageSubKeyUInt16;
				if (subint != null)
					subint.element.Add(typeid.id, typeid);
			}
			else if (typeid.entryType == TypeIDTreeNode.EntryType.Stranger)
			{
				DataStorageSubKeyKey subkey = sub as DataStorageSubKeyKey;
				StrangerTreeNode stranger = typeid as StrangerTreeNode;
				if ((subkey != null) && (stranger != null))
					subkey.element.Add(stranger.key, typeid);
			}
			else if (typeid.entryType == TypeIDTreeNode.EntryType.Invitation)
			{
				DataStorageSubKeyKey subkey = sub as DataStorageSubKeyKey;
				InvitationTreeNode invite = typeid as InvitationTreeNode;
				if ((subkey != null) && (invite != null))
					subkey.element.Add(invite.key, typeid);
			}
		}

		public void Delete(TypeIDTreeNode typeid)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(typeid.entryType, out sub))
				return;

			if ((typeid.entryType == TypeIDTreeNode.EntryType.Friend) ||
				(typeid.entryType == TypeIDTreeNode.EntryType.Group))
			{
				DataStorageSubKeyUInt16 subint = sub as DataStorageSubKeyUInt16;
				if (subint != null)
					subint.element.Remove(typeid.id);
			}
			else if (typeid.entryType == TypeIDTreeNode.EntryType.Stranger)
			{
				DataStorageSubKeyKey subkey = sub as DataStorageSubKeyKey;
				StrangerTreeNode stranger = typeid as StrangerTreeNode;
				if ((subkey != null) && (stranger != null))
					subkey.element.Remove(stranger.key);
			}
			else if (typeid.entryType == TypeIDTreeNode.EntryType.Invitation)
			{
				DataStorageSubKeyKey subkey = sub as DataStorageSubKeyKey;
				InvitationTreeNode invite = typeid as InvitationTreeNode;
				if ((subkey != null) && (invite != null))
					subkey.element.Remove(invite.key);
			}
		}

		public TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, UInt16 id)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(entrytype, out sub))
				return null;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return null;
			
			TypeIDTreeNode typeid = null;
			subactual.element.TryGetValue(id, out typeid);
			return typeid;
		}

		public TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, ToxKey key)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(entrytype, out sub))
				return null;

			DataStorageSubKeyKey subactual = sub as DataStorageSubKeyKey;
			if (subactual == null)
				return null;

			// does NOT work as advertised:
			// 'key' does have IEquatable, but it is never called.
			// probably only works for the trival case of a builtin type

			// TypeIDTreeNode typeid = null;
			// subactual.element.TryGetValue(key, out typeid);

			// do it manually
			foreach(KeyValuePair<ToxKey, TypeIDTreeNode> pair in subactual.element)
				if (pair.Key.Equals(key))
					return pair.Value;
					
			return null;
		}

		public int FindFriendsWithKeyStartingWithID(string keyfragment, out FriendTreeNode friend)
		{
			friend = null;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Friend, out sub))
				return -1;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return -1;

			int rc = 0;
			foreach(TypeIDTreeNode typeid in subactual.element.Values)
			{
				FriendTreeNode check = typeid as FriendTreeNode;
				if (check == null)
					continue;

				if (0 == string.Compare(check.key.str.Substring(0, keyfragment.Length), keyfragment, true))
				{
					rc++;
					friend = check;
				}
			}

			return rc;
		}

		public int FindFriendsWithNameOrKeyStartingWithID(string keyfragment, out FriendTreeNode friend)
		{
			friend = null;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Friend, out sub))
				return -1;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return -1;

			int rc = 0;
			foreach(TypeIDTreeNode typeid in subactual.element.Values)
			{
				FriendTreeNode check = typeid as FriendTreeNode;
				if (check == null)
					continue;

				if ((0 == string.Compare(check.key.str.Substring(0, keyfragment.Length), keyfragment, true)) ||
				    (0 == string.Compare(check.name.Substring(0, keyfragment.Length), keyfragment, true)))
				{
					rc++;
					friend = check;
				}
			}

			return rc;
		}

		public void FriendCount(out int online, out int total)
		{
			online = 0;
			total = 0;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Friend, out sub))
				return;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return;

			foreach(TypeIDTreeNode typeid in subactual.element.Values)
			{
				FriendTreeNode friend = typeid as FriendTreeNode;
				if (friend != null)
				{
					total++;
					if (friend.online)
						online++;
				}
			}
		}

		public bool GroupEnumerator(out Dictionary<UInt16, TypeIDTreeNode> groups)
		{
			groups = null;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Group, out sub))
				return false;

			DataStorageSubKeyUInt16 subactual = sub as DataStorageSubKeyUInt16;
			if (subactual == null)
				return false;

			groups = subactual.element;
			return true;
		}
	}
}
