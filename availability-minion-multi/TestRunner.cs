using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace availability_minion_multi
{
	public class TestRunner: IDisposable
	{
		
		private HttpClient client;
		private TelemetryConfiguration telemetryConfiguration;
		private TelemetryClient telemetryClient;
		private ILogger log;

		public TestRunner(ILogger log, AvailabilityTest test)
		{
			this.log = log;

			//HttpClient setup
			this.client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
			this.client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; MinionBot)");

			//Telemetry client setup
			this.telemetryConfiguration = new TelemetryConfiguration();
			this.telemetryClient = new TelemetryClient(telemetryConfiguration);
		}


		/// <summary>
		/// Executes web tests for the Config
		/// </summary>
		/// <param name="log"></param>
		/// <param name="test"></param>
		/// <returns></returns>
		public async Task RunAvailabilityTest(ILogger log, AvailabilityTest test)
		{
			// If your resource is in a region like Azure Government or Azure China, change the endpoint address accordingly.
			// Visit https://docs.microsoft.com/azure/azure-monitor/app/custom-endpoints#regions-that-require-endpoint-modification for more details.
			string EndpointAddress = "https://dc.services.visualstudio.com/v2/track";
			telemetryConfiguration.InstrumentationKey = test.APPINSIGHTS_INSTRUMENTATIONKEY;
			telemetryConfiguration.TelemetryChannel = new InMemoryChannel { EndpointAddress = EndpointAddress };

			foreach (EndPoint e in test.Endpoints)
			{
				await RunAvailbiltyTestAsync(log, client, test, e, telemetryClient).ConfigureAwait(false);
			}

			client = null;
			telemetryConfiguration = null;
			telemetryClient = null;

			log.LogInformation($"Completed tests for {test.ApplicationName}");
		}

		/// <summary>
		/// Executes an availability test for a Url
		/// </summary>
		/// <param name="log"></param>
		/// <param name="testName"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		private async static Task RunAvailbiltyTestAsync(ILogger log, HttpClient client, AvailabilityTest test, EndPoint endpoint, TelemetryClient telemetryClient)
		{
			if (null == test)
			{
				Exception ex = new ArgumentOutOfRangeException("test", "The 'test' object is null.");
				log.LogError($"Invalid Test Settings : {ex.Message}");
				throw ex;
			}
			if (null == telemetryClient)
			{
				Exception ex = new ArgumentOutOfRangeException("telemetryClient", "The 'telemetryClient' object is null.");
				log.LogError($"Invalid Test Settings : {ex.Message}");
				throw ex;
			}
			if (null == endpoint)
			{
				Exception ex = new ArgumentOutOfRangeException("telemetryClient", "The 'telemetryClient' object is null.");
				log.LogError($"Invalid Test Settings : {ex.Message}");
				throw ex;
			}

			if (String.IsNullOrEmpty(endpoint.Name))
			{
				Exception ex = new ArgumentOutOfRangeException("Name", "No 'Name' was provided in the config.");
				log.LogError($"Invalid Test Settings : {ex.Message}");
				throw ex;
			}

			Uri goodUri = null;
			Uri.TryCreate(endpoint.PageUrl, UriKind.Absolute, out goodUri);
			if (null == goodUri)
			{
				Exception ex = new ArgumentOutOfRangeException("uri", $"The provided uri {endpoint.PageUrl} is invalid.");
				log.LogError($"Invalid Test Settings : {ex.Message}");
				throw ex;
			}

			//setup the telemetry
			string operationId = Guid.NewGuid().ToString("N");
			var availability = new AvailabilityTelemetry
			{
				Id = operationId,
				Name = $"{test.ApplicationName} : {endpoint.Name}",
				RunLocation = System.Environment.MachineName,
				Success = false
			};

			//start the timer
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			//try it
			try
			{
				await ExecuteWebGet(log, availability, client, goodUri).ConfigureAwait(false);
			}
			catch (HttpRequestException ex)
			{
				//grab the inner exception if the Request fails outright
				availability.Message = ex.InnerException.Message;

				var exceptionTelemetry = new ExceptionTelemetry(ex);
				exceptionTelemetry.Context.Operation.Id = operationId;
				exceptionTelemetry.Properties.Add("TestName", endpoint.Name);
				exceptionTelemetry.Properties.Add("TestLocation", System.Environment.MachineName);
				telemetryClient.TrackException(exceptionTelemetry);
			}
			catch (Exception ex)
			{
				availability.Message = ex.Message;

				var exceptionTelemetry = new ExceptionTelemetry(ex);
				exceptionTelemetry.Context.Operation.Id = operationId;
				exceptionTelemetry.Properties.Add("TestName", endpoint.Name);
				exceptionTelemetry.Properties.Add("TestLocation", System.Environment.MachineName);
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
		private async static Task ExecuteWebGet(ILogger log, AvailabilityTelemetry telemetry, HttpClient client, Uri uri)
		{
			client.DefaultRequestHeaders.Accept.Clear();
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
			client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

			using (HttpRequestMessage request = new HttpRequestMessage())
			{
				request.RequestUri = uri;
				request.Method = HttpMethod.Get;
				request.Headers.Add("SyntheticTest-RunId", telemetry.Id);
				request.Headers.Add("Request-Id", "|" + telemetry.Id);

				using (var httpResponse = await client.SendAsync(request).ConfigureAwait(false))
				{
					// add test results to availability telemetry property
					telemetry.Properties.Add("HttpResponseStatusCode", value: Convert.ToInt32(httpResponse.StatusCode).ToString());

					if (httpResponse.IsSuccessStatusCode)
					{
						telemetry.Success = true;
						telemetry.Message = $"Test succeeded with response: {httpResponse.StatusCode}";
						log.LogTrace($"[Verbose]: {telemetry.Message}");
					}
					else if (!httpResponse.IsSuccessStatusCode)
					{
						telemetry.Message = $"Test failed with response: {httpResponse.StatusCode}";
						log.LogWarning($"[Warning]: {telemetry.Message}");
					}
				}
			};

		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					client = null;
					telemetryClient = null;
					telemetryConfiguration=null;
				}

				disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}
		#endregion
	}
}
