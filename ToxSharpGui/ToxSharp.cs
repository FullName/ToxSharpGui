
using Sys = System;
using SysIO = System.IO;
using SysGlobal = System.Globalization;
using SysACL = System.Security.AccessControl;
using SRIOp = Sys.Runtime.InteropServices; // DllImport
using SysEnv = Sys.Environment;

namespace ToxSharpGui
{
	public interface IToxSharpBasic
	{
		void ToxConnected(bool state);
	}

	public interface IToxSharpFriend : IToxSharpBasic
	{
		void ToxFriendAddRequest(ToxKey key, string message);

		void ToxFriendInit(int id, ToxKey key, string name, bool online, FriendPresenceState presence, string state);
		
		void ToxFriendName(int friendId, string name);
		void ToxFriendPresenceState(int friendId, string state);
		void ToxFriendPresenceState(int friendId, FriendPresenceState state);
		void ToxFriendConnected(int friendId, bool connected);
		void ToxFriendMessage(int friendId, string message);
		void ToxFriendAction(int friendId, string action);
	}

	public interface IToxSharpGroup : IToxSharpBasic
	{
		void ToxGroupchatInvite(int friendnumber, ToxKey friend_groupkey);
		void ToxGroupchatMessage(int groupnumber, int friendgroupnumber, string message);
	}

	public enum FriendPresenceState { Unknown, Away, Busy, Invalid };

	public class ToxKey : Sys.IComparable, Sys.IEquatable<ToxKey>
	{
		protected string _str;
		protected byte[] _bin;

		public ToxKey(string str)
		{
			this._str = str;
		}

		public ToxKey(byte[] bin)
		{
			this._bin = new byte[bin.Length];
			for(uint i = 0; i < bin.Length; i++)
				this._bin[i] = bin[i];
		}

		public string str
		{
			get
			{
				if (_str == null)
				{
					Sys.Text.StringBuilder x = new Sys.Text.StringBuilder(2 * _bin.Length);
					for(int i = 0; i < _bin.Length; i++)
						x.AppendFormat("{0:X2}", _bin[i]);

					_str = x.ToString();
				}

				return _str;
			}
		}

		public byte[] bin
		{
			get
			{
				if (_bin == null)
				{
					_bin = new byte[_str.Length / 2];
					for(int i = 0; i < _bin.Length; i++)
						_bin[i] = Sys.Convert.ToByte(_str.Substring(i * 2, 2), 16);
				}

				return _bin;
			}
		}

		public int CompareTo(object X)
		{
			ToxKey key = X as ToxKey;
			if (key != null)
				return string.Compare(str, key.str, true);

			return 0;
		}

		public bool Equals(ToxKey key)
		{
			return string.Compare(str, key.str, true) == 0;
		}
	}

	public class ToxSharp
	{
		public const int ID_LEN_BINARY = 38;
		public const int NAME_LEN = 128;

		protected IToxSharpBasic cbBasic = null;
		protected IToxSharpFriend cbFriend = null;
		protected IToxSharpGroup cbGroup = null;

		protected Sys.IntPtr tox = Sys.IntPtr.Zero;
		protected Sys.Threading.Mutex toxmutex = null;
		protected Sys.Threading.Thread toxpollthread = null;
		public byte toxpollthreadrequestend = 0;

		[SRIOp.DllImport("toxcore")]
		private static extern void tox_do(Sys.IntPtr tox);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_wait_prepare(Sys.IntPtr tox, byte[] data, ref Sys.UInt16 length);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_wait_execute(Sys.IntPtr tox, byte[] data, Sys.UInt16 length, Sys.UInt16 milliseconds);

