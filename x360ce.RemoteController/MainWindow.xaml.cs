using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Threading;
using JocysCom.ClassLibrary.Data;
using System.ComponentModel;

namespace JocysCom.x360ce.RemoteController
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		/****************************************************************************
		 * PUBLIC VARIABLES
		 ****************************************************************************/
		public State currentState { get; private set; }
		public ushort localPort { get; private set; }
		public bool IsRunning { get; private set; }





		/****************************************************************************
		 * PRIVATE VARIABLES
		 ****************************************************************************/
		Thread serverThread;
		private TcpListener tcpServer { get; set; }
		JocysCom.ClassLibrary.HiResTimer _timer;
		JocysCom.ClassLibrary.Data.TlvSerializer _Serializer;
		object timerLock = new object();
		public bool Suspended;
		public Exception LastException = null;
		TcpClient tcpClient;
		bool serverNotExit;





		/****************************************************************************
		 * PUBLIC METHODS
		 ****************************************************************************/
		public MainWindow()
		{
			InitializeComponent();
			Title = new JocysCom.ClassLibrary.Configuration.AssemblyInfo().GetTitle();
		}





		/****************************************************************************
		 * PRIVATE METHODS
		 ****************************************************************************/
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			init_gamepad ();
			init_tcp ();
			if (Properties.Settings.Default.AutoConnect)
			{
				ClientStart ();
			}
		}
		private void MainWindow_Closing (object sender, CancelEventArgs e)
		{
			ClientStop ();
			ServerStop ();
		}





			#region Gamepad
			#region Graphics
			Dictionary<Shape, GamepadButtonFlags> list;
		Dictionary<GamepadButtonFlags, Shape> list_r;
		// METHODS
		private void init_gamepad ()
		{
			list = new Dictionary<Shape, GamepadButtonFlags> ();
			list.Add (LeftShoulderRectangle, GamepadButtonFlags.LeftShoulder);
			list.Add (RightShoulderRectangle, GamepadButtonFlags.RightShoulder);
			//list.Add(LeftTriggerRectangle, GamepadButtonFlags.);
			//list.Add(RightTriggerRectangle);
			list.Add (LeftThumbEllipse, GamepadButtonFlags.LeftThumb);
			list.Add (RightThumbEllipse, GamepadButtonFlags.RightThumb);
			list.Add (DPadUpEllipse, GamepadButtonFlags.DPadUp);
			list.Add (DPadRightEllipse, GamepadButtonFlags.DPadRight);
			list.Add (DPadDownEllipse, GamepadButtonFlags.DPadDown);
			list.Add (DPadLeftEllipse, GamepadButtonFlags.DPadLeft);
			list.Add (ButtonBackEllipse, GamepadButtonFlags.Back);
			list.Add (ButtonStartEllipse, GamepadButtonFlags.Start);
			list.Add (ButtonAEllipse, GamepadButtonFlags.A);
			list.Add (ButtonYEllipse, GamepadButtonFlags.Y);
			list.Add (ButtonXEllipse, GamepadButtonFlags.X);
			list.Add (ButtonBEllipse, GamepadButtonFlags.B);
			foreach (var key in list.Keys)
			{
				key.TouchDown += Shape_Down;
				key.MouseDown += Shape_Down;
				key.TouchUp += Shape_Up;
				key.MouseUp += Shape_Up;
			}

			list_r = new Dictionary <GamepadButtonFlags, Shape> ();
			foreach (var kv in list)
			{
				list_r.Add (kv.Value, kv.Key);
			}
		}

		private void Shape_Up (object sender, EventArgs e)
		{
			var s = (Shape)sender;
			s.Fill = (Brush) FindResource ("TouchUpBrush");
			currentState = currentState.RemoveButton (list [s]);
		}

		private void Shape_Down (object sender, EventArgs e)
		{
			var s = (Shape)sender;
			s.Fill = (Brush) FindResource ("TouchDownBrush");
			currentState = currentState.AddButton (list [s]);
		}

		private void shapeUpdate ()
		{
			var a = EnumUtil.GetValues<GamepadButtonFlags> ();
			foreach (var button in EnumUtil.GetValues<GamepadButtonFlags> ())
			{
				if (list_r.ContainsKey (button))
				{
					if (currentState.ButtonIsPressed (button))
					{
						list_r [button].Fill = (Brush) FindResource ("TouchDownBrush");
					}
					else
					{
						list_r [button].Fill = (Brush) FindResource ("TouchUpBrush");
					}
				}
			}
		}
		#endregion





		#region Functionality
		public void GamepadThreadStart ()
		{
			lock (timerLock)
			{
				if (_timer != null)
					return;
				_timer = new JocysCom.ClassLibrary.HiResTimer ();
				_timer.Elapsed += GamepadTimerElapsed;
				_timer.Interval = 2;
				_timer.Start ();
			}
		}

		public void GamepadThreadStop ()
		{
			lock (timerLock)
			{
				if (_timer == null)
					return;
				_timer.Stop ();
				_timer.Dispose ();
				_timer = null;
			}
		}

		State oldState;
		void GamepadTimerElapsed (object sender, System.Timers.ElapsedEventArgs e)
		{
			try
			{
				// If state changed.
				if (currentState.Gamepad.Buttons != oldState.Gamepad.Buttons)
				{
					AddLog ("New Button Obtined\r\n");
					ClientSend (currentState);
					oldState = currentState;
				}
				else
				{
					//AddLog ("No Button Changed\r\n");
				}
			}
			catch (Exception ex)
			{
				AddLog ("GamepadTimerElapsed Error {0}.\r\n", ex.Message);
				LastException = ex;
			}
		}
		#endregion
		#endregion





		#region Buttons
		#region Host
		private void HostButton_Click(object sender, RoutedEventArgs e)
		{
			var oldPort = Convert.ToUInt16 (Properties.Settings.Default.ComputerPort);
			var win = new HostWindow();
			var result = win.ShowDialog();
			var newPort = Convert.ToUInt16 (Properties.Settings.Default.ComputerPort);
			if (result == true)
			{
				if (IsRunning && oldPort != newPort)
				{
					ServerStop();
					serverThread = new Thread (ServerStart);
					serverThread.Start (newPort);
				}
				else if (!IsRunning)
				{
					serverThread = new Thread (ServerStart);
					serverThread.Start (newPort);
				}

				((Button) sender).Background = (Brush) FindResource ("TouchDownBrush");
				ConnectButton.Background = (Brush) FindResource ("TouchUpBrush");
			}
		}
		#endregion





		#region Connect to Host
		private void ConnectButton_Click (object sender, RoutedEventArgs e)
		{
			var win = new ConnectWindow();
			var result = win.ShowDialog();
			if (result == true)
			{
				ClientStop ();
				ClientStart ();

				((Button) sender).Background = (Brush) FindResource ("TouchDownBrush");
				HostButton.Background = (Brush) FindResource ("TouchUpBrush");
			}
		}
		#endregion
		#endregion





		#region TCP
		private void init_tcp ()
		{
			var types = new Dictionary<Type, int>();
			types.Add (typeof (State), 0);
			types.Add (typeof (Gamepad), 1);
			_Serializer = new ClassLibrary.Data.TlvSerializer (types);
		}





		#region Server
