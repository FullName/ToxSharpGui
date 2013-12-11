
using System;

namespace ToxSharpBasic
{
	internal class InputHandling
	{
		private char[] splitter;
		protected ToxInterface toxsharp;
		protected Interfaces.IUIReactions uireactions;
		protected Interfaces.IDataReactions datareactions;
		protected IToxGlue toxglue;

		public InputHandling(ToxInterface toxsharp, Interfaces.IUIReactions uireactions, Interfaces.IDataReactions datareactions, IToxGlue toxglue)
		{
			splitter = new char[1];
			splitter[0] =  ' ';

			this.toxsharp = toxsharp;
			this.uireactions = uireactions;
			this.datareactions = datareactions;
			this.toxglue = toxglue;
		}

		protected void TextAdd(Interfaces.SourceType type, UInt32 id, string source, string text)
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
				toxsharp.ToxFriendAction(friend.ids(), actionstr);
	
				return 1;
			}
	
			if ((len > 2) && (text.Substring(0, 3) == "/fm"))
			{
				if (toxsharp.ToxFriendMessage(friend.ids(), actionstr) != 0)
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
			text = text.Substring(2);
			if (text.Length == 0)
				return 0;

			int res = 0;

			// ailmn
			char letter = text.ToLower()[0];
			switch(letter)
			{
				case 'a': // accept invitiation #
					string[] cmd_invite = text.Split(splitter, 2);
					if (cmd_invite.Length < 2)
					{
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Not enough arguments for command.");
						break;
					}

					UInt16 invitenumber;
					if (!UInt16.TryParse(cmd_invite[1], System.Globalization.NumberStyles.Number, null, out invitenumber))
					{
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Failed to parse group number.");
						break;
					}

					res = toxglue.GroupchatInviteaccept(invitenumber) ? 1 : -1;
					break;

				case 'i': // invite # friend
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Command not implemented.");
					res = -1;
					break;

				case 'l': // leave #
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Command not implemented.");
					res = -1;
					break;

				case 'm': // message #
					string[] cmd_num_message = text.Split(splitter, 3);
					if (cmd_num_message.Length < 3)
					{
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Not enough arguments for command.");
						break;
					}

					UInt16 groupnumber;
					if (!UInt16.TryParse(cmd_num_message[1], System.Globalization.NumberStyles.Number, null, out groupnumber))
					{
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Failed to parse group number.");
						break;
					}

					res = toxglue.GroupchatMessage(groupnumber, cmd_num_message[2]) ? 1 : -1;
					break;

				case 'n': // new
					res = (toxglue.GroupchatAdd() ? 1 : - 1);
					break;

				default:
					break;
			}

			if (res == 0)
				TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Command not recognized.");

			return res;
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

				return toxglue.RendezvousCreateOrUpdate(text, DateTime.Now);
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
					uireactions.TitleUpdate(namestr, toxsharp.ToxSelfID());
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
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/fr(emove) <ID>          : Removes the given ID from the list of friends.");

						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/fm(essage) <name or ID> : Sends a message to the given name or ID.");
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/fd(o) <name or ID>      : Sends an action to the given name.");

						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "(IN TEST) Name or ID can be partial as long as it expands uniquely.");
					}

					if (text.Substring(extra + 1, 1) == "g")
					{
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "TODO: Implement these commands.");

						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/gn(ew)                           : Add a new group.");
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/gl(eave) <group #>               : Leave a group.");
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/gi(nvite) <group #> <name or ID> : Invite a friend to a group.");
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/ga(ccept) <invitation #>                   : Accept a group invitation.");


						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "/gm(essage) <group #>             : Sends a message to the given group.");

						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "(TODO) Name or ID can be partial as long as it expands uniquely.");
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
				UInt32 id32;
				if (!uireactions.CurrentTypeID(out type, out id32))
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "No target for a message on this page. Try '/h' for help.");
				else if (id32 > UInt16.MaxValue)
					TextAdd(Interfaces.SourceType.System, 0, "DEBUG", "Oops. How did you get here?");
				else
				{
					UInt16 id = (UInt16)id32;
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
						handled = toxglue.GroupchatMessage(id, text) ? 1 : -1;
					else
						TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "Internal error. Sorry!");
				}
			}

			return (handled == 1);
		}

		public bool Do(string text, Interfaces.InputKey key)
		{
			switch(key) {
				case Interfaces.InputKey.Up:
				case Interfaces.InputKey.Down:
					// Combobox, keeping the current input unless a different is selected
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "TODO: Command history.");
					return false;
				case Interfaces.InputKey.Tab:
					// Combobox, popping friends, strangers or groups depending on input
					TextAdd(Interfaces.SourceType.System, 0, "SYSTEM", "TODO: Support input on entering an ID.");
					return false;
				case Interfaces.InputKey.Return:
					return InputHandle(text);
				default:
					return false;
			}
		}
	}
}
