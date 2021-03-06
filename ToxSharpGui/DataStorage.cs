
using System;
using System.Collections.Generic;

namespace ToxSharpBasic
{
	internal class KeyTreeNode : TypeIDTreeNode
	{
		public ToxKey key;

		public KeyTreeNode(EntryType entrytype, UInt16 id, ToxKey key) : base(entrytype, id)
		{
			this.key = key;
		}
	}

	internal class FriendTreeNode : KeyTreeNode
	{
		protected string _name;
		public string name { get { return _name; } }
		public bool online;
		public FriendPresenceState presence;	// enum
		public string state;	// text

		public FriendTreeNode(UInt16 id, ToxKey key, string name, bool online, FriendPresenceState presence, string state) : base(EntryType.Friend, id, key)
		{
			if (name != null)
				_name = name;
			else
				_name = "";

			this.online = online;
			this.presence = presence;
			this.state = state;
		}

		public void NameUpdate(string name)
		{
			if (name != null)
				_name = name;
		}

		public override UInt16 Check()
		{
			return online ? (UInt16)2 : (UInt16)1;
		}

		public override string Text()
		{
			return _name;
		}

		public override string TooltipText()
		{
			string res = _name;
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

	internal class StrangerTreeNode : KeyTreeNode
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

	internal class GroupTreeNode : KeyTreeNode
	{
		protected string _name;
		public string name { get { return _name; } }

		public GroupTreeNode(UInt16 id, ToxKey key, string name) : base(EntryType.Group, id, key)
		{
			if (name != null)
				_name = name;
			else
				_name = "";
		}

		public override string Text()
		{
			string res = "[" + id + "]";
			if (_name.Length > 0)
				res += " " + _name;

			return res;
		}

		public UInt32 MemberID(UInt16 subid)
		{
			return (id << 16) + subid;
		}
	}

	internal class InvitationTreeNode : KeyTreeNode
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

	internal class RendezvousTreeNode : TypeIDTreeNode
	{
		protected static ushort unique = 1;

		public string   text;
		public DateTime time;
		public bool current;

		public RendezvousTreeNode(string text, DateTime time) : base(EntryType.Rendezvous, unique)
		{
			unique++;
			if (unique == ushort.MaxValue)
				unique = 1;

			this.text = text;
			this.time = time;
			this.current = false;
		}

		public override ushort Check()
		{
			return current ? (UInt16)2 : (UInt16)1;
		}

		public override string Text()
		{
			return "[" + time.ToShortTimeString() + "] " + text.Substring(0, 16) + "...";
		}

		public override string TooltipText()
		{
			return "[" + time.ToShortDateString() + " " + time.ToShortTimeString() + "]\n"
					+ text.Substring(0, 16) + "...";
		}
	}

	internal class GroupMemberTreeNode : TypeIDTreeNode
	{
		public GroupTreeNode group;
		public UInt16 subid;
		protected string _name;
		public string name
		{
			get { return _name; }
			set
			{
				if (value == null)
					_name = "";
				else
					_name = value;
			}
		}

		public GroupMemberTreeNode(GroupTreeNode group, UInt16 peer, string name) : base(EntryType.GroupMember, group.MemberID(peer))
		{
			this.group = group;
			this.subid = peer;
			this.name = name;
		}

		public override string Text()
		{
			return "[" + subid + "] " + name;
		}
	}

	internal class DataStorage : Interfaces.IDataReactions
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
	
		protected class DataStorageSubKeyUInt32 : DataStorageSub
		{
			public Dictionary<UInt32, TypeIDTreeNode> element;
	
			public DataStorageSubKeyUInt32()
			{
				element = new Dictionary<UInt32, TypeIDTreeNode>();
			}
		}

		// Dictionary: Type
		//    Dictionary ID | List
		protected Dictionary<TypeIDTreeNode.EntryType, DataStorageSub> data;

		public DataStorage()
		{
			data = new Dictionary<TypeIDTreeNode.EntryType, DataStorageSub>();

			data.Add(TypeIDTreeNode.EntryType.Friend, new DataStorageSubKeyUInt32());
			data.Add(TypeIDTreeNode.EntryType.Stranger, new DataStorageSubKeyKey());
			data.Add(TypeIDTreeNode.EntryType.Rendezvous, new DataStorageSubKeyUInt32());

			data.Add(TypeIDTreeNode.EntryType.Group, new DataStorageSubKeyUInt32());
			data.Add(TypeIDTreeNode.EntryType.Invitation, new DataStorageSubKeyKey());
			data.Add(TypeIDTreeNode.EntryType.GroupMember, new DataStorageSubKeyUInt32());
		}

		public void Add(TypeIDTreeNode typeid)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(typeid.entryType, out sub))
				return;

