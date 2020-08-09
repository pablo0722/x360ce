﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Text;
using System.Reflection;
using System.Xml.Serialization;

namespace x360ce.Engine.Data
{
	public partial class Game
	{

		public Game()
		{
			Timeout = -1;
		}

		[XmlIgnore]
		public string DisplayName
		{
			get { return string.Join(" - ", new string[] { FileName, FileProductName });  }
		}

		public static Game FromDisk(string fileName)
		{
			var item = new Game();
			var fi = new FileInfo(fileName);
			var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(fi.FullName);
			var architecture = Win32.PEReader.GetProcessorArchitecture(fi.FullName);
			var mask = GetMask(fi.FullName, architecture);
			if (mask == Engine.XInputMask.None)
			{
				mask = (architecture == System.Reflection.ProcessorArchitecture.Amd64)
				? mask = Engine.XInputMask.XInput13_x64
				: mask = Engine.XInputMask.XInput13_x86;
			}
			item.Timeout = -1;
			item.Comment = vi.Comments ?? "";
			item.DateCreated = DateTime.Now;
			item.DateUpdated = item.DateCreated;
			item.FileName = fi.Name ?? "";
			item.FileProductName = EngineHelper.FixName(vi.ProductName, item.FileName);
			item.FileVersion = vi.FileVersion ?? "";
			item.CompanyName = vi.CompanyName ?? "";
			item.DiskDriveId = BoardInfo.GetHashedDiskId();
			item.FileVersion = new Version(vi.FileMajorPart, vi.FileMinorPart, vi.FileBuildPart, vi.FilePrivatePart).ToString();
			item.FullPath = fi.FullName ?? "";
			item.GameId = Guid.NewGuid();
			item.HookMask = 0;
			item.XInputMask = (int)mask;
			item.DInputMask = 0;
			item.DInputFile = "";
			item.FakeVID = 0;
			item.FakePID = 0;
			item.Timeout = -1;
			item.Weight = 1;
			item.IsEnabled = true;
			item.ProcessorArchitecture = (int)architecture;
			return item;
		}

		/// <summary>
		/// Look inside file for "XInput..." strings and return XInput mask.
		/// </summary>
		/// <param name="fullName"></param>
		/// <param name="architecture"></param>
		/// <returns></returns>
		public static XInputMask GetMask(string fullName, ProcessorArchitecture architecture)
		{
			XInputMask[] xiValues;
			if (architecture == System.Reflection.ProcessorArchitecture.Amd64)
			{
				xiValues = Enum.GetValues(typeof(XInputMask))
					.Cast<XInputMask>()
					.Where(x => x.ToString().Contains("x64"))
					.ToArray();
			}
			else
			{
				xiValues = Enum.GetValues(typeof(XInputMask))
					.Cast<XInputMask>()
					.Where(x => x.ToString().Contains("x86"))
					.ToArray();
			}
			XInputMask mask = Engine.XInputMask.None;
			var dic = new Dictionary<XInputMask, string>();
			foreach (var value in xiValues)
			{
				dic.Add(value, JocysCom.ClassLibrary.ClassTools.EnumTools.GetDescription(value));
			}
			byte[] fileBytes = File.ReadAllBytes(fullName);
			foreach (var key in dic.Keys)
			{
				var stringLBytes = Encoding.UTF8.GetBytes(dic[key].ToLower());
				var stringUBytes = Encoding.UTF8.GetBytes(dic[key].ToUpper());
				int j;
				for (var i = 0; i <= (fileBytes.Length - stringLBytes.Length); i++)
				{
					if (fileBytes[i] == stringLBytes[0] || fileBytes[i] == stringUBytes[0])
					{
						for (j = 1; j < stringLBytes.Length && (fileBytes[i + j] == stringLBytes[j] || fileBytes[i + j] == stringUBytes[j]); j++) ;
						if (j == stringLBytes.Length)
						{
							return key;
						}
					}
				}
			}
			return mask;
		}


		public void LoadDefault(Program program)
		{
			if (program == null) return;
			HookMask = program.HookMask;
			XInputMask = program.XInputMask;
			if (string.IsNullOrEmpty(FileProductName) && !string.IsNullOrEmpty(program.FileProductName))
			{
				FileProductName = program.FileProductName;
			}
		}

	}
}
