
using Sys = System;
using SysIO = System.IO;
using SysGlobal = System.Globalization;
using SysACL = System.Security.AccessControl;
using SRIOp = Sys.Runtime.InteropServices; // DllImport
using SysEnv = Sys.Environment;

namespace ToxSharpBasic
{
	internal interface IToxSharpBasic
	{
		void ToxDo(Interfaces.CallToxDo calltoxdo, Sys.IntPtr tox);
		void ToxConnected(bool state);
	}

	internal interface IToxSharpFriend
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

	internal enum ToxGroupNamelistChangeType { PeerAdded = 0, PeerRemoved, PeerNamechange, Unknown = 255 };

	internal interface IToxSharpGroup
	{
		void ToxGroupchatInit(Sys.UInt16 groupchatnum);
		void ToxGroupchatInvite(int friendnumber, string friendname, ToxKey friend_groupkey);
		void ToxGroupchatMessage(int groupnumber, int friendgroupnumber, string message);
		void ToxGroupNamelistChange(int groupnumber, int peernumber, ToxGroupNamelistChangeType change);
	}

	internal interface IToxSharpRendezvous
	{
		void ToxRendezvousFound(ushort ID, ToxKey key);
		byte ToxRendezvousTimeout(ushort ID);
	}

	internal enum FriendPresenceState { Unknown, Away, Busy, Invalid };

	internal class ToxKey : Sys.IComparable, Sys.IEquatable<ToxKey>
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

	internal class ToxInterface
	{
		public const int ID_LEN_BINARY = 38;
		public const int NAME_LEN = 128;

		protected IToxSharpBasic cbbasic = null;
		protected IToxSharpFriend cbfriend = null;
		protected IToxSharpGroup cbgroup = null;
		protected IToxSharpRendezvous cbrendezvous = null;

		protected Sys.IntPtr tox = Sys.IntPtr.Zero;
		protected Sys.Threading.Mutex toxmutex = null;
		protected Sys.Threading.Thread toxpollthread = null;

		protected enum ToxPollThreadState { NOT_CREATED, STARTING, RUNNING, ENDREQUESTED, ENDED, DONE };
		protected ToxPollThreadState toxpollstate = ToxPollThreadState.NOT_CREATED;

		protected static string ToString(byte[] bin)
		{
			return CutAtNul(System.Text.Encoding.UTF8.GetString(bin));
		}

		protected static byte[] ToBytes(string str)
		{
			return System.Text.Encoding.UTF8.GetBytes(str + '\0');
		}

		[SRIOp.DllImport("toxcore")]
		private static extern void tox_do(Sys.IntPtr tox);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_wait_prepare(Sys.IntPtr tox, byte[] data, ref Sys.UInt16 length);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_wait_execute(Sys.IntPtr tox, byte[] data, Sys.UInt16 length, Sys.UInt16 milliseconds);

		[SRIOp.DllImport("toxcore")]
		private static extern void tox_wait_cleanup(Sys.IntPtr tox, byte[] data, Sys.UInt16 length);

