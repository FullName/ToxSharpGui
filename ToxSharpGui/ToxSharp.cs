
using Sys = System;
using SysIO = System.IO;
using SysGlobal = System.Globalization;
using SysACL = System.Security.AccessControl;
using SRIOp = Sys.Runtime.InteropServices; // DllImport

namespace ToxSharpGui
{
	public class Key : Sys.IComparable, Sys.IEquatable<Key>
	{
		protected string _str;
		protected byte[] _bin;

		public Key(string str)
		{
			this._str = str;
		}

		public Key(byte[] bin)
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
			Key key = X as Key;
			if (key != null)
				return string.Compare(str, key.str, true);

			return 0;
		}

		public bool Equals(Key key)
		{
			return string.Compare(str, key.str, true) == 0;
		}
	}

	public enum FriendPresenceState { Unknown, Away, Busy, Invalid };

	public interface IToxSharpBasic
	{
		void ToxConnected(bool state);
	}

	public interface IToxSharpFriend : IToxSharpBasic
	{
		void ToxFriendAddRequest(Key key, string message);

		void ToxFriendInit(int id, Key key, string name, string state, bool online);
		
		void ToxFriendName(int friendId, string name);
		void ToxFriendPresenceState(int friendId, string state);
		void ToxFriendPresenceState(int friendId, FriendPresenceState state);
		void ToxFriendConnected(int friendId, bool connected);
		void ToxFriendMessage(int friendId, string message);
	}

	public interface IToxSharpGroup : IToxSharpBasic
	{
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
						if (accumulated < 1200)
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

		public ToxSharp(IToxSharpBasic cb)
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
	
		protected string ToxConfigHome()
		{
			// TODO: other systems, make directory
			if (System.Environment.OSVersion.Platform == System.PlatformID.Unix)
				return System.Environment.GetEnvironmentVariable("HOME") + "/.config/tox/";
			else
				return "";
		}

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_load(Sys.IntPtr tox, byte[] bytes, Sys.UInt32 length);		

		protected void ToxLoadInternal()
		{
			try
			{
				string filename = ToxConfigHome() + "data";
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
			string filename = ToxConfigHome() + "data";

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

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_getname(Sys.IntPtr tox, int friendnumber, byte[] name);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_get_statusmessage_size(Sys.IntPtr tox, int friendnumber);

		[SRIOp.DllImport("toxcore")] /* string as set by user */
		private static extern int tox_copy_statusmessage(Sys.IntPtr tox, int friendnumber, byte[] buf, Sys.UInt32 maxlen);

		[SRIOp.DllImport("toxcore")] /* 1 == online, 0 == offline */
		private static extern int tox_get_friend_connectionstatus(Sys.IntPtr tox, int friendnumber);

		protected void ToxFriendInitInternal(byte[] name, byte[] state, int i)
		{
			byte[] keybin = new byte[ID_LEN_BINARY];
			tox_getclient_id(tox, i, keybin);

			tox_getname(tox, i, name);
			name[name.Length - 1] = 0;

			Sys.UInt32 lenwithzero =(Sys.UInt32)tox_get_statusmessage_size(tox, i);
			if (state.Length < lenwithzero)
				state = new byte[lenwithzero];
			tox_copy_statusmessage(tox, i, state, lenwithzero);

			if (cbFriend != null)
			{
				Key key = new Key(keybin);
				cbFriend.ToxFriendInit(i, key, CutAtNul(System.Text.Encoding.UTF8.GetString(name)),
						               CutAtNul(System.Text.Encoding.UTF8.GetString(state)),
				                       1 == tox_get_friend_connectionstatus(tox, i));
			}
		}

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.UInt16 tox_getselfname(Sys.IntPtr tox, byte[] bytes, Sys.UInt16 length);

		protected string _name = null;

		public string ToxName()
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
				SysIO.FileStream fs = new SysIO.FileStream(ToxConfigHome() + "DHTservers", System.IO.FileMode.Open);
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

			Key key = new Key(addrbin);
			return key.str;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_addfriend(Sys.IntPtr tox, byte[] friendIdbin, byte[] messagebin, Sys.UInt16 length);

		public int ToxFriendAdd(Key friendkey, string messagestr)
		{
			byte[] messagebin = System.Text.Encoding.UTF8.GetBytes(messagestr + '\0');

			toxmutex.WaitOne();
			int rc = tox_addfriend(tox, friendkey.bin, messagebin, (Sys.UInt16)messagebin.Length);
			toxmutex.ReleaseMutex();

			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_addfriend_norequest(Sys.IntPtr tox, byte[] friendId);

		public int ToxFriendAddNoRequest(Key friendkey)
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
		private static extern int tox_getclient_id(Sys.IntPtr tox, int friendnumber, byte[] friendIdbin);

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_delfriend(Sys.IntPtr tox, int friendnumber);

		public int ToxFriendDel(Key friendkey)
		{
			if (friendkey.bin.Length != ID_LEN_BINARY)
				return -1;

			int rc = -1;
			byte[] bin = new byte[ID_LEN_BINARY];

			toxmutex.WaitOne();
			int friendnum = (int)tox_count_friendlist(tox);
			for(int i = 0; i < friendnum; i++)
			{
				tox_getclient_id(tox, i, bin);

				// holy cow: no memcmp!!!
				int k;
				for(k = 0; k < ID_LEN_BINARY; k++)
					if (friendkey.bin[k] != bin[k])
						break;
			
				if (k == ID_LEN_BINARY)
				{
					rc = tox_delfriend(tox, i);
					break;
				}
			}
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
				Key key = new Key(keybinary);
				cbFriend.ToxFriendAddRequest(key, CutAtNul(System.Text.Encoding.UTF8.GetString(message)));
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
				cbFriend.ToxFriendMessage(id, CutAtNul(System.Text.Encoding.UTF8.GetString(message)));
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
				cbFriend.ToxFriendName(id, CutAtNul(System.Text.Encoding.UTF8.GetString(name)));
		}

		//
		// C1ECE4620571325F8211649B462EC1B3398B87FF13B363ACD682F5A27BC4FD46937EAAF221F2
		//
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


			cbfriendname = new CallBackDelegateFriendName(ToxCallbackFriendName);

			toxmutex.WaitOne();
			tox_callback_namechange(tox, cbfriendname, Sys.IntPtr.Zero);
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

		// unused
		[SRIOp.DllImport("toxcore")] /* unset, away, busy, invalid */
		private static extern FriendPresenceState tox_get_userstatus(Sys.IntPtr tox, int friendnumber);
	}
}