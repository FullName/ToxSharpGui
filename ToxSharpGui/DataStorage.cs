
using System;
using System.Collections.Generic;
using Gtk;

namespace ToxSharpGui
{
	public class DataStorage
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

		protected Gtk.TreeStore store;
		protected StoreIterators storeiterators;

		// Dictionary: Type
		//    Dictionary ID | List
		protected Dictionary<TypeIDTreeNode.EntryType, DataStorageSub> data;

		public DataStorage(Gtk.TreeStore store, StoreIterators storeiterators)
		{
			this.store = store;
			this.storeiterators = storeiterators;

			data = new Dictionary<TypeIDTreeNode.EntryType, DataStorageSub>();

			data.Add(TypeIDTreeNode.EntryType.Friend, new DataStorageSubKeyUInt16());
			data.Add(TypeIDTreeNode.EntryType.Stranger, new DataStorageSubKeyKey());
			data.Add(TypeIDTreeNode.EntryType.Group, new DataStorageSubKeyUInt16());
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
		}

		public void Del(TypeIDTreeNode typeid)
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

		public HolderTreeNode HolderTreeNodeNew(TypeIDTreeNode typeid)
		{
			Add(typeid);
			return HolderTreeNode.Create(typeid);
		}

		public void StoreDelete(TypeIDTreeNode typeid)
		{
			TreeIter parent;
			if (!storeiterators.GetByTypeRaw(typeid.entryType, out parent))
				return;

			int num = store.IterNChildren(parent);
			for(int i = 0; i < num; i++)
			{
				TreeIter iter;
				if (store.IterNthChild(out iter, parent, i))
				{
					HolderTreeNode holder = store.GetValue(iter, 0) as HolderTreeNode;
					if (holder != null)
					{
						if (holder.typeid == typeid)
						{
							store.Remove(ref iter);
							Del(typeid);
							break;
						}
					}
				}
			}

			if (!store.IterHasChild(parent))
			{
				store.Remove(ref parent);
				storeiterators.SetByTypeRaw(typeid.entryType, Gtk.TreeIter.Zero);
			}
		}
	}

	public class HolderTreeNode : Gtk.TreeNode
	{
		public TypeIDTreeNode typeid;

		protected HolderTreeNode(TypeIDTreeNode typeid)
		{
			this.typeid = typeid;
		}

		public static HolderTreeNode Create(TypeIDTreeNode typeid)
		{
			return new HolderTreeNode(typeid);
		}

		public static HolderTreeNode HeaderNew(string text)
		{
			TypeIDTreeNode typeid = new HeaderTreeNode(text);
			return HolderTreeNode.Create(typeid);
		}
	}

	public class TypeIDTreeNode
	{
		public enum EntryType { Header, Friend, Stranger, Group };

		public EntryType entryType;
		public UInt16 id;

		public TypeIDTreeNode(EntryType entryType, UInt16 id)
		{
			this.entryType = entryType;
			this.id = id;
		}

	}

	public class HeaderTreeNode : TypeIDTreeNode
	{
		public string title;
		
		public HeaderTreeNode(string title) : base(EntryType.Header, 0)
		{
			this.title = title;
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
	}

	public class StrangerTreeNode : KeyTreeNode
	{
		public string message;
		
		public StrangerTreeNode(ToxKey key, string message) : base(EntryType.Stranger, 0, key)
		{
			this.message = message;
		}
	}

	public class GroupTreeNode : KeyTreeNode
	{
		public string name;

		public GroupTreeNode(UInt16 id, ToxKey key, string name) : base(EntryType.Group, id, key)
		{
			this.name = name;
		}
	}

	public class StoreIterators
	{
		protected Gtk.TreeStore store;

		public StoreIterators(Gtk.TreeStore store)
		{
			this.store = store;
		}

		public bool GetByTypeRaw(TypeIDTreeNode.EntryType type, out Gtk.TreeIter iter)
		{
			switch(type)
			{
				case TypeIDTreeNode.EntryType.Friend:
					iter = _frienditer;
					break;
					
				case TypeIDTreeNode.EntryType.Stranger:
					iter = _strangeriter;
					break;

				case TypeIDTreeNode.EntryType.Group:
					iter = _groupiter;
					break;
			}

			return !iter.Equals(Gtk.TreeIter.Zero);
		}

		public void SetByTypeRaw(TypeIDTreeNode.EntryType type, Gtk.TreeIter iter)
		{
			if (type == TypeIDTreeNode.EntryType.Friend)
				_frienditer = iter;
			if (type == TypeIDTreeNode.EntryType.Stranger)
				_strangeriter = iter;
			if (type == TypeIDTreeNode.EntryType.Group)
				_groupiter = iter;
		}

		protected Gtk.TreeIter _frienditer;
		protected Gtk.TreeIter _strangeriter;
		protected Gtk.TreeIter _groupiter;

		public Gtk.TreeIter frienditer
		{
			get
			{
				if (_frienditer.Equals(Gtk.TreeIter.Zero))
				{
					_frienditer = store.AppendValues(HolderTreeNode.HeaderNew("Friends"));
					if (!_strangeriter.Equals(Gtk.TreeIter.Zero))
						store.MoveBefore(_frienditer, _strangeriter);
					else if (!_groupiter.Equals(Gtk.TreeIter.Zero))
						store.MoveBefore(_frienditer, _groupiter);
				}
	
				return _frienditer;
			}
		}

		public Gtk.TreeIter strangeriter
		{
			get
			{
				if (_strangeriter.Equals(Gtk.TreeIter.Zero))
				{
					_strangeriter = store.AppendValues(HolderTreeNode.HeaderNew("Strangers"));
					if (!_groupiter.Equals(Gtk.TreeIter.Zero))
						store.MoveBefore(_strangeriter, _groupiter);
				}
	
				return _strangeriter;
			}
		}
	
		public Gtk.TreeIter groupiter
		{
			get
			{
				if (_groupiter.Equals(Gtk.TreeIter.Zero))
					_groupiter = store.AppendValues(HolderTreeNode.HeaderNew("Group"));
	
				return _groupiter;
			}
		}
	}

	public class ListStoreSourceTypeID : Gtk.ListStore
	{
		public Interfaces.SourceType type;
		public UInt16 id;
		public ListStoreSourceTypeID(Interfaces.SourceType type, UInt16 id, params Type[] args) : base(args)
		{
			this.type = type;
			this.id = id;
		}
	}
}