		private void ToxPollFunc()
		{
			toxmutex.WaitOne();
			if (toxpollstate < ToxPollThreadState.RUNNING)
				toxpollstate = ToxPollThreadState.RUNNING;
			toxmutex.ReleaseMutex();

			// give UI some time to come up
			// TODO: let ui start this
			Sys.Threading.Thread.Sleep(2000);
			MainClass.PrintDebug("Poll thread now running.\n");

			bool canpoll = false;
			int res;
			Sys.UInt16 length = 0;
			byte[] data = null;

			toxmutex.WaitOne();

			try
			{
				res = tox_wait_prepare(tox, data, ref length);
			}
			catch
			{
				res = -1;
			}
			if (res == 0)
			{
				data = new byte[length];
				try
				{
					res = tox_wait_prepare(tox, data, ref length);
				}
				catch
				{
					res = -1;
				}

				if (res == 1)
					canpoll = true;
			}

			toxmutex.ReleaseMutex();

			bool connected_ui = false;
			Sys.UInt16 milliseconds = 400;
			Sys.UInt32 accumulated = 0;
			Sys.UInt32 accumulated_max = 1600;
			int counter = 3;
			while(toxpollstate == ToxPollThreadState.RUNNING)
			{
				if (canpoll)
				{
					try
					{
						toxmutex.WaitOne();
						try
						{
							res = tox_wait_prepare(tox, data, ref length);
						}
						finally
						{
							toxmutex.ReleaseMutex();
						}

						if (res != 1)
						{
							canpoll = false;
							continue;
						}

						// Sys.Console.Write(toxpollthreadrequestend.ToString());

						/* tox_wait_execute() mustn't change anything inside tox,
						 * else we would need locking here, which would
						 * completely destroy the point of the exercise */
						res = tox_wait_execute(tox, data, length, milliseconds);

						if (toxpollstate != ToxPollThreadState.RUNNING)
							break;

						if (res == -1)
						{
							canpoll = false;
							continue;
						}

						toxmutex.WaitOne();
					    try
						{
							tox_wait_cleanup(tox, data, length);
						}
						finally
						{
							toxmutex.ReleaseMutex();
						}

						if (res == 0)
						{
							/* every so many times, we can't skip tox_do() */
							accumulated += milliseconds;
							if (accumulated < accumulated_max)
								continue;
						}
						accumulated = 0;
					}
				    catch
					{
						canpoll = false;
						continue;
					}
				}
				else // wait() not working: sleep "hard" 100ms
					Sys.Threading.Thread.Sleep(100);

				// GUI thread runs tox_do() for now to ensure callbacks returning in GUI thread
				if (cbbasic != null)
					cbbasic.ToxDo(ToxDo, tox);

				if (counter-- < 0)
				{
					counter = 25;
					bool connected_tox = ToxConnected();
					if (connected_tox != connected_ui)
					{
						connected_ui = connected_tox;
						if (cbbasic != null)
							cbbasic.ToxConnected(connected_tox);
					}
				}
			}

			Sys.Console.WriteLine();
			Sys.Console.WriteLine("***");

			toxmutex.WaitOne();
			if (toxpollstate < ToxPollThreadState.ENDED)
				toxpollstate = ToxPollThreadState.ENDED;
			toxmutex.ReleaseMutex();
		}

		public void ToxDo(Sys.IntPtr tox)
		{
			toxmutex.WaitOne();
			try
			{
				tox_do(tox);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}
		}

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.IntPtr tox_new(byte ipv6enabled);

		public ToxInterface(string[] args)
		{
			for(int i = 0; i < args.Length; i++)
			{
				if ((args[i] == "-c") && (i + 1 < args.Length))
				{
					MainClass.PrintDebug("Configuration directory: " + args[i + 1]);
					_ToxConfigHome = args[i + 1];
				}
				if ((args[i] == "-f") && (i + 1 < args.Length))
				{
					MainClass.PrintDebug("Data filename: " + args[i + 1]);
					_ToxConfigData = args[i + 1];
				}
			}
		}

		public void ToxInit(IToxSharpBasic cbbasic, IToxSharpFriend cbfriend, IToxSharpGroup cbgroup, IToxSharpRendezvous cbrendezvous)
		{
			this.cbbasic = cbbasic;
			this.cbfriend = cbfriend;
			this.cbgroup = cbgroup;
			this.cbrendezvous = cbrendezvous;

			toxmutex = new Sys.Threading.Mutex();
			if (toxmutex == null)
				return;

			tox = tox_new(1);
			if (tox != Sys.IntPtr.Zero)
			{
				ToxCallbackInit();
				ToxLoadInternal();
				ToxFriendsInitInternal();
			}
		}

