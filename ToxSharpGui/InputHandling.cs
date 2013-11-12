
using System;

namespace ToxSharpBasic
{
	public enum InputKey { None, Up, Down, Tab, Return };

	public class InputHandling
	{
		protected ToxInterface toxsharp;
		protected Interfaces.IUIReactions uireactions;
		protected Interfaces.IDataReactions datareactions;

		public InputHandling(ToxInterface toxsharp, Interfaces.IUIReactions uireactions, Interfaces.IDataReactions datareactions)
		{
			this.toxsharp = toxsharp;
			this.uireactions = uireactions;
			this.datareactions = datareactions;
		}

		protected void TextAdd(Interfaces.SourceType type, UInt16 id, string source, string text)
		{
			uireactions.TextAdd(type, id, source, text);
		}

		protected int CommandFriendHandle(string text)
		{
			int len = text.Length;

			int space1 = text.IndexOf(' ');
			if (space1 <= 0)
			{
				TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Command requires a name or ID.");
				return -1;
			}

			if ((len > 2) && (text.Substring(0, 3) == "/fa"))
			{
				string ID, message = "";

				int spaceagain = text.IndexOf(' ', space1 + 1);
				if (spaceagain > 0)
				{
					ID = text.Substring(space1 + 1, spaceagain - (space1 + 1));
					message = text.Substring(spaceagain);
				}
				else
					ID = text.Substring(space1 + 1);

				if (ID.Length != 2 * ToxInterface.ID_LEN_BINARY)
				{
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/fa(dd) <ID>: ID must be exactly " +
					        2 * ToxInterface.ID_LEN_BINARY + " characters long. (Your input's ID was " + ID.Length + "characters long.)");
					return -1;
				}

				ToxKey key = new ToxKey(ID);
				int friendid = toxsharp.ToxFriendAdd(key, message);
				if (friendid < 0)
				{
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Command wasn't successful: " + friendid);
					return -1;
				}
	
				if (message.Length > 0)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Friend request sent:\n" +
																	   "Message: \"" + message + "\n" +
																	   "ID: " + ID);
				else
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Friend request sent to: " + ID);
	
				toxsharp.ToxFriendInit(friendid);
				return 1;
			}
	
