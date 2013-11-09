
using System;

namespace ToxSharpGTK
{
	public partial class GtkInputTwoLines : Gtk.Dialog
	{
		public GtkInputTwoLines()
		{
			this.Build();
		}

		public bool Do(string message, string name1, string name2, out string input1, out string input2)
		{
			this.message.Text = message;
			this.name1.Text = name1;
			this.name2.Text = name2;
			this.input1.Text = "";
			this.input2.Text = "";

			Focus = this.input1;
			DefaultResponse = Gtk.ResponseType.Ok;
			
			// -5: Ok
			// -6: Cancel
			int res = Run();
			Hide();

			input1 = this.input1.Text;
			input2 = this.input2.Text;
			return res == -5;
		}
	}
}