		public void ToxStopAndSave()
		{
			if (toxpollstate == ToxPollThreadState.NOT_CREATED)
				return;

			toxmutex.WaitOne();
			if (toxpollstate == ToxPollThreadState.RUNNING)
				toxpollstate = ToxPollThreadState.ENDREQUESTED;
			toxmutex.ReleaseMutex();

			if (toxpollstate == ToxPollThreadState.ENDREQUESTED)
			{
				uint tries = 100;
				while ((toxpollstate < ToxPollThreadState.ENDED) && (tries-- > 0))
					System.Threading.Thread.Sleep(100);
			}

			if (toxpollstate == ToxPollThreadState.ENDED)
			{
				bool save = false;

				toxmutex.WaitOne();
				if (toxpollstate == ToxPollThreadState.ENDED)
				{
					save = true;
					toxpollstate = ToxPollThreadState.DONE;
				}
				toxmutex.ReleaseMutex();

				if (save)
					ToxSave();  // sets lock internally
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
		public string ToxConfigHome
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
					// standard windows: <userdir>/appdata/local
					string path = SysEnv.GetEnvironmentVariable("LOCALAPPDATA");
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
						// taken from Toxic
						path += "/Library/Application Support/Tox/";
						if (!SysIO.Directory.Exists(path))
							SysIO.Directory.CreateDirectory(path);
					}

					_ToxConfigHome = path;
				}
				else
					_ToxConfigHome = "";