//		public class ThreadServer

		public void ServerStart (object arg)
		{
			ushort port = (ushort) arg;

			AddLog ("Starting server... ");
			if (IsRunning)
			{
				AddLog (" already started.\r\n");
				return;
			}
			IsRunning = true;
			// Set local endpoint (random port).
			//localPort = FindFreePort();
			IPAddress adddress;
			IPAddress.TryParse ("127.0.0.1", out adddress);
			localPort = port;
			// Create client.
			var localEndpoint = new IPEndPoint(adddress, port);
			tcpServer = new TcpListener (localEndpoint);
			tcpServer.Start ();
			AddLog ("Server started on {0}:{1}.\r\n", localEndpoint.Address, localEndpoint.Port);
			try
			{
				TcpClient client = tcpServer.AcceptTcpClient();
				AddLog ("Client {0} conected.\r\n", client.Client.RemoteEndPoint);
				serverNotExit = true;
				ServerReceive (client);
				client.Close ();
			}
			catch (SocketException ex)
			{
				AddLog ("Accept cancel: {0}.\r\n", ex.Message);
			}
		}
		public void ServerStop ()
		{
			if (null != tcpServer)
			{
				AddLog ("Stopping remote server... ");
				if (!IsRunning)
				{
					AddLog ("already stopped.\r\n");
					return;
				}
				IsRunning = false;
				serverNotExit = false;
				Thread.Sleep (1000);
				tcpServer.Stop ();
				AddLog ("Server stopped.\r\n");
			}
		}

		void ServerReceive (TcpClient client)
		{
			try
			{
				AddLog ("Receive from {0}...\r\n", client.Client.RemoteEndPoint);
				while (serverNotExit)
				{
					ServerReceiveCallBack (client);
				}
				AddLog ("Server thread stopped.\r\n");
			}
			catch (Exception ex)
			{
				AddLog ("Receiving Error {0}.\r\n", ex.Message);
				IsRunning = false;
			}

			AddLog ("Exiting Receive\r\n");
		}
		bool ServerReceiveCallBack (TcpClient client)
		{
			NetworkStream stream = client.GetStream ();
			try
			{
				if (stream.DataAvailable)
				{
					Byte[] data = new Byte [32];
					Int32 bytes = stream.Read(data, 0, data.Length);
					data = data.Take (bytes).ToArray ();
					var responseData = string.Join("", data.Select(x => x.ToString("X2")));
					AddLog ("Received {0} bytes data from {1}: \"{2}\"\r\n", bytes, client.Client.RemoteEndPoint, responseData);
					object result;
					var ms = new MemoryStream (data);
					//ms.Position = 0;
					TlvSerializerError status = _Serializer.Deserialize (ms, out result);
					currentState = (State) result;
					Dispatcher.Invoke (System.Windows.Threading.DispatcherPriority.Send, new Action (shapeUpdate));
				}
				else
				{
					Thread.Sleep (1);
				}
			}
			catch (SocketException ex)
			{
				AddLog ("Remote client {0} shutted down or closed the connection: {1}", client.Client.RemoteEndPoint, ex.Message);
			}

			return true;
		}
		#endregion





		#region Client
		public void ClientStart ()
		{
			AddLog ("Starting connection to remote server... ");
			if (IsRunning)
			{
				AddLog (" already connected.\r\n");
				return;
			}
			IsRunning = true;
			GamepadThreadStart ();
			// Set local endpoint (random port).
			//localPort = FindFreePort();
			localPort = Convert.ToUInt16 (Properties.Settings.Default.ComputerPort);
			IPAddress adddress;
			IPAddress.TryParse (Properties.Settings.Default.ComputerHost, out adddress);
			// Create client.
			tcpClient = new TcpClient ();
			var remoteEndpoint = new IPEndPoint(adddress, localPort);
			// Workaround for: TCP SocketException - Only one usage of each socket address is normally permitted.
			try
			{
				tcpClient.Connect (remoteEndpoint);
			}
			catch (SocketException ex)
			{
				AddLog ("An error occurred when accessing the socket: {0}.\r\n", ex.Message);
			}
			catch (ArgumentNullException ex)
			{
				AddLog ("endPoint is null: {0}.\r\n", ex.Message);
			}
			catch (ObjectDisposedException ex)
			{
				AddLog ("The TcpClient is closed: {0}.\r\n", ex.Message);
			}
			//InitClientServer();
			AddLog ("started on {0}:{1}.\r\n", remoteEndpoint.Address, remoteEndpoint.Port);
		}

		public void ClientStop ()
		{
			if (null != tcpClient)
			{
				AddLog ("Stopping remote server... ");
				if (!IsRunning)
				{
					AddLog ("already stopped.\r\n");
					return;
				}
				IsRunning = false;
				GamepadThreadStop ();
					tcpClient.Close ();
				AddLog ("stopped.\r\n");
			}
		}

		void ClientSend (State state)
		{
			var ms = new MemoryStream();
			var status = _Serializer.Serialize(ms, state);
			if (status == ClassLibrary.Data.TlvSerializerError.None)
			{
				var bytes = ms.ToArray ();
				IPAddress adddress;
				IPAddress.TryParse (Properties.Settings.Default.ComputerHost, out adddress);
				var remoteEndpoint = new IPEndPoint(adddress, Properties.Settings.Default.ComputerPort);
				var data = string.Join("", bytes.Select(x => x.ToString("X2")));
				AddLog ("To send message ({0} bytes length {1}) \"{2}\"...\r\n", bytes.Length, data.Length, data);
				try
				{
					NetworkStream stream = tcpClient.GetStream();
					stream.Write (bytes, 0, bytes.Length);
				}
				catch (ObjectDisposedException ex)
				{
					AddLog ("TcpClient is closed: {0}\r\nTry to re-open...\r\n", ex.Message);
					IsRunning = false;
					ClientStart ();
					NetworkStream stream = tcpClient.GetStream();
					stream.Write (bytes, 0, bytes.Length);
				}
				AddLog ("{0:HH:mm:ss.fff} Send status: {1}\r\n", DateTime.Now, data);
			}
		}
		#endregion
		#endregion





		#region Helper Functions
		void AddLog (string format, params object [] args)
		{
			Trace.Write (string.Format (format, args));
		}
		public static class EnumUtil
		{
			public static IEnumerable<T> GetValues<T> ()
			{
				return Enum.GetValues (typeof (T)).Cast<T> ();
			}
		}

		/// <summary>
		/// Find first free port. IANA Port categories:
		///         0 –  1023 – System or Well Known ports.
		///      1024 – 49151 – User or Registered ports.
		///     49152 - 65535 – Dynamic (Private) or Ephemeral Ports.
		/// </summary>
		/// <returns>Free port number if found; otherwise 0.</returns>
		private ushort FindFreePort(ushort startPort = 49152, ushort endPort = ushort.MaxValue)
		{
			var portArray = new List<int>();
			var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
			// Get TCP connection ports.
			var ports = properties.GetActiveTcpConnections()
				.Where(x => x.LocalEndPoint.Port >= startPort)
				.Select(x => x.LocalEndPoint.Port);
			portArray.AddRange(ports);
			// Get TCP listener ports.
			ports = properties.GetActiveTcpListeners()
				.Where(x => x.Port >= startPort)
				.Select(x => x.Port);
			portArray.AddRange(ports);
			// Get TCP listener ports.
			ports = properties.GetActiveTcpListeners()
				.Where(x => x.Port >= startPort)
				.Select(x => x.Port);
			portArray.AddRange(ports);
			// Get first port not in the list.
			for (int i = startPort; i <= endPort; i++)
				if (!portArray.Contains(i))
					return (ushort)i;
			return 0;
		}
		#endregion
	}

}