		private void toxpollfunc()
		{
			bool connected_ui = false;
			Sys.UInt16 milliseconds = 400;
			Sys.UInt32 accumulated = 0;
			Sys.UInt32 accumulated_max = 1600;
			int res, counter = 3;
			byte[] data = new byte[0];
			while(toxpollthreadrequestend == 0)
			{
				Sys.UInt16 length = (Sys.UInt16)data.Length;

				toxmutex.WaitOne();
				try
				{
					res = tox_wait_prepare(tox, data, ref length);
				}
				catch
				{
					res = -1;
				}
				toxmutex.ReleaseMutex();

				if (res == 0)
				{
					// something bugged outside... bail.
					if (length <= data.Length)
						break;
					
					data = new byte[length];
					continue;
				}

				if (res == 1)
				{
					/* tox_wait() mustn't change anything inside tox,
					 * else we would need locking here, which would
					 * completely destroy the point of the exercise */
					Sys.Console.Write(toxpollthreadrequestend.ToString());
					try
					{
						res = tox_wait_execute(tox, data, length, milliseconds);
					}
					catch
					{
						res = -1;
					}

					if (toxpollthreadrequestend != 0)
						break;
	
					if (res == 0)
					{
						/* every so many times, we can't skip tox_do() */
						accumulated += milliseconds;
						if (accumulated < accumulated_max)
							continue;
					}
					accumulated = 0;
				}

				// wait() not working: sleep "hard"
				if (res == -1)
					Sys.Threading.Thread.Sleep(250000);

				toxmutex.WaitOne();
				tox_do(tox);
				toxmutex.ReleaseMutex();

				if (counter-- < 0)
				{
					counter = 25;
					bool connected_tox = ToxConnected();
					if (connected_tox != connected_ui)
					{
						connected_ui = connected_tox;
						if (cbBasic != null)
							cbBasic.ToxConnected(connected_tox);
					}
				}
			}

			Sys.Console.WriteLine();
			Sys.Console.WriteLine("***");
			toxpollthreadrequestend = 2;
		}

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.IntPtr tox_new(byte ipv6enabled);

		public ToxSharp(string[] args)
		{
			for(int i = 0; i < args.Length; i++)
			{
				if ((args[i] == "-c") && (i + 1 < args.Length))
				{
					Sys.Console.WriteLine("Configuration directory: " + args[i + 1]);
					_ToxConfigHome = args[i + 1];
				}
				if ((args[i] == "-f") && (i + 1 < args.Length))
				{
					Sys.Console.WriteLine("Data filename: " + args[i + 1]);
					_ToxConfigData = args[i + 1];
				}
			}
		}

		public void ToxInit(IToxSharpBasic cb)
		{
			if (cb != null)
			{
				cbBasic = cb;
				cbFriend = cb as IToxSharpFriend;
				cbGroup = cb as IToxSharpGroup;
			}

			toxmutex = new Sys.Threading.Mutex();
			if (toxmutex == null)
				return;

			tox = tox_new(1);
			if (tox != Sys.IntPtr.Zero)
			{
				ToxCallbackInit(tox);
				ToxLoadInternal();
				ToxFriendsInitInternal();
			}
		}
	
		protected string _ToxConfigData;
		protected string ToxConfigData
		{
			get
			{
				if (_ToxConfigData != null)
					return _ToxConfigData;

				_ToxConfigData = ToxConfigHome + "data";
				return _ToxConfigData;
			}
		}

		protected string _ToxConfigHome;
		protected string ToxConfigHome
		{
			get
			{
				if (_ToxConfigHome != null)
					return _ToxConfigHome;

				// TODO: other systems
				if (SysEnv.OSVersion.Platform == System.PlatformID.Unix)
				{
					string path = SysEnv.GetEnvironmentVariable("HOME");
					if (path == null)
						path = "";
					else if (path != "")
					{
						path += "/.config/tox/";
						if (!SysIO.Directory.Exists(path))
							SysIO.Directory.CreateDirectory(path);
					}

					_ToxConfigHome = path;
				}
				else if (SysEnv.OSVersion.Platform == System.PlatformID.Win32NT)
				{
					string path = SysEnv.GetEnvironmentVariable("USERPROFILE");
					if (path == null)
						path = "";
					else if (path != "")
					{
						path += "\\Tox\\";
						if (!SysIO.Directory.Exists(path))
							SysIO.Directory.CreateDirectory(path);
					}

					_ToxConfigHome = path;
				}
				else if (SysEnv.OSVersion.Platform == System.PlatformID.MacOSX)
				{
					string path = SysEnv.GetEnvironmentVariable("HOME");
					if (path == null)
						path = "";
					else if (path != "")
					{
						path += "/Library/Application Support/Tox/";
						if (!SysIO.Directory.Exists(path))
							SysIO.Directory.CreateDirectory(path);
					}

					_ToxConfigHome = path;
				}
				else
					_ToxConfigHome = "";

				return _ToxConfigHome;
			}
		}

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_load(Sys.IntPtr tox, byte[] bytes, Sys.UInt32 length);		

