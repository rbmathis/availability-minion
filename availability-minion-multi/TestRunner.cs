using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace availability_minion_multi
{
	public class TestRunner
	{
		//singleton HttpClient
		private static readonly HttpClient client = new HttpClient();

		//here's the magic - AppInsights telemetry client
		private static TelemetryConfiguration telemetryConfiguration = new TelemetryConfiguration();
		private static TelemetryClient telemetryClient = new TelemetryClient(telemetryConfiguration);


		/// <summary>
		/// Executes web tests for the Config
		/// </summary>
		/// <param name="log"></param>
		/// <param name="config"></param>
		/// <returns></returns>
		public static async Task RunAvailabilityTestFromConfig(ILogger log, TestConfig config)
		{
			// If your resource is in a region like Azure Government or Azure China, change the endpoint address accordingly.
			// Visit https://docs.microsoft.com/azure/azure-monitor/app/custom-endpoints#regions-that-require-endpoint-modification for more details.
			string EndpointAddress = "https://dc.services.visualstudio.com/v2/track";
			telemetryConfiguration.InstrumentationKey = config.APPINSIGHTS_INSTRUMENTATIONKEY;
			telemetryConfiguration.TelemetryChannel = new InMemoryChannel { EndpointAddress = EndpointAddress };

			foreach (Test test in config.Tests)
			{
				log.LogInformation($"Beginning test {test.TestName}");
				await RunAvailbiltyTestAsync(log, config, test, telemetryClient);
			}

			log.LogInformation($"Successfully executed tests for {config.FileName}");
		}

		/// <summary>
		/// Executes an availability test for a Url
		/// </summary>
		/// <param name="log"></param>
		/// <param name="testName"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		public async static Task RunAvailbiltyTestAsync(ILogger log, TestConfig config, Test test, TelemetryClient telemetryClient)
		{
			if (String.IsNullOrEmpty(test.TestName))
			{
				Exception ex = new ArgumentOutOfRangeException("TestName", "No 'testName' was provided");
				log.LogError($"Invalid Test Settings : {ex.Message}");
				throw ex;
			}

			Uri goodUri = null;
			Uri.TryCreate(test.PageUrl, UriKind.Absolute, out goodUri);
			if (null == goodUri)
			{
				Exception ex = new ArgumentOutOfRangeException("uri", $"The provided uri {test.PageUrl} is invalid.");
				log.LogError($"Invalid Test Settings : {ex.Message}");
				throw ex;
			}

			// REGION_NAME is a default environment variable that comes with App Service
			// It's also set in the local.settings.json for testing
			string location = config.Region;

			log.LogInformation($"Executing availability test run for {test.PageUrl} at: {DateTime.Now}");
			string operationId = Guid.NewGuid().ToString("N");

			var availability = new AvailabilityTelemetry
			{
				Id = operationId,
				Name = $"{config.ApplicationName} : {test.TestName}",
				RunLocation = location,
				Success = false
			};

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			try
			{
				await ExecuteWebGet(log, client, goodUri);
				availability.Success = true;
			}
			catch (Exception ex)
			{
				availability.Message = ex.Message;

				var exceptionTelemetry = new ExceptionTelemetry(ex);
				exceptionTelemetry.Context.Operation.Id = operationId;
				exceptionTelemetry.Properties.Add("TestName", test.TestName);
				exceptionTelemetry.Properties.Add("TestLocation", location);
				telemetryClient.TrackException(exceptionTelemetry);
			}
			finally
			{
				stopwatch.Stop();
				availability.Duration = stopwatch.Elapsed;
				availability.Timestamp = DateTimeOffset.UtcNow;
				telemetryClient.TrackAvailability(availability);
				// call flush to ensure telemetry is sent
				telemetryClient.Flush();
			}
		}


		/// <summary>
		/// Simple HTTP GET runner
		/// </summary>
		/// <param name="log"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		private async static Task ExecuteWebGet(ILogger log, HttpClient client, Uri uri)
		{
			client.DefaultRequestHeaders.Accept.Clear();
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
			client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

			log.LogInformation($"Checking {uri}");

			var stringTask = client.GetStringAsync(uri);
			var res = stringTask.Result;

			if (stringTask.IsCompletedSuccessfully)
				log.LogInformation("success");
			else
			{
				log.LogInformation("fail");
				throw new Exception($"Availability Test failed while checking {uri}.");
			}

		}
	}
}
