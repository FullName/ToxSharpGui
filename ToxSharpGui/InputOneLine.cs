
using System;

namespace ToxSharpGTK
{
	public partial class InputOneLine : Gtk.Dialog
	{
		public InputOneLine()
		{
			this.Build();
		}

		public bool Do(string output, out string input)
		{
			label1.Text = output;
			entry1.Text = "";
			Focus = entry1;
			DefaultResponse = Gtk.ResponseType.Ok;
			
			// -5: Ok
			// -6: Cancel
			int res = Run();
			Hide();

			input = entry1.Text;
			return res == -5;
		}
	}
}

