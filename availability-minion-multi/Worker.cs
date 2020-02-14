using availability_minion_multi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace availabilityMinionMulti
{
	/// <summary>
	/// Worker is where the magic happens
	/// BackgroundService base class here lets us easily run as a hosted service.
	/// </summary>
	public class Worker : BackgroundService
	{

		/// <summary>
		/// For logging activity into AppInsights logger
		/// </summary>
		private readonly ILogger<Worker> _logger;


		/// <summary>
		/// Config files loaded from local path
		/// </summary>
		public List<AvailabilityTest> tests { get; private set; }


		/// <summary>
		/// Local ref to keep execution history
		/// </summary>
		private Dictionary<string, AvailabilityTest> RunningTests = new Dictionary<string, AvailabilityTest>();


		/// <summary>
		/// local testhelper
		/// </summary>
		private AvailabilityTestHelper TestHelper;

		/// <summary>
		/// The path where we can find configuration files
		/// </summary>
		public string TestConfigurationPath { get; private set; }

		/// <summary>
		/// The service will wake up at this interval to see if any tests need to be executed
		/// Defaults to 30
		/// </summary>
		public int RestTimeInSeconds { get; private set; }

		/// <summary>
		/// ctor to setup everything
		/// </summary>
		/// <param name="logger"></param>
		public Worker(ILogger<Worker> logger)
		{
			_logger = logger;

			IConfiguration config = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json", true, true)
			.Build();

			//ensure we have a good location to load the config files
			EnsureConfiguration(logger, config);

			//load the tests from the file system
			TestHelper = new AvailabilityTestHelper(_logger, TestConfigurationPath);

			//load the tests into memory
			TestHelper.Tests.ForEach(o => RunningTests.Add(o.ApplicationName, o));

		}

		private void EnsureConfiguration(ILogger<Worker> logger, IConfiguration config)
		{
			if (string.IsNullOrEmpty(config["TestConfigurationPath"]))
			{
				throw new ArgumentNullException("TestConfigurationPath", "There is no setting in appsettings.json for 'TestConfigurationPath'");
			}
			else
			{
				TestConfigurationPath = config["TestConfigurationPath"];
				logger.LogInformation($"Looking for configuration files at {TestConfigurationPath}");
				if (!Directory.Exists(TestConfigurationPath))
				{
					throw new ArgumentOutOfRangeException("TestConfigurationPath", $"The path provided for 'TestConfigurationPath' does not exist. Unable to access {TestConfigurationPath}");
				}
			}

			if (string.IsNullOrEmpty(config["IntervalToWaitInSeconds"]))
			{
				this.RestTimeInSeconds = 30;
			}
			else
			{
				this.RestTimeInSeconds = Convert.ToInt32(config["RestTimeInSeconds"]);
			}
		}


		/// <summary>
		/// Gets called automatically by the host
		/// </summary>
		/// <param name="stoppingToken"></param>
		/// <returns></returns>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			//keep-alive
			while (!stoppingToken.IsCancellationRequested)
			{
				var teststorun = TestHelper.Tests.Where(o => (DateTime.Now - o.LastExecuted).TotalSeconds > o.IntervalInSeconds);
				_logger.LogInformation($"Woke up at {DateTime.Now}, Found {teststorun.Count()} applications that need to be tested.");

				//check to see if we need to run again
				foreach (AvailabilityTest test in teststorun)
				{
					using (TestRunner runner = new TestRunner(_logger, test))
					{
						await runner.RunAvailabilityTest(_logger, test).ConfigureAwait(false);

						//set last exeution date so we know when to run again
						test.LastExecuted = DateTime.Now;
					}
				}

				//sleep for 30 seconds
				await Task.Delay(this.RestTimeInSeconds * 1000, stoppingToken).ConfigureAwait(false);
			}
		}
	}
}