			if ((len > 2) && (text.Substring(0, 3) == "/fr"))
			{
				string keypartial = text.Substring(space1 + 1);
				FriendTreeNode friend2delete = null;
				int candidates2deletenum = datareactions.FindFriendsWithKeyStartingWithID(keypartial, out friend2delete);
				if (candidates2deletenum == 1)
				{
					int code = toxsharp.ToxFriendDel(friend2delete.key);
					if (code != 0)
					{
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Command wasn't successful: " + code);
						return -1;
					}

					datareactions.Delete(friend2delete);
					uireactions.TreeDel(friend2delete);
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "No longer a friend of yours: " + friend2delete.name + "\n" + friend2delete.key.str);

					return 1;
				}
				else if (candidates2deletenum == 0)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "The given ID wasn't found among your friends.");
				else if (candidates2deletenum > 1)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "ID fragment fits to more than one friend.");
				else if (candidates2deletenum < 0)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");

				return -1;
			}

			int space2 = text.IndexOf(' ', space1 + 1);
			string actionstr = text.Substring(space2 + 1);
			if ((space1 <= 0) || (space2 <= 0) || (actionstr.Length == 0))
			{
				TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Not enough arguments: Need a target (name or ID) and additional text.");
				return -1;
			}
	
			string nameorkeypartial = text.Substring(space1 + 1, space2 - space1 - 1);
			FriendTreeNode friend = null;
			int foundnum = datareactions.FindFriendsWithNameOrKeyStartingWithID(nameorkeypartial, out friend);
			if (foundnum != 1)
			{
				if (foundnum == 0)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "The intended audience wasn't found among your friends.");
				else if (foundnum > 1)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "The name/ID fits to more than one friend.");
				else if (foundnum < 0)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");

				return -1;
			}

			if ((len > 2) && (text.Substring(0, 3) == "/fd"))
			{
				TextAdd(Interfaces.SourceType.Friend, friend.id, "ACTION", toxsharp.ToxNameGet() + " " + actionstr);
				toxsharp.ToxFriendAction(friend.id, actionstr);
	
				return 1;
			}
	
			if ((len > 2) && (text.Substring(0, 3) == "/fm"))
			{
				if (toxsharp.ToxFriendMessage(friend.id, actionstr) != 0)
				{
					TextAdd(Interfaces.SourceType.Friend, friend.id, toxsharp.ToxNameGet(), actionstr);
					return 1;
				}
				else
				{
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Failed to queue the message. Sorry.");
					return -1;
				}
			}
	
			return 0;
		}
	
		protected int CommandGroupHandle(string text)
		{
			TextAdd(Interfaces.SourceType.System, 0, "DEBUG", "TODO: Group commands not implemented.");
			return 0;
		}

		protected int CommandHandle(string text)
		{
			int len = text.Length;
			if ((len > 1) && (text.Substring(0, 2) == "/i"))
			{
				string id = toxsharp.ToxSelfID();
				uireactions.ClipboardSend(id);
				TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Your id has been copied into the clipboard:\n" + id);
				return 1;
			}

			if ((len > 1) && (text.Substring(0, 2) == "/r"))
			{
				if (len < 16)
				{
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Command allows a time (format: @HH:MM, e.g. @03:05) and requires a text of at least 16 characters.\n");
					return 0;
				}

				char[] separator = new char[1];
				separator[0] = ' ';
				string[] parts = text.Split(separator, 3);
				if (parts.Length < 2)
				{
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Command not recognized.");
					return 0;
				}

				if (parts[1].Substring(0, 1) == "@")
				{
					TextAdd(Interfaces.SourceType.System, 0, "DEBUG", "Rendezvous: Timestamp not yet implemented, skipping.");
					text = parts[2];
				}
				else
					text = parts[1] + " " + parts[2];

				RendezvousTreeNode rendezvous = datareactions.FindRendezvous(text);
				// TODO: rendezvous ID
				int res = toxsharp.ToxPublish(new IntPtr(312), text, DateTime.Now);
				if (res > 0)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Set up successfully.\n");
				else if (res == 0)
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Failed to set up due to invalid input.\n");
				else if (res < 0)
				{
					if (res == -2)
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Failed to set up, function missing.\n");
					else
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Rendezvous: Failed to set up for unknown reason.\n");
				}

				return res;
			}

			if ((len > 1) && (text.Substring(0, 2) =="/n"))
			{
				int space = text.IndexOf(' ');
				if (space <= 0)
				{
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/name <name>: No name given.");
					return -1;
				}

				string namestr = text.Substring(space + 1);
				if (namestr.Length == 0)
				{
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/name <name>: No name given.");
					return -1;
				}

				if (toxsharp.ToxNameSet(namestr) == 1)
				{
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Your name is now " + namestr + ".");
					uireactions.TitleUpdate();
					return 1;
				}

				TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");
				return -1;
			}

			if ((len > 1) && (text.Substring(0, 2) == "/h"))
			{
				int extra = text.IndexOf(' ');
				if (extra > 0)
				{
					if (text.Substring(extra + 1, 1) == "f")
					{
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/fa(dd) <ID>             : Sends a friend request to the given ID.");
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/fr(emove) <ID>  : Removes the given ID from the list of friends.");
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/fm(essage) <name or ID> : Sends a message to the given name or ID.");
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/fd(o) <name or ID>      : Sends an action to the given name.");
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "(TODO) Name or ID can be partial as long as it expands uniquely.");
					}
					if (text.Substring(extra + 1, 1) == "g")
					{
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "TODO: Help for this context.");
					}
				}
				else
				{
					string message = "Tox# GUI 0.0.1: Commands start with a slash. (On the main page only commands can be entered.)\n" +
						"On any other page, any input but an 'action' will be sent as typed to the target audience.\n" +
						"/h(elp)           : this help\n" +
						"/q(uit)           : exit the program\n" +
						"/i(d)             : copies your ID to the clipboard.\n" +
						"/n(ame) ...       : sets your name\n" +
						"/a(m) <X>         : sets you to one of 'here', 'away', busy'\n" +
						"/s(tate) ...      : sets your state (any text, e.g. 'amused')\n" +
						"/d(o) ...         : sends an action to the current conversation partner\n" +
						"/r(endezvous) ... : sets up a rendezvous\n" +
						"/h(elp) f(riends) : commands related to friends\n" +
						"/h(elp) g(roups)  : commands related to groups";
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", message);
				}

				return 1;
			}

			if ((len > 1) && (text.Substring(0, 2) =="/q"))
			{
				TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Preparing to shut down...");
				uireactions.Quit();
				return 1;   // not reached?
			}

			int handled = 0;
			if (len > 1)
			{
				if (text.Substring(0, 2) == "/f")
					handled = CommandFriendHandle(text);
				if (text.Substring(0, 2) == "/g")
					handled = CommandGroupHandle(text);
			}

			if (handled == 0)
				TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Command not recognized.");
	
			return handled;
		}
	
		protected bool InputHandle(string text)
		{
			if (text.Length == 0)
				return false;
	
			int handled = 0;
			bool slash = text[0] == '/';
			bool action = false;
			if (slash)
				action = (text.Length > 4) && (text.Substring(0, 4) == "/do ");

			if (slash && !action)
				handled = CommandHandle(text);
			else
			{
				// send to target
				Interfaces.SourceType type;
				UInt16 id;
				if (!uireactions.CurrentTypeID(out type, out id))
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "No target for a message on this page. Try '/h' for help.");
				else
				{
					if (type == Interfaces.SourceType.Friend)
					{
						if (action)
						{
							handled = 1;

							string actionstr = text.Substring(4);
							TextAdd(type, id, "ACTION", toxsharp.ToxNameGet() + " " + actionstr);
							toxsharp.ToxFriendAction(id, actionstr);
						}
						else
						{
							if (toxsharp.ToxFriendMessage(id, text) != 0)
							{
								TextAdd(type, id, toxsharp.ToxNameGet(), text);
								handled = 1;
							}
							else
							{
								TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Failed to queue the message. Sorry.");
								handled = -1;
							}
						}
					}
					else if (type == Interfaces.SourceType.Group)
					{
						if (toxsharp.ToxGroupchatMessage(id, text))
						{
							TextAdd(Interfaces.SourceType.Group, id, toxsharp.ToxNameGet() + " => #" + id, text);
							handled = 1;
						}
						else
						{
							TextAdd(Interfaces.SourceType.Group, id, "SYSTEM", "Failed to send message to group.");
							handled = -1;
						}
					}
					else
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");
				}
			}

			return (handled == 1);
		}

		public bool Do(string text, InputKey key)
		{
			switch(key) {
				case InputKey.Up:
				case InputKey.Down:
					// Combobox, keeping the current input unless a different is selected
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "TODO: Command history.");
					return false;
				case InputKey.Tab:
					// Combobox, popping friends, strangers or groups depending on input
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "TODO: Support input on entering an ID.");
					return false;
				case InputKey.Return:
					return InputHandle(text);
				default:
					return false;
			}
		}
	}
}
