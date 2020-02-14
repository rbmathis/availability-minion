using System;
using System.Collections.Generic;
using System.Text;

namespace availability_minion_multi
{
	/// <summary>
	/// Class to hold config from JSON files
	/// </summary>
	public class AvailabilityTest
	{
		public string FileName { get; set; }
		public string APPINSIGHTS_INSTRUMENTATIONKEY { get; set; }
		public string ApplicationName { get; set; }
		public int IntervalInSeconds { get; set; }
		public EndPoint[] Endpoints { get; set; }
		public DateTime LastExecuted { get; set; } = DateTime.MinValue;
	}
	/// <summary>
	/// POCO for Tests
	/// </summary>
	public class EndPoint
	{
		public string Name { get; set; }
		public string PageUrl { get; set; }
	}
}
