using System.Windows;
using x360ce.Engine;

namespace JocysCom.x360ce.RemoteController
{
	/// <summary>
	/// Interaction logic for HostWindow.xaml
	/// </summary>
	public partial class HostWindow : Window
	{
		public HostWindow()
		{
			InitializeComponent ();
			Title = new JocysCom.ClassLibrary.Configuration.AssemblyInfo().GetTitle(false, false, false, false, false, 0) + "- Options";
			LoadSettings();
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			SaveSettings();
			DialogResult = true;
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}

		void LoadSettings()
		{
			LocalPortTextBox.Text = Properties.Settings.Default.ComputerPort.ToString();
			LoginPasswordBox.Password = Properties.Settings.Default.LoginPassword;
			var index = (MapToMask)Properties.Settings.Default.ControllerIndex;
			Controller1CheckBox.IsChecked = index.HasFlag(MapToMask.Controller1);
			Controller2CheckBox.IsChecked = index.HasFlag(MapToMask.Controller2);
			Controller3CheckBox.IsChecked = index.HasFlag(MapToMask.Controller3);
			Controller4CheckBox.IsChecked = index.HasFlag(MapToMask.Controller4);
		}

		void SaveSettings()
		{
			int port;
			Properties.Settings.Default.ComputerPort = int.TryParse(LocalPortTextBox.Text, out port)
				? port : 26010;
			Properties.Settings.Default.LoginPassword = LoginPasswordBox.Password.ToString();
			var index = MapToMask.None;
			if (Controller1CheckBox.IsChecked == true)
				index |= MapToMask.Controller1;
			if (Controller2CheckBox.IsChecked == true)
				index |= MapToMask.Controller2;
			if (Controller3CheckBox.IsChecked == true)
				index |= MapToMask.Controller2;
			if (Controller4CheckBox.IsChecked == true)
				index |= MapToMask.Controller4;
			Properties.Settings.Default.ControllerIndex = (int)index;
			Properties.Settings.Default.Save();
		}

	}
}