				MainClass.PrintDebug("Default data directory: " + _ToxConfigHome);
				return _ToxConfigHome;
			}
		}

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_load(Sys.IntPtr tox, byte[] bytes, Sys.UInt32 length);		

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.UInt16 tox_num_groupchats(Sys.IntPtr tox);		

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

				tox_load(tox, space, (Sys.UInt32)fsinfo.Length);

				/*
				 * tox_num_groupchats(...) will throw up as it
				 * hasn't been accepted in the official tree
				 */
				Sys.UInt16 groupchatnum = tox_num_groupchats(tox);
				if ((groupchatnum > 0) && (cbgroup != null))
					cbgroup.ToxGroupchatInit(groupchatnum);
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
			byte[] space = new byte[tox_size(tox)];

			toxmutex.WaitOne();
			try
			{
				tox_save(tox, space);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			SysIO.FileStream fs = new SysIO.FileStream(filename, System.IO.FileMode.Create);
			fs.Write(space, 0, space.Length);
			fs.Close();
		}

		public void ToxFriendsInit()
		{
			toxmutex.WaitOne();
			try
			{
				ToxFriendsInitInternal();
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}
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
			try
			{
				ToxFriendInitInternal(name, state, i);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_get_client_id(Sys.IntPtr tox, int friendnumber, byte[] friendIdbin);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_get_name(Sys.IntPtr tox, int friendnumber, byte[] name);

		[SRIOp.DllImport("toxcore")] /* 1 == online, 0 == offline */
		private static extern int tox_get_friend_connection_status(Sys.IntPtr tox, int friendnumber);

		[SRIOp.DllImport("toxcore")] /* unset, away, busy, invalid */
		private static extern FriendPresenceState tox_get_user_status(Sys.IntPtr tox, int friendnumber);

		[SRIOp.DllImport("toxcore")]
		private static extern int tox_get_status_message_size(Sys.IntPtr tox, int friendnumber);

		[SRIOp.DllImport("toxcore")] /* string as set by user */
		private static extern int tox_copy_statusmessage(Sys.IntPtr tox, int friendnumber, byte[] buf, Sys.UInt32 maxlen);

		protected void ToxFriendInitInternal(byte[] name, byte[] state, int i)
		{
			byte[] keybin = new byte[ID_LEN_BINARY];
			tox_get_client_id(tox, i, keybin);

			tox_get_name(tox, i, name);
			name[name.Length - 1] = 0;

			FriendPresenceState presence = tox_get_user_status(tox, i);

			Sys.UInt32 lenwithzero =(Sys.UInt32)tox_get_status_message_size(tox, i);
			if (state.Length < lenwithzero)
				state = new byte[lenwithzero];
			tox_copy_statusmessage(tox, i, state, lenwithzero);

			if (cbfriend != null)
			{
				ToxKey key = new ToxKey(keybin);
				cbfriend.ToxFriendInit(i, key, CutAtNul(System.Text.Encoding.UTF8.GetString(name)),
				                       1 == tox_get_friend_connection_status(tox, i), presence,
						               CutAtNul(System.Text.Encoding.UTF8.GetString(state)));
			}
		}

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.UInt16 tox_get_self_name(Sys.IntPtr tox, byte[] bytes, Sys.UInt16 length);

		protected string _name = null;

		public string ToxNameGet()
		{
			if (_name != null)
				return _name;
			
			if (tox == Sys.IntPtr.Zero)
				return "";

			byte[] space = new byte[NAME_LEN + 1];
			Sys.UInt16 len = 0;

			toxmutex.WaitOne();
			try
			{
				len = tox_get_self_name(tox, space, (Sys.UInt16)(space.Length - 1));
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			space[len] = 0;
			_name = CutAtNul(System.Text.Encoding.UTF8.GetString(space));

			return _name;
		}
		
		[SRIOp.DllImport("toxcore")]
		private static extern int tox_set_name(Sys.IntPtr tox, byte[] namebin, Sys.UInt16 length);

		public int ToxNameSet(string namestr)
		{
			if (tox == Sys.IntPtr.Zero)
				return -1;

			int rc = -1;
			byte[] namebin = System.Text.Encoding.UTF8.GetBytes(namestr + '\0');

			toxmutex.WaitOne();
			try
			{
				rc = tox_set_name(tox, namebin, (Sys.UInt16)namebin.Length);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			if (rc == 0)
				_name = namestr;

			return rc == 0 ? 1 : 0;
		}

		public int ToxBootstrap()
		{
			if (tox == Sys.IntPtr.Zero)
				return -1;

			int rc = -1;
			toxmutex.WaitOne();

			try
			{
				rc = ToxBootstrapInternal();
				if (toxpollstate == ToxPollThreadState.NOT_CREATED)
				{
					Sys.Threading.Thread toxpollthread = new Sys.Threading.Thread(ToxPollFunc);
					if (toxpollthread != null)
					{
						toxpollstate = ToxPollThreadState.STARTING;
						toxpollthread.Start();
					}
				}
			}
			finally
			{
				toxmutex.ReleaseMutex();
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
				MainClass.PrintDebug("Reading bootstrap servers from: " + ToxConfigHome + "DHTservers");
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

						try
						{
							if (1 == tox_bootstrap_from_address(tox, strfld[0] + '\0', 1, port, key))
								addrok++;
							else
								MainClass.PrintDebug("Failed to parse line: " + line);
						}
						catch
						{
						}
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

			bool rc = false;
			toxmutex.WaitOne();
			try
			{
				rc = tox_isconnected(tox) == 1;
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return rc;
		}

		[SRIOp.DllImport("toxcore")]
		private static extern Sys.UInt32 tox_count_friendlist(Sys.IntPtr tox);

		public Sys.UInt32 ToxFriendNum()
		{
			if (tox == Sys.IntPtr.Zero)
				return 0;

			Sys.UInt32 rc = 0;
			toxmutex.WaitOne();
			try
			{
				rc = tox_count_friendlist(tox);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

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
			try
			{
				_id = ToxSelfIDInternal(tox);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return _id;
		}

		[SRIOp.DllImport("toxcore")]
		private static extern void tox_get_address(Sys.IntPtr tox, byte[] address);

		protected string ToxSelfIDInternal(Sys.IntPtr tox)
		{
			byte[] addrbin = new byte[ID_LEN_BINARY];
			tox_get_address(tox, addrbin);

			ToxKey key = new ToxKey(addrbin);
			return key.str;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_add_friend(Sys.IntPtr tox, byte[] friendIdbin, byte[] messagebin, Sys.UInt16 length);

		public int ToxFriendAdd(ToxKey friendkey, string messagestr)
		{
			byte[] messagebin = System.Text.Encoding.UTF8.GetBytes(messagestr + '\0');
			int rc = -1;

			toxmutex.WaitOne();
			try
			{
				rc = tox_add_friend(tox, friendkey.bin, messagebin, (Sys.UInt16)messagebin.Length);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_add_friend_norequest(Sys.IntPtr tox, byte[] friendId);

		public int ToxFriendAddNoRequest(ToxKey friendkey)
		{
			if (friendkey.bin.Length != ID_LEN_BINARY)
				return -1;

			int rc = -1;
			toxmutex.WaitOne();
			try
			{
				rc = tox_add_friend_norequest(tox, friendkey.bin);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			if (rc >= 0)
				ToxFriendInit(rc);

			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_get_friend_id(Sys.IntPtr tox, byte[] client_id);

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_del_friend(Sys.IntPtr tox, int friendnumber);

		public int ToxFriendDel(ToxKey friendkey)
		{
			if (friendkey.bin.Length != ID_LEN_BINARY)
				return -1;

			int rc = -1;
			toxmutex.WaitOne();
			try
			{
				rc = tox_get_friend_id(tox, friendkey.bin);
				if (rc >= 0)
					rc = tox_del_friend(tox, rc);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern Sys.UInt32 tox_send_message(Sys.IntPtr tox, int friendnumber, byte[] message, Sys.UInt32 length);

		public Sys.UInt32 ToxFriendMessage(Sys.UInt16 id, string messagestr)
		{
			// NOT null-terminated, is that a problem? Yes.
			byte[] messagebin = System.Text.Encoding.UTF8.GetBytes(messagestr + '\0');

			Sys.UInt32 rc = 0;
			toxmutex.WaitOne();
			try
			{
				rc = tox_send_message(tox, id, messagebin, (Sys.UInt32)messagebin.Length);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}
			
			return rc;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_send_action(Sys.IntPtr tox, int friendnumber, byte[] action, Sys.UInt32 length);

		public int ToxFriendAction(Sys.UInt16 id, string actionstr)
		{
			// NOT null-terminated, is that a problem? Yes.
			byte[] actionbin = System.Text.Encoding.UTF8.GetBytes(actionstr + '\0');

			int rc = -1;
			toxmutex.WaitOne();
			try
			{
				rc = tox_send_action(tox, id, actionbin, (Sys.UInt32)actionbin.Length);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

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
			try
			{
				id = tox_add_groupchat(tox);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return id >= 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_del_groupchat(Sys.IntPtr tox, int groupnumber);

		public bool ToxGroupchatDel(int groupnumber)
		{
			int rc = -1;
			toxmutex.WaitOne();
			try
			{
				rc = tox_del_groupchat(tox, groupnumber);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return rc == 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_group_peername(Sys.IntPtr tox, int groupnumber, int peernumber, byte[] name);

		public bool ToxGroupchatPeername(Sys.UInt16 groupnumber, Sys.UInt16 peernumber, out string namestr)
		{
			byte[] namebin = new byte[NAME_LEN + 1];
			int rc = -1;

			toxmutex.WaitOne();
			try
			{
				rc = tox_group_peername(tox, groupnumber, peernumber, namebin);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			namestr = "";
			if (rc > 0)
				namestr = CutAtNul(System.Text.Encoding.UTF8.GetString(namebin));

			return rc >= 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_invite_friend(Sys.IntPtr tox, int friendnumber, int groupnumber);

		public bool ToxGroupchatInvite(int groupnumber, ToxKey friendkey)
		{
			int rc = -1;
			toxmutex.WaitOne();
			try
			{
				int friendnumber = tox_get_friend_id(tox, friendkey.bin);
				if (friendnumber >= 0)
					rc = tox_invite_friend(tox, friendnumber, groupnumber);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return rc == 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_join_groupchat(Sys.IntPtr tox, int friendnumber, byte[] friend_groupkey);

		public bool ToxGroupchatJoin(int friendnumber, ToxKey friend_groupkey, out int groupnumber)
		{
			toxmutex.WaitOne();
			try
			{
				groupnumber = tox_join_groupchat(tox, friendnumber, friend_groupkey.bin);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return groupnumber >= 0;
		}


		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_group_message_send(Sys.IntPtr tox, int groupnumber, byte[] messagebin, Sys.UInt32 length);

		public bool ToxGroupchatMessage(int groupnumber, string messagestr)
		{
			byte[] messagebin = System.Text.Encoding.UTF8.GetBytes(messagestr + '\0');
			int rc = -1;

			toxmutex.WaitOne();
			try
			{
				rc = tox_group_message_send(tox, groupnumber, messagebin, (Sys.UInt32)messagebin.Length);
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

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
		 * void tox_callback_connection_status(Tox *tox, void(*function)(Tox *tox, int, uint8_t, void *), void *userdata);
		 */
		protected delegate void CallBackDelegateFriendConnectionStatus(Sys.IntPtr tox, int friendId, byte state, Sys.IntPtr X);
		protected CallBackDelegateFriendConnectionStatus cbfriendconnectionstatus;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_connection_status(Sys.IntPtr tox, CallBackDelegateFriendConnectionStatus cbfriendconnectionstatus, Sys.IntPtr X);

		protected void ToxCallbackFriendConnectionStatus(Sys.IntPtr tox, int id, byte state, Sys.IntPtr X)
		{
			if (cbfriend != null)
				cbfriend.ToxFriendConnected(id, state != 0);
		}

		/*
		 * void callback(uint8_t *public_key, uint8_t *data, uint16_t length)
		 * 
		 * void tox_callback_friend_request(Tox *tox, void(*function)(uint8_t *, uint8_t *, uint16_t, void *), void *userdata);
		 */
		
		protected delegate void CallBackDelegateFriendAddRequest([SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeConst = 38)] byte[] key, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] message, Sys.UInt16 length, Sys.IntPtr tox);
		protected CallBackDelegateFriendAddRequest cbfriendaddrequest;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_friend_request(Sys.IntPtr tox, CallBackDelegateFriendAddRequest cbfriendaddrequest, Sys.IntPtr X);

		protected void ToxCallbackFriendAddRequest(byte[] keybinary, byte[] message, Sys.UInt16 length, Sys.IntPtr X)
		{
			if (cbfriend != null)
			{
				ToxKey key = new ToxKey(keybinary);
				cbfriend.ToxFriendAddRequest(key, CutAtNul(System.Text.Encoding.UTF8.GetString(message, 0, length - 1)));
			}
		}

		/*
		 * void callback(Tox *tox, int friendid, uint8_t *data, uint16_t length, void *userdata)
		 * 
		 * void tox_callback_friend_message(Tox *tox,
		 * 								   void (*function)(Tox *tox, int, uint8_t *, uint16_t, void *),
		 * 								   void *userdata);
		 */
		
		protected delegate void CallBackDelegateFriendMessage(Sys.IntPtr tox, int friendid, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] message, Sys.UInt16 length, Sys.IntPtr X);
		protected CallBackDelegateFriendMessage cbfriendmessage;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_friend_message(Sys.IntPtr tox, CallBackDelegateFriendMessage cbfriendmessage, Sys.IntPtr X);

		protected void ToxCallbackFriendMessage(Sys.IntPtr tox, int id, byte[] message, Sys.UInt16 length, Sys.IntPtr X)
		{
			if (cbfriend != null)
				cbfriend.ToxFriendMessage(id, CutAtNul(System.Text.Encoding.UTF8.GetString(message, 0, length - 1)));
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
			if (cbfriend != null)
				cbfriend.ToxFriendAction(id, CutAtNul(System.Text.Encoding.UTF8.GetString(action, 0, length - 1)));
		}

		/*
		 * void callback(Tox *tox, int friendid, uint8_t *data, uint16_t length, void *userdata)
		 * 
		 * void tox_callback_name_change(Tox *tox,
		 * 								void (*function)(Tox *tox, int, uint8_t *, uint16_t, void *),
		 * 								void *userdata);
		 */
		
		protected delegate void CallBackDelegateFriendName(Sys.IntPtr tox, int id, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] name, Sys.UInt16 length, Sys.IntPtr X);
		protected CallBackDelegateFriendName cbfriendname;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_name_change(Sys.IntPtr tox, CallBackDelegateFriendName cbfriendname, Sys.IntPtr X);

		protected void ToxCallbackFriendName(Sys.IntPtr tox, int id, byte[] name, Sys.UInt16 length, Sys.IntPtr X)
		{
			if (cbfriend != null)
				cbfriend.ToxFriendName(id, CutAtNul(System.Text.Encoding.UTF8.GetString(name, 0, length - 1)));
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
			if (cbgroup != null)
			{
				byte[] namebin = new byte[NAME_LEN + 1];

				// we're already under lock here, as this callback happens inside tox_do()
				tox_get_name(tox, friendnumber, namebin);

				ToxKey friend_groupkey = new ToxKey(friend_groupkeybin);
				cbgroup.ToxGroupchatInvite(friendnumber, ToString(namebin), friend_groupkey);
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
			if (cbgroup != null)
				cbgroup.ToxGroupchatMessage(groupnumber, friendgroupnumber, CutAtNul(System.Text.Encoding.UTF8.GetString(message, 0, length - 1)));
		}

		/*
		 * void callback(Tox *tox, int groupnumber, int peernumber, TOX_CHAT_CHANGE change, void *userdata);
		 *
		 * typedef enum {
		 *     TOX_CHAT_CHANGE_PEER_ADD,
		 *     TOX_CHAT_CHANGE_PEER_DEL,
		 *     TOX_CHAT_CHANGE_PEER_NAME,
		 * } TOX_CHAT_CHANGE;
		 * void tox_callback_group_namelist_change(Tox *tox,
		 *                                      void (*function)(Tox *tox, int, int, uint8_t, void *),
		 *                                      void *userdata);
		 */

		protected delegate void CallBackDelegateGroupNamelistChange(Sys.IntPtr tox, int groupnumber, int peernumber, byte change, Sys.IntPtr X);
		protected CallBackDelegateGroupNamelistChange cbgroupnamelistchange;

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern void tox_callback_group_namelist_change(Sys.IntPtr tox, CallBackDelegateGroupNamelistChange cbgroupnamelistchange, Sys.IntPtr X);

		protected void ToxCallbackGroupNamelistChange(Sys.IntPtr tox, int groupnumber, int peernumber, byte change, Sys.IntPtr X)
		{
			// MainClass.PrintDebug("Group::Namelist::Change(" + groupnumber + "." + peernumber + " => " + change + ")");
			if (cbgroup != null)
			{
				ToxGroupNamelistChangeType changetype = ToxGroupNamelistChangeType.Unknown;
				try
				{
					changetype = (ToxGroupNamelistChangeType)change;
				}
				catch
				{
					changetype = ToxGroupNamelistChangeType.Unknown;
				}

				cbgroup.ToxGroupNamelistChange(groupnumber, peernumber, changetype);
			}
		}

/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/

		protected void ToxCallbackInit()
		{
			toxmutex.WaitOne();
			try
			{
				ToxCallbackInitInternal();
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}
		}

		protected void ToxCallbackInitInternal()
		{
			cbfriendconnectionstatus = new CallBackDelegateFriendConnectionStatus(ToxCallbackFriendConnectionStatus);
			tox_callback_connection_status(tox, cbfriendconnectionstatus, Sys.IntPtr.Zero);


			cbfriendaddrequest = new CallBackDelegateFriendAddRequest(ToxCallbackFriendAddRequest);
			tox_callback_friend_request(tox, cbfriendaddrequest, Sys.IntPtr.Zero);


			cbfriendmessage = new CallBackDelegateFriendMessage(ToxCallbackFriendMessage);
			tox_callback_friend_message(tox, cbfriendmessage, Sys.IntPtr.Zero);


			cbfriendaction = new CallBackDelegateFriendAction(ToxCallbackFriendAction);
			tox_callback_action(tox, cbfriendaction, Sys.IntPtr.Zero);


			cbfriendname = new CallBackDelegateFriendName(ToxCallbackFriendName);
			tox_callback_name_change(tox, cbfriendname, Sys.IntPtr.Zero);

/*****************************************************************************/

			cbgroupchatinvite = new CallBackDelegateGroupchatInvite(ToxCallbackGroupchatInvite);
			tox_callback_group_invite(tox, cbgroupchatinvite, Sys.IntPtr.Zero);


			cbgroupchatmessage = new CallBackDelegateGroupchatMessage(ToxCallbackGroupchatMessage);
			tox_callback_group_message(tox, cbgroupchatmessage, Sys.IntPtr.Zero);

			cbgroupnamelistchange = new CallBackDelegateGroupNamelistChange(ToxCallbackGroupNamelistChange);
			tox_callback_group_namelist_change(tox, cbgroupnamelistchange, Sys.IntPtr.Zero);
		}

/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/

		// int tox_rendezvous(Tox *tox, char *secret, uint64_t at,
		//          void (*found)(void *user data, uint8_t *friend_address),
        //          uint8_t (*timeout)(void *userdata), void *userdata);

		protected delegate void CallBackDelegateRendezVousFound(Sys.IntPtr X, [SRIOp.MarshalAs(SRIOp.UnmanagedType.LPArray, SizeConst = 38)] byte[] friendaddress);
		protected CallBackDelegateRendezVousFound cbrendezvousfound;

		protected void ToxRendezvousFound(Sys.IntPtr X, byte[] friendaddressbin)
		{
			if (cbrendezvous != null)
			{
				ToxKey friendaddresskey = new ToxKey(friendaddressbin);
				cbrendezvous.ToxRendezvousFound(publishid, friendaddresskey);
			}
		}

		protected delegate byte CallBackDelegateRendezTimeout(Sys.IntPtr X);
		protected CallBackDelegateRendezTimeout cbrendezvoustimeout;

		protected byte ToxRendezvousTimeout(Sys.IntPtr X)
		{
			byte retval = 0;

			if (cbrendezvous != null)
				retval = cbrendezvous.ToxRendezvousTimeout(publishid);

			if (retval == 0)
				publishid = 0;

			return retval;
		}

		[SRIOp.DllImport("toxcore", CallingConvention = SRIOp.CallingConvention.Cdecl)]
		private static extern int tox_rendezvous(Sys.IntPtr tox, byte[] secret, Sys.UInt64 time, CallBackDelegateRendezVousFound cbrendezvousfound,
		                                         CallBackDelegateRendezTimeout cbrendezvoustimeout, Sys.IntPtr X);

		protected static Sys.UInt64 ToUnixTime(Sys.DateTime date)
	    {
	        Sys.DateTime epoch = new Sys.DateTime(1970, 1, 1, 0, 0, 0, Sys.DateTimeKind.Utc);
	        return Sys.Convert.ToUInt64((date.ToUniversalTime() - epoch).TotalSeconds);
	    }

		protected ushort publishid = 0;

		public int ToxPublish(ushort ID, string secretstr, Sys.DateTime datetime)
		{
			if (ID == 0)
				return -3;

			if (publishid != 0)
				if (publishid != ID)
					return -4;

			if (cbrendezvousfound == null)
				cbrendezvousfound = new CallBackDelegateRendezVousFound(ToxRendezvousFound);
			if (cbrendezvoustimeout == null)
				cbrendezvoustimeout = new CallBackDelegateRendezTimeout(ToxRendezvousTimeout);

			byte[] secretbin = System.Text.Encoding.UTF8.GetBytes(secretstr + '\0');
			Sys.UInt64 unixtime = ToUnixTime(datetime);
			Sys.IntPtr X = new Sys.IntPtr();
			int res = -1;

			try
			{
				toxmutex.WaitOne();
				res = tox_rendezvous(tox, secretbin, unixtime, cbrendezvousfound, cbrendezvoustimeout, X);
				if (res > 0)
					publishid = ID;
			}
			catch (Sys.EntryPointNotFoundException)
			{
				res = -2;
			}
			finally
			{
				toxmutex.ReleaseMutex();
			}

			return res;
		}

/*****************************************************************************/
/*****************************************************************************/
/*****************************************************************************/

		protected static string CutAtNul(string data)
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