		protected void ToxLoadInternal()
		{
			try
			{
				string filename = ToxConfigData;
				SysIO.FileInfo fsinfo = new SysIO.FileInfo(filename);
				SysIO.FileStream fs = new SysIO.FileStream(filename, System.IO.FileMode.Open);
				byte[] space = new byte[fsinfo.Length];
				fs.Read(space, 0,(int)fsinfo.Length);
				fs.Close();

				tox_load(tox, space,(Sys.UInt32)fsinfo.Length);
			}
			catch
			{
			}
		}

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.UInt32 tox_size(Sys.IntPtr tox);		

		[SRIOp.DllImport("toxcore")]
		private static extern void tox_save(Sys.IntPtr tox, byte[] bytes);

		public void ToxSave()
		{
			string filename = ToxConfigData;

			toxmutex.WaitOne();
			byte[] space = new byte[tox_size(tox)];
			tox_save(tox, space);
			toxmutex.ReleaseMutex();

			SysIO.FileStream fs = new SysIO.FileStream(filename, System.IO.FileMode.Create);
			fs.Write(space, 0, space.Length);
			fs.Close();
		}

		public void ToxFriendsInit()
		{
			toxmutex.WaitOne();
			ToxFriendsInitInternal();
			toxmutex.ReleaseMutex();
		}

		protected void ToxFriendsInitInternal()
		{
			Sys.UInt32 friendnum = tox_count_friendlist(tox);

			byte[] name = new byte[NAME_LEN + 1];
			byte[] state = new byte[NAME_LEN + 1];
			for(int i = 0; i < friendnum; i++)
				ToxFriendInitInternal(name, state, i);
		}

