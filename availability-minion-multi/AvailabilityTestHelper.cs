using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace availability_minion_multi
{
	public class AvailabilityTestHelper
	{
		/// <summary>
		/// Path to the config files (files named "Appxxxx.json")
		/// </summary>
		public string ConfigFilePath { get; private set; }

		/// <summary>
		/// List of AvailabilityTests that contain the test settings for each app
		/// </summary>
		public List<AvailabilityTest> Tests { get; private set; } = new List<AvailabilityTest>();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="log"></param>
		/// <param name="path"></param>
		public AvailabilityTestHelper(ILogger log, string path)
		{
			if (String.IsNullOrEmpty(path))
				throw new ArgumentNullException("path", "There was no value provided for 'path'");

			ConfigFilePath = path;

			EnsureConfiguration(log);

		}

		/// <summary>
		/// Load and validate configuration
		/// </summary>
		/// <param name="log"></param>
		/// <returns>Deserialized config from json files found in the current directory</returns>
		private void EnsureConfiguration(ILogger log)
		{
			try
			{
				//load list of config files
				string[] files = FindConfigFiles(log);

				//load each config file
				files.ToList().ForEach(o => Tests.Add(LoadConfigFromFile(log, o)));

				log.LogInformation($"Successfully loaded configuration files from {ConfigFilePath}");
			}
			catch (Exception ex)
			{
				log.LogError($"No config files found at {ConfigFilePath}");
				throw new FileNotFoundException($"No config files found at {ConfigFilePath}", ConfigFilePath);
			}
	
		}

		/// <summary>
		/// Load json from file into POCO
		/// </summary>
		/// <param name="log"></param>
		/// <param name="filename"></param>
		/// <returns>Hydrated Config object from json</returns>
		private static AvailabilityTest LoadConfigFromFile(ILogger log, string filename)
		{
			AvailabilityTest config;
			try
			{
				config = (AvailabilityTest)JsonConvert.DeserializeObject(File.ReadAllText(filename), typeof(AvailabilityTest));
				config.FileName = filename;
			}
			catch (Exception ex)
			{
				log.LogError($"Exception while loading json in {filename} : {ex.Message}");
				throw new ArgumentException($"Exception while loading json in {filename} : {ex.Message}");
			}

			log.LogInformation($"Successfully loaded configuration from {filename}");
			return config;
		}



		/// <summary>
		/// Check the current running directory for files named "Appxxxx.json"
		/// </summary>
		/// <param name="log"></param>
		/// <returns>List of json files in the current directory</returns>
		private string[] FindConfigFiles(ILogger log)
		{
			//check for config files
			var files = Directory.GetFiles(this.ConfigFilePath, "ApplicationTest*.json");
			if (files.Length == 0)
			{
				log.LogError("There are no configuration files present in the current directory");
				throw new FileNotFoundException("There are no configuration files present in the current directory");
			}

			log.LogInformation($"Found {files.Length} configuration files.");
			return files;
		}
	}
}
