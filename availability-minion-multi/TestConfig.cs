using System;
using System.Collections.Generic;
using System.Text;

namespace availability_minion_multi
{
	/// <summary>
	/// Class to hold config from JSON files
	/// </summary>
	public class TestConfig
	{
		public string FileName { get; set; }
		public string APPINSIGHTS_INSTRUMENTATIONKEY { get; set; }
		public string ApplicationName { get; set; }
		public string Region { get; set; }
		public Test[] Tests { get; set; }
	}
	/// <summary>
	/// POCO for Tests
	/// </summary>
	public class Test
	{
		public string TestName { get; set; }
		public string PageUrl { get; set; }
	}
}