		public void ToxFriendInit(int i)
		{
			byte[] name = new byte[NAME_LEN + 1];
			byte[] state = new byte[NAME_LEN + 1];

			toxmutex.WaitOne();
			ToxFriendInitInternal(name, state, i);
			toxmutex.ReleaseMutex();
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_getclient_id(Sys.IntPtr tox, int friendnumber, byte[] friendIdbin);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_getname(Sys.IntPtr tox, int friendnumber, byte[] name);

		[SRIOp.DllImport("toxcore")] /* 1 == online, 0 == offline */
		private static extern int tox_get_friend_connectionstatus(Sys.IntPtr tox, int friendnumber);

		[SRIOp.DllImport("toxcore")] /* unset, away, busy, invalid */
		private static extern FriendPresenceState tox_get_userstatus(Sys.IntPtr tox, int friendnumber);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_get_statusmessage_size(Sys.IntPtr tox, int friendnumber);

		[SRIOp.DllImport("toxcore")] /* string as set by user */
		private static extern int tox_copy_statusmessage(Sys.IntPtr tox, int friendnumber, byte[] buf, Sys.UInt32 maxlen);

		protected void ToxFriendInitInternal(byte[] name, byte[] state, int i)
		{
			byte[] keybin = new byte[ID_LEN_BINARY];
			tox_getclient_id(tox, i, keybin);

			tox_getname(tox, i, name);
			name[name.Length - 1] = 0;

			FriendPresenceState presence = tox_get_userstatus(tox, i);

			Sys.UInt32 lenwithzero =(Sys.UInt32)tox_get_statusmessage_size(tox, i);
			if (state.Length < lenwithzero)
				state = new byte[lenwithzero];
			tox_copy_statusmessage(tox, i, state, lenwithzero);

			if (cbFriend != null)
			{
				ToxKey key = new ToxKey(keybin);
				cbFriend.ToxFriendInit(i, key, CutAtNul(System.Text.Encoding.UTF8.GetString(name)),
				                       1 == tox_get_friend_connectionstatus(tox, i), presence,
						               CutAtNul(System.Text.Encoding.UTF8.GetString(state)));
			}
		}

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.UInt16 tox_getselfname(Sys.IntPtr tox, byte[] bytes, Sys.UInt16 length);

		protected string _name = null;

		public string ToxNameGet()
		{
			if (_name != null)
				return _name;
			
			if (tox == Sys.IntPtr.Zero)
				return "";

			byte[] space = new byte[NAME_LEN + 1];
			toxmutex.WaitOne();
			Sys.UInt16 len = tox_getselfname(tox, space, (Sys.UInt16)(space.Length - 1));
			toxmutex.ReleaseMutex();

			space[len] = 0;
			_name = CutAtNul(System.Text.Encoding.UTF8.GetString(space));

			return _name;
		}
		
		[SRIOp.DllImport("toxcore")]
		private static extern int tox_setname(Sys.IntPtr tox, byte[] namebin, Sys.UInt16 length);

		public int ToxNameSet(string namestr)
		{
			if (tox == Sys.IntPtr.Zero)
				return -1;

			byte[] namebin = System.Text.Encoding.UTF8.GetBytes(namestr + '\0');

			toxmutex.WaitOne();
			int rc = tox_setname(tox, namebin, (Sys.UInt16)namebin.Length);
			toxmutex.ReleaseMutex();

			if (rc == 0)
				_name = namestr;

			return rc == 0 ? 1 : 0;
		}

		public int ToxBootstrap()
		{
			if (tox == Sys.IntPtr.Zero)
				return -1;

			toxmutex.WaitOne();
			int rc = ToxBootstrapInternal();
			toxmutex.ReleaseMutex();

			if (toxpollthread == null)
			{
				toxpollthread = new Sys.Threading.Thread(toxpollfunc);
				if (toxpollthread != null)
					toxpollthread.Start();
			}

			return rc;
		}

		[SRIOp.DllImport("toxcore", CharSet = SRIOp.CharSet.Ansi)]
		private static extern int tox_bootstrap_from_address(Sys.IntPtr tox, string address, byte ipv6enabled, Sys.UInt16 port, byte[] key);

		private int ToxBootstrapInternal()
		{
			int addrok = 0;

			try
			{
				SysIO.FileStream fs = new SysIO.FileStream(ToxConfigHome + "DHTservers", System.IO.FileMode.Open);
				SysIO.StreamReader sr = new SysIO.StreamReader(fs);
				while(!sr.EndOfStream)
				{
					string line = sr.ReadLine();
					if (line.Length > 50)
					{
						Sys.String[] strfld = line.Split();
						if (strfld.Length < 3)
							continue;
	
						Sys.UInt16 port = Sys.Convert.ToUInt16(strfld[1], 10);
						port =(Sys.UInt16)Sys.Net.IPAddress.HostToNetworkOrder((short)port);
	
						byte[] key = new byte[32];
						if (strfld[2].Length < 64)
							continue;
	
						strfld[2].ToCharArray();
						for(int i = 0; i < 32; i++)
							Sys.Byte.TryParse(strfld[2].Substring(i * 2, 2), SysGlobal.NumberStyles.HexNumber, null, out key[i]);
	
						if (1 == tox_bootstrap_from_address(tox, strfld[0] + '\0', 1, port, key))
							addrok++;
					}
				}
			}
			catch
			{
			}

			return addrok;
		}

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_isconnected(Sys.IntPtr tox);

		public bool ToxConnected()
		{
			if (tox == Sys.IntPtr.Zero)
				return false;

			toxmutex.WaitOne();
			bool rc = tox_isconnected(tox) == 1;
			toxmutex.ReleaseMutex();

			return rc;
		}

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.UInt32 tox_count_friendlist(Sys.IntPtr tox);

		public Sys.UInt32 ToxFriendNum()
		{
			if (tox == Sys.IntPtr.Zero)
				return 0;

			toxmutex.WaitOne();
			Sys.UInt32 rc = tox_count_friendlist(tox);
			toxmutex.ReleaseMutex();

			return rc;
		}

		protected string _id = null;

		public string ToxSelfID()
		{
			if (_id != null)
				return _id;

			if (tox == Sys.IntPtr.Zero)
				return "";

			toxmutex.WaitOne();
			_id = ToxSelfIDInternal(tox);
			toxmutex.ReleaseMutex();

			return _id;
		}

		[SRIOp.DllImport("toxcore")]
		private static extern void tox_getaddress(Sys.IntPtr tox, byte[] address);

		protected string ToxSelfIDInternal(Sys.IntPtr tox)
		{
			byte[] addrbin = new byte[ID_LEN_BINARY];
			tox_getaddress(tox, addrbin);

			ToxKey key = new ToxKey(addrbin);
			return key.str;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_addfriend(Sys.IntPtr tox, byte[] friendIdbin, byte[] messagebin, Sys.UInt16 length);

		public int ToxFriendAdd(ToxKey friendkey, string messagestr)
		{
			byte[] messagebin = System.Text.Encoding.UTF8.GetBytes(messagestr + '\0');

			toxmutex.WaitOne();
			int rc = tox_addfriend(tox, friendkey.bin, messagebin, (Sys.UInt16)messagebin.Length);
			toxmutex.ReleaseMutex();

			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_addfriend_norequest(Sys.IntPtr tox, byte[] friendId);

		public int ToxFriendAddNoRequest(ToxKey friendkey)
		{
			if (friendkey.bin.Length != ID_LEN_BINARY)
				return -1;

			toxmutex.WaitOne();
			int rc = tox_addfriend_norequest(tox, friendkey.bin);
			if (rc >= 0)
				ToxFriendInit(rc);
			toxmutex.ReleaseMutex();

			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_getfriend_id(Sys.IntPtr tox, byte[] client_id);

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_delfriend(Sys.IntPtr tox, int friendnumber);

		public int ToxFriendDel(ToxKey friendkey)
		{
			if (friendkey.bin.Length != ID_LEN_BINARY)
				return -1;

			toxmutex.WaitOne();
			int rc = tox_getfriend_id(tox, friendkey.bin);
			if (rc >= 0)
				rc = tox_delfriend(tox, rc);
			toxmutex.ReleaseMutex();

			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern Sys.UInt32 tox_sendmessage(Sys.IntPtr tox, int friendnumber, byte[] message, Sys.UInt32 length);

		public Sys.UInt32 ToxFriendMessage(Sys.UInt16 id, string messagestr)
		{
			// NOT null-terminated, is that a problem? Yes.
			byte[] messagebin = System.Text.Encoding.UTF8.GetBytes(messagestr + '\0');

			toxmutex.WaitOne();
			Sys.UInt32 rc = tox_sendmessage(tox, id, messagebin, (Sys.UInt32)messagebin.Length);
			toxmutex.ReleaseMutex();
			
			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_sendaction(Sys.IntPtr tox, int friendnumber, byte[] action, Sys.UInt32 length);

		public int ToxFriendAction(Sys.UInt16 id, string actionstr)
		{
			// NOT null-terminated, is that a problem? Yes.
			byte[] actionbin = System.Text.Encoding.UTF8.GetBytes(actionstr + '\0');

			toxmutex.WaitOne();
			int rc = tox_sendaction(tox, id, actionbin, (Sys.UInt32)actionbin.Length);
			toxmutex.ReleaseMutex();

			return rc;
		}

/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_add_groupchat(Sys.IntPtr tox);

		public bool ToxGroupchatAdd(out int id)
		{
			toxmutex.WaitOne();
			id = tox_add_groupchat(tox);
			toxmutex.ReleaseMutex();

			return id >= 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_del_groupchat(Sys.IntPtr tox, int groupnumber);

		public bool ToxGroupchatDel(int groupnumber)
		{
			toxmutex.WaitOne();
			int rc = tox_del_groupchat(tox, groupnumber);
			toxmutex.ReleaseMutex();

			return rc == 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_group_peername(Sys.IntPtr tox, int groupnumber, int peernumber, byte[] name);

		public bool ToxGroupchatPeername(int groupnumber, int peernumber, out string namestr)
		{
			byte[] namebin = new byte[NAME_LEN + 1];

			toxmutex.WaitOne();
			int rc = tox_group_peername(tox, groupnumber, peernumber, namebin);
			toxmutex.ReleaseMutex();

			namestr = "";
			if (rc > 0)
				namestr = CutAtNul(System.Text.Encoding.UTF8.GetString(namebin));

			return rc >= 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_invite_friend(Sys.IntPtr tox, int friendnumber, int groupnumber);

		public bool ToxGroupchatInvite(int groupnumber, ToxKey friendkey)
		{
			toxmutex.WaitOne();
			int friendnumber = tox_getfriend_id(tox, friendkey.bin);
			int rc = -1;
			if (friendnumber >= 0)
				rc = tox_invite_friend(tox, friendnumber, groupnumber);
			toxmutex.ReleaseMutex();

			return rc == 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_join_groupchat(Sys.IntPtr tox, int friendnumber, byte[] friend_groupkey);

		public bool ToxGroupchatJoin(int friendnumber, ToxKey friend_groupkey, out int groupnumber)
		{
			toxmutex.WaitOne();
			groupnumber = tox_join_groupchat(tox, friendnumber, friend_groupkey.bin);
			toxmutex.ReleaseMutex();

			return groupnumber >= 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_group_message_send(Sys.IntPtr tox, int groupnumber, byte[] messagebin, Sys.UInt32 length);

		public bool ToxGroupchatMessage(int groupnumber, string messagestr)
		{
			byte[] messagebin = System.Text.Encoding.UTF8.GetBytes(messagestr + '\0');

			toxmutex.WaitOne();
			int rc = tox_group_message_send(tox, groupnumber, messagebin, (Sys.UInt32)messagebin.Length);
			toxmutex.ReleaseMutex();

			return rc == 0;
		}

/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/

		/*
		 * status: 0 = offline, 1 = online
		 *
		 * void callback(Tox *tox, int friendnumber, uint8_t status, void *userdata)
		 *
		 * void tox_callback_connectionstatus(Tox *tox, void(*function)(Tox *tox, int, uint8_t, void *), void *userdata);
		 */
		protected delegate void CallBackDelegateFriendConnectionStatus(Sys.IntPtr tox, int friendId, byte state, Sys.IntPtr X);
		protected CallBackDelegateFriendConnectionStatus cbfriendconnectionstatus;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_connectionstatus(Sys.IntPtr tox, CallBackDelegateFriendConnectionStatus cbfriendconnectionstatus, Sys.IntPtr X);

		protected void ToxCallbackFriendConnectionStatus(Sys.IntPtr tox, int id, byte state, Sys.IntPtr X)
		{
			if (cbFriend != null)
				cbFriend.ToxFriendConnected(id, state != 0);
		}

		/*
		 * void callback(uint8_t *public_key, uint8_t *data, uint16_t length)
		 * 
		 * void tox_callback_friendrequest(Tox *tox, void(*function)(uint8_t *, uint8_t *, uint16_t, void *), void *userdata);
		 */
		
		protected delegate void CallBackDelegateFriendAddRequest([SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeConst = 38)] byte[] key, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] message, Sys.UInt16 length, Sys.IntPtr tox);
		protected CallBackDelegateFriendAddRequest cbfriendaddrequest;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_friendrequest(Sys.IntPtr tox, CallBackDelegateFriendAddRequest cbfriendaddrequest, Sys.IntPtr X);

		protected void ToxCallbackFriendAddRequest(byte[] keybinary, byte[] message, Sys.UInt16 length, Sys.IntPtr X)
		{
			if (cbFriend != null)
			{
				ToxKey key = new ToxKey(keybinary);
				cbFriend.ToxFriendAddRequest(key, CutAtNul(System.Text.Encoding.UTF8.GetString(message, 0, length - 1)));
			}
		}

		/*
		 * void callback(Tox *tox, int friendid, uint8_t *data, uint16_t length, void *userdata)
		 * 
		 * void tox_callback_friendmessage(Tox *tox,
		 * 								   void (*function)(Tox *tox, int, uint8_t *, uint16_t, void *),
		 * 								   void *userdata);
		 */
		
		protected delegate void CallBackDelegateFriendMessage(Sys.IntPtr tox, int friendid, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] message, Sys.UInt16 length, Sys.IntPtr X);
		protected CallBackDelegateFriendMessage cbfriendmessage;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_friendmessage(Sys.IntPtr tox, CallBackDelegateFriendMessage cbfriendmessage, Sys.IntPtr X);

		protected void ToxCallbackFriendMessage(Sys.IntPtr tox, int id, byte[] message, Sys.UInt16 length, Sys.IntPtr X)
		{
			if (cbFriend != null)
				cbFriend.ToxFriendMessage(id, CutAtNul(System.Text.Encoding.UTF8.GetString(message, 0, length - 1)));
		}

		/*
		 * void callback(Tox *tox, int friendid, uint8_t *data, uint16_t length, void *userdata)
		 * 
		 * void tox_callback_action(Tox *tox,
		 * 							void (*function)(Tox *tox, int, uint8_t *, uint16_t, void *),
		 * 							void *userdata);
		 */
		
		protected delegate void CallBackDelegateFriendAction(Sys.IntPtr tox, int friendid, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] action, Sys.UInt16 length, Sys.IntPtr X);
		protected CallBackDelegateFriendAction cbfriendaction;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_action(Sys.IntPtr tox, CallBackDelegateFriendAction cbfriendaction, Sys.IntPtr X);

		protected void ToxCallbackFriendAction(Sys.IntPtr tox, int id, byte[] action, Sys.UInt16 length, Sys.IntPtr X)
		{
			if (cbFriend != null)
				cbFriend.ToxFriendAction(id, CutAtNul(System.Text.Encoding.UTF8.GetString(action, 0, length - 1)));
		}

		/*
		 * void callback(Tox *tox, int friendid, uint8_t *data, uint16_t length, void *userdata)
		 * 
		 * void tox_callback_namechange(Tox *tox,
		 * 								void (*function)(Tox *tox, int, uint8_t *, uint16_t, void *),
		 * 								void *userdata);
		 */
		
		protected delegate void CallBackDelegateFriendName(Sys.IntPtr tox, int id, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] name, Sys.UInt16 length, Sys.IntPtr X);
		protected CallBackDelegateFriendName cbfriendname;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_namechange(Sys.IntPtr tox, CallBackDelegateFriendName cbfriendname, Sys.IntPtr X);

		protected void ToxCallbackFriendName(Sys.IntPtr tox, int id, byte[] name, Sys.UInt16 length, Sys.IntPtr X)
		{
			if (cbFriend != null)
				cbFriend.ToxFriendName(id, CutAtNul(System.Text.Encoding.UTF8.GetString(name, 0, length - 1)));
		}

/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/

		/*
		 * void callback(Tox *tox, int friendnumber, uint8_t *group_public_key, void *userdata)
		 *
		 * void tox_callback_group_invite(Tox *tox,
		 *                                void (*function)(Tox *tox, int, uint8_t *, void *),
		 *                                void *userdata);
		 */

		protected delegate void CallBackDelegateGroupchatInvite(Sys.IntPtr tox, int friendnumber, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeConst = 38)] byte[] friend_groupkey, Sys.IntPtr X);
		protected CallBackDelegateGroupchatInvite cbgroupchatinvite;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_group_invite(Sys.IntPtr tox, CallBackDelegateGroupchatInvite cbgroupchatinvite, Sys.IntPtr X);

		protected void ToxCallbackGroupchatInvite(Sys.IntPtr tox, int friendnumber, byte[] friend_groupkeybin, Sys.IntPtr X)
		{
			if (cbGroup != null)
			{
				ToxKey friend_groupkey = new ToxKey(friend_groupkeybin);
				cbGroup.ToxGroupchatInvite(friendnumber, friend_groupkey);
			}
		}

		/*
		 * void callback(Tox *tox, int groupnumber, int friendgroupnumber, uint8_t * message, uint16_t length, void *userdata);
		 *
		 * void tox_callback_group_message(Tox *tox,
		 *                              void (*function)(Tox *tox, int, int, uint8_t *, uint16_t, void *),
		 * 								void *userdata);
		 */

		protected delegate void CallBackDelegateGroupchatMessage(Sys.IntPtr tox, int groupnumber, int friendgroupnumber,
									[SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] message,
		                                                         Sys.UInt16 length, Sys.IntPtr X);
		protected CallBackDelegateGroupchatMessage cbgroupchatmessage;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_group_message(Sys.IntPtr tox, CallBackDelegateGroupchatMessage cbgroupchatmessage, Sys.IntPtr X);

		protected void ToxCallbackGroupchatMessage(Sys.IntPtr tox, int groupnumber, int friendgroupnumber, byte[] message, Sys.UInt16 length, Sys.IntPtr X)
		{
			if (cbGroup != null)
				cbGroup.ToxGroupchatMessage(groupnumber, friendgroupnumber, CutAtNul(System.Text.Encoding.UTF8.GetString(message, 0, length - 1)));
		}

/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/

		protected void ToxCallbackInit(Sys.IntPtr tox)
		{
			cbfriendconnectionstatus = new CallBackDelegateFriendConnectionStatus(ToxCallbackFriendConnectionStatus);

			toxmutex.WaitOne();
			tox_callback_connectionstatus(tox, cbfriendconnectionstatus, Sys.IntPtr.Zero);
			toxmutex.ReleaseMutex();


			cbfriendaddrequest = new CallBackDelegateFriendAddRequest(ToxCallbackFriendAddRequest);

			toxmutex.WaitOne();
			tox_callback_friendrequest(tox, cbfriendaddrequest, Sys.IntPtr.Zero);
			toxmutex.ReleaseMutex();

			
			cbfriendmessage = new CallBackDelegateFriendMessage(ToxCallbackFriendMessage);

			toxmutex.WaitOne();
			tox_callback_friendmessage(tox, cbfriendmessage, Sys.IntPtr.Zero);
			toxmutex.ReleaseMutex();


			cbfriendaction = new CallBackDelegateFriendAction(ToxCallbackFriendAction);

			toxmutex.WaitOne();
			tox_callback_action(tox, cbfriendaction, Sys.IntPtr.Zero);
			toxmutex.ReleaseMutex();


			cbfriendname = new CallBackDelegateFriendName(ToxCallbackFriendName);

			toxmutex.WaitOne();
			tox_callback_namechange(tox, cbfriendname, Sys.IntPtr.Zero);
			toxmutex.ReleaseMutex();

/*****************************************************************************/

			cbgroupchatinvite = new CallBackDelegateGroupchatInvite(ToxCallbackGroupchatInvite);

			toxmutex.WaitOne();
			tox_callback_group_invite(tox, cbgroupchatinvite, Sys.IntPtr.Zero);
			toxmutex.ReleaseMutex();


			cbgroupchatmessage = new CallBackDelegateGroupchatMessage(ToxCallbackGroupchatMessage);

			toxmutex.WaitOne();
			tox_callback_group_message(tox, cbgroupchatmessage, Sys.IntPtr.Zero);
			toxmutex.ReleaseMutex();
		}

		string CutAtNul(string data)
		{
			if (data.Length == 0)
				return data;

			int zero = data.IndexOf('\0');
			if (zero >= 0)
				return data.Substring(0, zero);
			else
				return data;
		}
	}
}
