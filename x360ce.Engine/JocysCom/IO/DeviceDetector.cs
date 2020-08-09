﻿using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using JocysCom.ClassLibrary.Win32;
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JocysCom.ClassLibrary.IO
{

	public class DeviceDetector : IDisposable
	{

		private const int ERROR_INSUFFICIENT_BUFFER = 122;
		private const int ERROR_INVALID_DATA = 13;
		private const int ERROR_NO_MORE_ITEMS = 259;

		public const int INVALID_HANDLE_VALUE = -1;
		public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
		public const int DEVICE_NOTIFY_SERVICE_HANDLE = 0x00000001;
		public const int DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 0x00000004;
		public const int WM_DEVICECHANGE = 0x0219;
		public const int DIF_PROPERTYCHANGE = 0x00000012;
		public const int DIF_REMOVE = 0x00000005;
		public const uint DICS_FLAG_GLOBAL = 0x00000001;
		public const uint DICS_FLAG_CONFIGSPECIFIC = 0x00000002;
		public const uint DICS_ENABLE = 0x00000001;
		public const uint DICS_DISABLE = 0x00000002;
		public const int MAX_DEVICE_LEN = 1000;
		public const int MAX_DEVICE_ID_LEN = 200;
		private const int ERROR_INVALID_HANDLE_VALUE = -1;
		const int BROADCAST_QUERY_DENY = 0x424D5144;

		#region Native

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern int RegCloseKey(IntPtr hkey);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern int RegQueryValueEx(IntPtr hKey, string valueName, int reserved, ref REG type, System.Text.StringBuilder data, ref int dataSize);

		[DllImport("cfgmgr32", SetLastError = true)]
		public static extern CR CM_Open_DevNode_Key(int dnDevNode, int samDesired, int ulHardwareProfile, int Disposition, ref IntPtr phkDevice, int ulFlags);

		[DllImport("cfgmgr32", SetLastError = true)]
		public static extern CR CM_Locate_DevNode(out UInt32 dnDevInst, IntPtr pDeviceID, int ulFlags);

		[DllImport("cfgmgr32", SetLastError = true)]
		public static extern CR CM_Reenumerate_DevNode(UInt32 dnDevInst, int ulFlags);

		/// <summary>
		/// Retrieves the "device instance ID" for a specified device
		/// </summary>
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_Device_ID(UInt32 dnDevInst, StringBuilder Buffer, int BufferLen, int ulFlags);

		[DllImport("setupapi.dll")]
		public static extern CR CM_Get_Parent(out UInt32 pdnDevInst, UInt32 dnDevInst, int ulFlags);

		/// <summary>
		/// http://msdn.microsoft.com/en-gb/library/windows/hardware/ff538517%28v=vs.85%29.aspx
		/// </summary>
		/// <param name="Status"></param>
		/// <param name="ProblemNumber"></param>
		/// <param name="dnDevInst"></param>
		/// <param name="ulFlags"></param>
		/// <param name="hMachine"></param>
		/// <returns></returns>
		[DllImport("setupapi.dll", SetLastError = true)]
		public static extern CR CM_Get_DevNode_Status_Ex(out UInt32 Status, out UInt32 ProblemNumber, UInt32 dnDevInst, int ulFlags, IntPtr hMachine);

		[DllImport("setupapi.dll", SetLastError = true)]
		public static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDeviceInfoSet);

		[DllImport("setupapi.dll", SetLastError = true)]
		public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, [MarshalAs(UnmanagedType.LPStruct)]Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

		[DllImport("setupapi.dll", SetLastError = true)]
		internal static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, IntPtr DeviceInstanceId, int DeviceInstanceIdSize, ref int RequiredSize);

		[DllImport("setupapi.dll", SetLastError = true)]
		public static extern IntPtr SetupDiGetClassDevs([MarshalAs(UnmanagedType.LPStruct)]System.Guid classGuid, string enumerator, IntPtr hwndParent, DIGCF flags);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiRemoveDevice(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

		[DllImport("Newdev.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool DiUninstallDevice(IntPtr hwndParent, IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, int ulFlags, out bool NeedReboot);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, int memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, ref int requiredSize, ref SP_DEVINFO_DATA deviceInfoData);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiGetClassDescription(ref Guid ClassGuid, StringBuilder classDescription, Int32 ClassDescriptionSize, ref UInt32 RequiredSize);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiSetSelectedDevice(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

		static bool GetDeviceNodeStatus(UInt32 dnDevInst, IntPtr hMachine, out Win32.DeviceNodeStatus status)
		{
			// c:\Program Files\Microsoft SDKs\Windows\v7.1\Include\cfg.h
			uint Status;
			uint ProblemNumber;
			bool success = false;
			// http://msdn.microsoft.com/en-gb/library/windows/hardware/ff538517%28v=vs.85%29.aspx
			var cr = CM_Get_DevNode_Status_Ex(out Status, out ProblemNumber, dnDevInst, 0, hMachine);
			status = 0;
			if (cr == CR.CR_SUCCESS)
			{
				status = (Win32.DeviceNodeStatus)Status;
				success = true;
			}
			return success;
		}

		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_DevNode_Status_Ex(UInt32 dnDevInst, StringBuilder Buffer, UInt32 BufferLen, UInt32 ulFlags);

		/// <summary>
		/// The SetupDiSetClassInstallParams function sets or clears class install parameters
		/// for a device information set or a particular device information element.
		/// </summary>
		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern bool SetupDiSetClassInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, SP_PROPCHANGE_PARAMS ClassInstallParams, UInt32 ClassInstallParamsSize);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern bool SetupDiSetClassInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, SP_REMOVEDEVICE_PARAMS ClassInstallParams, UInt32 ClassInstallParamsSize);

		/// <summary>
		/// The SetupDiCallClassInstaller function calls the appropriate class installer,
		/// and any registered co-installers, with the specified installation request (DIF code).
		/// </summary>
		[DllImport("setupapi.dll", SetLastError = true)]
		static extern bool SetupDiCallClassInstaller(UInt32 InstallFunction, IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);

		[DllImport("setupapi.dll")]
		public static extern bool SetupDiOpenDeviceInfo(IntPtr deviceInfoSet, string deviceInstanceId, IntPtr hwndParent, UInt32 openFlags, ref SP_DEVINFO_DATA deviceInfoData);

		[DllImport("setupapi.dll", SetLastError = true)]
		static extern bool SetupDiChangeState(IntPtr deviceInfoSet, [In] ref SP_DEVINFO_DATA deviceInfoData);
		/// <summary>
		/// The SetupDiGetDeviceRegistryProperty function retrieves the specified device property.
		/// This handle is typically returned by the SetupDiGetClassDevs or SetupDiGetClassDevsEx function.
		/// </summary>
		/// <param Name="DeviceInfoSet">Handle to the device information set that contains the interface and its underlying device.</param>
		/// <param Name="DeviceInfoData">Pointer to an SP_DEVINFO_DATA structure that defines the device instance.</param>
		/// <param Name="Property">Device property to be retrieved. SEE MSDN</param>
		/// <param Name="PropertyRegDataType">Pointer to a variable that receives the registry data Type. This parameter can be NULL.</param>
		/// <param Name="PropertyBuffer">Pointer to a buffer that receives the requested device property.</param>
		/// <param Name="PropertyBufferSize">Size of the buffer, in bytes.</param>
		/// <param Name="RequiredSize">Pointer to a variable that receives the required buffer size, in bytes. This parameter can be NULL.</param>
		/// <returns>If the function succeeds, the return value is non zero.</returns>
		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool SetupDiGetDeviceRegistryProperty(
		   IntPtr DeviceInfoSet,
		   ref SP_DEVINFO_DATA DeviceInfoData,
		   uint Property,
		   out uint PropertyRegDataType,
		   byte[] PropertyBuffer,
		   uint PropertyBufferSize,
		   out uint RequiredSize
		);

		#endregion

		/// <summary>
		/// Handle of the window which receives messages from Windows. This will be a form.
		/// </summary>
		IntPtr _RecipientHandle;

		public delegate void DeviceDetectorEventHandler(Object sender, DeviceDetectorEventArgs e);

		/// <summary>
		/// Events signalized to the client app.
		/// Add handlers for these events in your form to be notified of removable device events 
		/// </summary>
		public event DeviceDetectorEventHandler DeviceChanged;

		private DeviceDetectorForm detectorForm;

		/// <summary>
		/// Create hidden form for processing Windows messages about USB drives. You do not need to override WndProc in your form.
		/// </summary>
		public DeviceDetector(bool showForm = false)
		{
			detectorForm = new DeviceDetectorForm(this);
			if (showForm) detectorForm.Show();
			_RecipientHandle = detectorForm.Handle;
			//RegisterDeviceNotification(_RecipientHandle, DEV_BROADCAST_DEVICEINTERFACE, 0)
		}

		/// <summary>
		/// Message handler which must be called from client form. Processes Windows messages and calls event handlers. 
		/// </summary>
		/// <param name="m"></param>
		public void WndProc(ref Message m)
		{
			if (m.Msg == WM_DEVICECHANGE)
			{
				var changeType = (DBT)m.WParam.ToInt32();
				object deviceInfo = null;
				DBCH_DEVICETYPE? deviceType = null;
				if (m.LParam != IntPtr.Zero)
				{
					var hdr = (DEV_BROADCAST_HDR)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_HDR));
					deviceType = hdr.dbch_devicetype;
					switch (deviceType)
					{
						case DBCH_DEVICETYPE.DBT_DEVTYP_DEVICEINTERFACE:
							var di = (DEV_BROADCAST_DEVICEINTERFACE)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_DEVICEINTERFACE));
							IntPtr dName = new IntPtr(m.LParam.ToInt64() + Marshal.OffsetOf(typeof(DEV_BROADCAST_DEVICEINTERFACE), "dbcc_name").ToInt64());
							di.dbcc_name = Marshal.PtrToStringAuto(dName).ToCharArray();
							deviceInfo = di;
							break;
						case DBCH_DEVICETYPE.DBT_DEVTYP_HANDLE:
							deviceInfo = (DEV_BROADCAST_HANDLE)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_HANDLE));
							break;
						case DBCH_DEVICETYPE.DBT_DEVTYP_OEM:
							deviceInfo = (DEV_BROADCAST_OEM)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_OEM));
							break;
						case DBCH_DEVICETYPE.DBT_DEVTYP_PORT:
							var pi = (DEV_BROADCAST_PORT)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_PORT));
							IntPtr pName = new IntPtr(m.LParam.ToInt64() + Marshal.OffsetOf(typeof(DEV_BROADCAST_PORT), "dbcc_name").ToInt64());
							pi.dbcc_name = Marshal.PtrToStringAuto(pName).ToCharArray();
							deviceInfo = pi;
							break;
						case DBCH_DEVICETYPE.DBT_DEVTYP_VOLUME:
							deviceInfo = (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));
							break;
						default:
							break;
					}
				}
				var e = new DeviceDetectorEventArgs(changeType, deviceType, deviceInfo);
				if (DeviceChanged != null) DeviceChanged(this, e);
				switch (changeType)
				{
					// Device is about to be removed. Any application can cancel the removal.
					case DBT.DBT_DEVICEQUERYREMOVE:
						if (deviceType == DBCH_DEVICETYPE.DBT_DEVTYP_HANDLE)
						{
							// If the client wants to cancel, let Windows know.
							if (e.Cancel) m.Result = (IntPtr)BROADCAST_QUERY_DENY;
						}
						break;
				}

			}
		}

		public static string GetDeviceId(uint deviceInstance)
		{
			var sb = new StringBuilder(MAX_DEVICE_ID_LEN);
			var CRResult = DeviceDetector.CM_Get_Device_ID(deviceInstance, sb, sb.Capacity, 0);
			if (CRResult != CR.CR_SUCCESS) throw new Exception("Error calling CM_Get_Device_ID: " + CRResult.ToString());
			return sb.ToString();
		}

		public static string GetDeviceDescription(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
		{
			var deviceDescription = GetStringPropertyForDevice(deviceInfoSet, deviceInfoData, SPDRP.SPDRP_DEVICEDESC);
			if (!string.IsNullOrEmpty(deviceDescription)) return deviceDescription.Trim();
			var deviceFriendlyName = GetStringPropertyForDevice(deviceInfoSet, deviceInfoData, SPDRP.SPDRP_FRIENDLYNAME);
			return (deviceFriendlyName ?? "").Trim();
		}

		public static string GetDeviceManufacturer(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
		{
			var deviceManufacturer = GetStringPropertyForDevice(deviceInfoSet, deviceInfoData, SPDRP.SPDRP_MFG);
			return (deviceManufacturer ?? "").Trim();
		}

		static Regex VidPidRx;

		static string GetVidPidRev(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, out uint vid, out uint pid, out uint rev)
		{
			VidPidRx = VidPidRx ?? new Regex("(VID|VEN)_(?<vid>[0-9A-F]{4})&PID_(?<pid>[0-9A-F]{4})(&REV_(?<rev>[0-9A-F]{4}))?");
			vid = 0;
			pid = 0;
			rev = 0;
			var value = GetStringPropertyForDevice(deviceInfoSet, deviceInfoData, SPDRP.SPDRP_HARDWAREID) ?? "";
			if (string.IsNullOrEmpty(value)) return value;
			var m = VidPidRx.Match(value.ToUpper());
			if (m.Success)
			{
				vid = Convert.ToUInt32(m.Groups["vid"].Value, 16);
				pid = Convert.ToUInt32(m.Groups["pid"].Value, 16);
				if (!string.IsNullOrEmpty(m.Groups["rev"].Value))
				{
					rev = Convert.ToUInt32(m.Groups["rev"].Value, 16);
				}
			}
			return value;
		}

		private static string GetStringPropertyForDevice(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, SPDRP propId)
		{
			uint proptype;
			uint outsize = 0;
			var buffer = new byte[MAX_DEVICE_LEN];
			var result = DeviceDetector.SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, (uint)propId, out proptype, buffer, (uint)buffer.Length, out outsize);
			if (!result)
			{
				var errcode = Marshal.GetLastWin32Error();
				if (errcode == ERROR_INVALID_DATA) return null;
				throw new Exception("Error calling SetupDiGetDeviceRegistryPropertyW: " + errcode);
			}
			var o = "";
			if (outsize > 0)
			{
				switch (proptype)
				{
					case (uint)REG.REG_SZ:
						o = Encoding.Unicode.GetString(buffer, 0, (int)outsize - 2);
						break;
					case (uint)REG.REG_MULTI_SZ:
						o = Encoding.Unicode.GetString(buffer, 0, (int)outsize - 2);
						o = o.Trim('\0').Replace("\0", "\r\n");
						break;
					default:
						break;
				}
			}
			return o;
		}

		private static SP_DEVINFO_DATA? GetDeviceInfo(string deviceInstanceId)
		{
			Guid classGuid = System.Guid.Empty;
			IntPtr deviceInfoSet = SetupDiGetClassDevs(classGuid, null, IntPtr.Zero, DIGCF.DIGCF_ALLCLASSES);
			SP_DEVINFO_DATA? di = null;
			if (deviceInfoSet.ToInt32() != ERROR_INVALID_HANDLE_VALUE)
			{
				di = GetDeviceInfo(deviceInfoSet, deviceInstanceId);
				SetupDiDestroyDeviceInfoList(deviceInfoSet);
			}
			return di;
		}

		public static SP_DEVINFO_DATA? GetDeviceInfo(IntPtr deviceInfoSet, string deviceInstanceId)
		{
			var da = new SP_DEVINFO_DATA();
			da.Initialize();
			var result = SetupDiOpenDeviceInfo(deviceInfoSet, deviceInstanceId, IntPtr.Zero, 0, ref da);
			if (!result) return null;
			return da;
		}

		public static DeviceInfo[] GetDevices()
		{
			return GetDevices(Guid.Empty, DIGCF.DIGCF_ALLCLASSES);
		}

		static object GetDevicesLock = new object();

		/// <summary>
		/// Get list of devices.
		/// </summary>
		/// <returns>List of devices</returns>
		/// <remarks>
		/// This is code I cobbled together from a number of newsgroup threads
		/// as well as some C++ stuff I translated off of MSDN.  Seems to work.
		/// The idea is to come up with a list of devices, same as the device
		/// manager does.  Currently it uses the actual "system" names for the
		/// hardware.  It is also possible to use hardware IDs.  See the docs
		/// for SetupDiGetDeviceRegistryProperty in the MS SDK for more details.
		/// Errors:   This method may throw the following errors.
		///           Failed to enumerate device tree!
		///           Invalid handle!
		/// </remarks>		
		public static DeviceInfo[] GetDevices(Guid classGuid, DIGCF flags)
		{
			return GetDevices(classGuid, flags, null, 0, 0, 0);
		}

		public static DeviceInfo GetDevice(Guid classGuid, DIGCF flags, string deviceId)
		{
			return GetDevices(classGuid, flags, deviceId, 0, 0, 0).FirstOrDefault();
		}

		public static DeviceInfo[] GetDevices(Guid classGuid, DIGCF flags, string deviceId, int vid, int pid, int rev)
		{
			lock (GetDevicesLock)
			{
				IntPtr deviceInfoSet = SetupDiGetClassDevs(classGuid, null, IntPtr.Zero, flags); //  | DIGCF.DIGCF_PRESENT
				if (deviceInfoSet.ToInt32() == ERROR_INVALID_HANDLE_VALUE)
				{
					throw new Exception("Invalid Handle");
				}
				var list = new List<DeviceInfo>();
				var deviceInfoData = new SP_DEVINFO_DATA();
				deviceInfoData.Initialize();
				int i;
				for (i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref deviceInfoData); i++)
				{
					var currentDeviceId = GetDeviceId(deviceInfoData.DevInst);
					if (!string.IsNullOrEmpty(deviceId) && deviceId != currentDeviceId) continue;
					var device = GetDeviceInfo(deviceInfoSet, deviceInfoData, deviceId);
					if (vid > 0 && device.VendorId != vid) continue;
					if (pid > 0 && device.ProductId != pid) continue;
					if (rev > 0 && device.Revision != rev) continue;
					list.Add(device);
				}
				SetupDiDestroyDeviceInfoList(deviceInfoSet);
				return list.OrderBy(x => x.ClassDescription).ThenBy(x => x.Description).ToArray();
			}
		}

		static DeviceInfo GetDeviceInfo(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, string deviceId)
		{
			var deviceName = GetDeviceDescription(deviceInfoSet, deviceInfoData);
			var deviceManufacturer = GetDeviceManufacturer(deviceInfoSet, deviceInfoData);
			var deviceClassGuid = deviceInfoData.ClassGuid;
			var classDescription = GetClassDescription(deviceClassGuid);
			Win32.DeviceNodeStatus status;
			GetDeviceNodeStatus(deviceInfoData.DevInst, IntPtr.Zero, out status);
			uint vid;
			uint pid;
			uint rev;
			var hwid = GetVidPidRev(deviceInfoSet, deviceInfoData, out vid, out pid, out rev);
			//if (deviceName.Contains("vJoy Device"))
			//{
			//	var sb = new StringBuilder();
			//	var props = (SPDRP[])Enum.GetValues(typeof(SPDRP));
			//	foreach (var item in props)
			//	{
			//		if (new[] { SPDRP.SPDRP_UNUSED0, SPDRP.SPDRP_UNUSED1, SPDRP.SPDRP_UNUSED2 }.Contains(item))
			//		{
			//			continue;
			//		}
			//		try
			//		{
			//			var value = GetStringPropertyForDevice(deviceInfoSet, deviceInfoData, item);
			//			sb.AppendFormat("{0}={1}\r\n", item, value);
			//		}
			//		catch (Exception ex)
			//		{
			//			sb.AppendFormat("{0}={1}\r\n", item, ex.ToString());
			//		}
			//	}
			//}
			var device = new DeviceInfo(deviceId, deviceManufacturer, deviceName, deviceClassGuid, classDescription, status, vid, pid, rev);
			return device;
		}


		public static DeviceInfo GetParentDevice(Guid classGuid, DIGCF flags, string deviceId)
		{
			lock (GetDevicesLock)
			{
				IntPtr deviceInfoSet = SetupDiGetClassDevs(classGuid, null, IntPtr.Zero, flags); //  | DIGCF.DIGCF_PRESENT
				if (deviceInfoSet.ToInt32() == ERROR_INVALID_HANDLE_VALUE)
				{
					throw new Exception("Invalid Handle");
				}
				DeviceInfo device = null;
				var deviceInfoData = new SP_DEVINFO_DATA();
				deviceInfoData.Initialize();
				int i;
				for (i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref deviceInfoData); i++)
				{
					if (deviceId == GetDeviceId(deviceInfoData.DevInst))
					{
						uint parentDeviceInstance = 0;
						var CRResult = CM_Get_Parent(out parentDeviceInstance, deviceInfoData.DevInst, 0);
						if (CRResult == CR.CR_NO_SUCH_DEVNODE) break;
						if (CRResult != CR.CR_SUCCESS) break;
						var parentDeviceId = GetDeviceId(parentDeviceInstance);
						device = GetDevice(classGuid, flags, parentDeviceId);
						break;
					}
				}
				SetupDiDestroyDeviceInfoList(deviceInfoSet);
				return device;
			}
		}

		public static string GetClassDescription(Guid classGuid)
		{
			StringBuilder deviceClassDescription = new StringBuilder(256);
			UInt32 retLength = 0;
			SetupDiGetClassDescription(ref classGuid, deviceClassDescription, deviceClassDescription.Capacity, ref retLength);
			return deviceClassDescription.ToString();
		}


		#region Manipulate Devices

		public static bool ScanForHardwareChanges()
		{
			UInt32 devInst = 0;
			CR status;
			var CM_LOCATE_DEVNODE_NORMAL = 0x00000000;
			//var CM_LOCATE_DEVNODE_PHANTOM = 0x00000001;
			//var CM_LOCATE_DEVNODE_CANCELREMOVE = 0x00000002;
			//var CM_LOCATE_DEVNODE_NOVALIDATION = 0x00000004;
			//var CM_LOCATE_DEVNODE_BITS = 0x00000007;
			// Get the root DEV node.
			status = CM_Locate_DevNode(out devInst, IntPtr.Zero, CM_LOCATE_DEVNODE_NORMAL);
			if (status != CR.CR_SUCCESS)
			{
				return false;
			}
			status = CM_Reenumerate_DevNode(devInst, 0);
			if (status != CR.CR_SUCCESS)
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Set device state.
		/// </summary>
		/// <param name="match"></param>
		/// <param name="enable"></param>
		/// <returns>Success state.</returns>
		/// <remarks>
		/// This is nearly identical to the method above except it
		/// tries to match the hardware description against the criteria
		/// passed in.  If a match is found, that device will the be
		/// enabled or disabled based on bEnable.
		/// Errors:   This method may throw the following exceptions.
		///           Failed to enumerate device tree!
		/// </remarks>
		public static bool SetDeviceState(string deviceId, bool enable)
		{
			try
			{
				Guid myGUID = System.Guid.Empty;
				IntPtr deviceInfoSet = SetupDiGetClassDevs(myGUID, null, IntPtr.Zero, DIGCF.DIGCF_ALLCLASSES | DIGCF.DIGCF_PRESENT);
				if (deviceInfoSet.ToInt32() == INVALID_HANDLE_VALUE)
				{
					return false;
				}
				var deviceInfoData = new SP_DEVINFO_DATA();
				deviceInfoData.Initialize();
				deviceInfoData.DevInst = 0;
				deviceInfoData.ClassGuid = System.Guid.Empty;
				deviceInfoData.Reserved = IntPtr.Zero;
				for (var i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref deviceInfoData); i++)
				{
					var currentDeviceId = GetDeviceId(deviceInfoData.DevInst);
					if (deviceId == currentDeviceId)
					{
						SetDeviceState(deviceInfoSet, deviceInfoData, enable);
					}
				}
				SetupDiDestroyDeviceInfoList(deviceInfoSet);
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to enumerate device tree!", ex);
				//return false;
			}
			return true;
		}

		/// <summary>
		/// Attempts to enable or disable a device driver.
		/// </summary>
		/// <param name="deviceInfoSet">Pointer to device.</param>
		/// <param name="deviceInfoData"></param>
		/// <param name="bEnable"></param>
		/// <returns>State of success.</returns>
		/// <remarks>
		/// IMPORTANT NOTE!!!
		/// This code currently does not check the reboot flag.
		/// Some devices require you reboot the OS for the change
		/// to take affect.  If this describes your device, you 
		/// will need to look at the SDK call:
		/// SetupDiGetDeviceInstallParams.  You can call it 
		/// directly after ChangeIt to see whether or not you need 
		/// to reboot the OS for you change to go into effect.
		/// Errors:   This method may throw the following exceptions.
		///           Unable to change device state!
		/// </remarks>
		private static bool SetDeviceState(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, bool bEnable)
		{
			try
			{
				SP_CLASSINSTALL_HEADER header = new SP_CLASSINSTALL_HEADER();
				header.cbSize = (UInt32)Marshal.SizeOf(header);
				header.InstallFunction = DIF_PROPERTYCHANGE;
				SP_PROPCHANGE_PARAMS classInstallParams = new SP_PROPCHANGE_PARAMS();
				classInstallParams.ClassInstallHeader = header;
				classInstallParams.StateChange = bEnable ? DICS_ENABLE : DICS_DISABLE;
				classInstallParams.Scope = DICS_FLAG_GLOBAL;
				classInstallParams.HwProfile = 0;
				var classInstallParamsSize = (UInt32)Marshal.SizeOf(classInstallParams);
				bool result = SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData, classInstallParams, classInstallParamsSize);
				if (result) result = SetupDiChangeState(deviceInfoSet, ref deviceInfoData);
				if (!result)
				{
					var ex = new Win32Exception();
					SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, deviceInfoSet, ref deviceInfoData);
				}
				return result;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static Win32Exception RemoveDevice(string deviceId)
		{
			bool needReboot;
			return RemoveDevice(deviceId, 1, out needReboot);
		}

		public static Win32Exception RemoveDevice(string deviceId, int method, out bool needReboot)
		{
			Guid classGuid = System.Guid.Empty;
			IntPtr deviceInfoSet = SetupDiGetClassDevs(classGuid, null, IntPtr.Zero, DIGCF.DIGCF_ALLCLASSES);
			Win32Exception ex = null;
			needReboot = false;
			var success = false;
			if (deviceInfoSet.ToInt32() != ERROR_INVALID_HANDLE_VALUE)
			{
				var deviceInfoData = GetDeviceInfo(deviceInfoSet, deviceId);
				if (deviceInfoData.HasValue)
				{
					var di = deviceInfoData.Value;
					switch (method)
					{
						case 1:
							SP_CLASSINSTALL_HEADER header = new SP_CLASSINSTALL_HEADER();
							header.cbSize = (UInt32)Marshal.SizeOf(header);
							header.InstallFunction = DIF_REMOVE;
							var classInstallParams = new SP_REMOVEDEVICE_PARAMS();
							classInstallParams.ClassInstallHeader = header;
							classInstallParams.Scope = DICS_FLAG_GLOBAL;
							classInstallParams.HwProfile = 0;
							var classInstallParamsSize = (UInt32)Marshal.SizeOf(classInstallParams);
							success = SetupDiSetClassInstallParams(deviceInfoSet, ref di, classInstallParams, classInstallParamsSize);
							if (success)
							{
								success = SetupDiSetSelectedDevice(deviceInfoSet, ref di);
								if (success)
								{
									success = SetupDiCallClassInstaller(DIF_REMOVE, deviceInfoSet, ref di);
									// ex.ErrorCode = 0xE0000235: SetupDiCallClassInstaller throws ERROR_IN_WOW64 when compiled for 32 bit on a 64 bit machine.
									if (!success) ex = new Win32Exception();
								}
								else ex = new Win32Exception();
							}
							else ex = new Win32Exception();
							break;
						case 2:
							success = SetupDiRemoveDevice(deviceInfoSet, ref di);
							if (!success) ex = new Win32Exception();
							break;
						case 3:
							success = DiUninstallDevice(IntPtr.Zero, deviceInfoSet, ref di, 0, out needReboot);
							if (!success) ex = new Win32Exception();
							break;
						default:
							break;
					}
				}
				SetupDiDestroyDeviceInfoList(deviceInfoSet);
			}
			else
			{
				ex = new Win32Exception();
			}
			return ex;
		}


		#endregion

		#region IDisposable

		// Dispose() calls Dispose(true)
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		// The bulk of the clean-up code is implemented in Dispose(bool)
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				//UnregisterDeviceNotification(_RecipientHandle);
				detectorForm.Dispose();
				detectorForm = null;
			}
		}

		#endregion

	}
}