			switch(typeid.entryType)
			{
				case TypeIDTreeNode.EntryType.Friend:
				case TypeIDTreeNode.EntryType.Group:
				case TypeIDTreeNode.EntryType.Rendezvous:
				case TypeIDTreeNode.EntryType.GroupMember:
					DataStorageSubKeyUInt32 subint = sub as DataStorageSubKeyUInt32;
					if (subint != null)
						subint.element.Add(typeid.id, typeid);
					break;

				case TypeIDTreeNode.EntryType.Stranger:
				case TypeIDTreeNode.EntryType.Invitation:
					DataStorageSubKeyKey subkey = sub as DataStorageSubKeyKey;
					KeyTreeNode keynode = typeid as KeyTreeNode;
					if ((subkey != null) && (keynode != null))
						subkey.element.Add(keynode.key, typeid);
					break;
			}
		}

		public void Delete(TypeIDTreeNode typeid)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(typeid.entryType, out sub))
				return;

			switch(typeid.entryType)
			{
				case TypeIDTreeNode.EntryType.Friend:
				case TypeIDTreeNode.EntryType.Group:
				case TypeIDTreeNode.EntryType.GroupMember:
					DataStorageSubKeyUInt32 subint = sub as DataStorageSubKeyUInt32;
					if (subint != null)
						subint.element.Remove(typeid.id);
					break;

				case TypeIDTreeNode.EntryType.Stranger:
				case TypeIDTreeNode.EntryType.Invitation:
					DataStorageSubKeyKey subkey = sub as DataStorageSubKeyKey;
					KeyTreeNode keynode = typeid as KeyTreeNode;
					if ((subkey != null) && (keynode != null))
						subkey.element.Remove(keynode.key);
					break;
			}
		}

		public TypeIDTreeNode Find(TypeIDTreeNode.EntryType entrytype, UInt32 id)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(entrytype, out sub))
				return null;

			DataStorageSubKeyUInt32 subactual = sub as DataStorageSubKeyUInt32;
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

			DataStorageSubKeyUInt32 subactual = sub as DataStorageSubKeyUInt32;
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

			DataStorageSubKeyUInt32 subactual = sub as DataStorageSubKeyUInt32;
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

		public RendezvousTreeNode FindRendezvous(string text)
		{
			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Rendezvous, out sub))
				return null;

			DataStorageSubKeyUInt32 subactual = sub as DataStorageSubKeyUInt32;
			if (subactual == null)
				return null;

			foreach(TypeIDTreeNode typeid in subactual.element.Values)
			{
				RendezvousTreeNode rendezvous = typeid as RendezvousTreeNode;
				if (rendezvous.text == text)
					return rendezvous;
			}

			return null;
		}

		public void FriendCount(out int online, out int total)
		{
			online = 0;
			total = 0;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Friend, out sub))
				return;

			DataStorageSubKeyUInt32 subactual = sub as DataStorageSubKeyUInt32;
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

		public bool GroupEnumerator(out Dictionary<UInt32, TypeIDTreeNode> groups)
		{
			groups = null;

			DataStorageSub sub;
			if (!data.TryGetValue(TypeIDTreeNode.EntryType.Group, out sub))
				return false;

			DataStorageSubKeyUInt32 subactual = sub as DataStorageSubKeyUInt32;
			if (subactual == null)
				return false;

			groups = subactual.element;
			return true;
		}
	}
}
