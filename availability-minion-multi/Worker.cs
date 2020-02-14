using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.ApplicationInsights.DataContracts;
using System.IO;
using System.Collections.Generic;
using availability_minion_multi;

namespace availabilityMinionMulti
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        public string[] configFile;
        public int testFrequency = 300000; //5 minutes in milliseconds (Edit this value to change the frequency of your tests)


        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConfigTestHelper helper = new ConfigTestHelper(_logger, "");

            foreach (TestConfig config in helper.Configs)
                await TestRunner.RunAvailabilityTestFromConfig(_logger, config);


            TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();
            var telemetryClient = new TelemetryClient(configuration);

            HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; MinionBot)");

            var testSchedule = new Dictionary<string, DateTime>();
            Random rand = new Random();


            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                if (telemetryClient != null)
                {
                    for (int i =0; i < testAddressList.Count; i++)
                    {
                        {
                            DateTime currentTime = DateTime.Now;

                            if (!testSchedule.ContainsKey(testAddressList[i]))
                            {
                                // Run only once for each address, generate initial random start time
                                // between 0 and testFrequency. Default= 300,000 milliseconds (5 minutes)
                                int resultRandom = rand.Next(0, testFrequency);
                                DateTime randStartTime = currentTime.AddMilliseconds(resultRandom);
                                testSchedule.Add(testAddressList[i], randStartTime);
                            }

                            DateTime checkPrevScheduledTime = testSchedule[testAddressList[i]];

                            // Prevent execution of test until scheduled time occurs
                            if (checkPrevScheduledTime <= currentTime)
                            {
                                _ = TestAvailability(telemetryClient, client, testAddressList[i], ikeys[i], _logger);

                                // Next scheduled execution is set to 5 minutes from now
                                DateTime scheduledRunTime = currentTime.AddMilliseconds(testFrequency); 
                                testSchedule[testAddressList[i]] = scheduledRunTime;
                            }
                        }
                    }
                }

                await Task.Delay(100, stoppingToken).ConfigureAwait(false);
            }
        }

        private static async Task TestAvailability(TelemetryClient telemetryClient, HttpClient client, String address, string ikey, ILogger _logger)
        {
            var availability = new AvailabilityTelemetry
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = address,
                RunLocation = System.Environment.MachineName,
                Success = false        
            };

            string testRunId = availability.Id;
            availability.Context.InstrumentationKey = ikey;
            availability.Context.Cloud.RoleName = "minion";
            availability.Context.Operation.Id = availability.Id;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            DateTimeOffset startTimeTest = DateTimeOffset.UtcNow;

            try
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(address),
                    Method = HttpMethod.Get
                };
                request.Headers.Add("SyntheticTest-RunId", testRunId);
                request.Headers.Add("Request-Id", "|" + testRunId);

                using (var httpResponse = await client.SendAsync(request).ConfigureAwait(false))
                {
                    // add test results to availability telemetry property
                    availability.Properties.Add("HttpResponseStatusCode", Convert.ToInt32(httpResponse.StatusCode).ToString());

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        availability.Success = true;
                        availability.Message = $"Test succeeded with response: {httpResponse.StatusCode}";
                        _logger.LogTrace($"[Verbose]: {availability.Message}");
                    }
                    else if (!httpResponse.IsSuccessStatusCode)
                    {
                        availability.Message = $"Test failed with response: {httpResponse.StatusCode}";
                        _logger.LogWarning($"[Warning]: {availability.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // track exception when unable to determine the state of web app
                availability.Message = ex.Message;
                var exceptionTelemetry = new ExceptionTelemetry(ex);
                exceptionTelemetry.Context.InstrumentationKey = ikey;
                exceptionTelemetry.Context.Cloud.RoleName = "minion";
                exceptionTelemetry.Context.Operation.Id = availability.Id;
                exceptionTelemetry.Properties.Add("TestAddress", address);
                exceptionTelemetry.Properties.Add("RunLocation", availability.RunLocation);
                telemetryClient.TrackException(exceptionTelemetry);
                _logger.LogError($"[Error]: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();

                availability.Duration = stopwatch.Elapsed;
                availability.Timestamp = startTimeTest;

                telemetryClient.TrackAvailability(availability);
                _logger.LogInformation($"Availability telemetry for {availability.Name} is sent.");
            }
        }

    }
}
