using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace availability_minion_multi
{
	public class ConfigTestHelper
	{

		public string ConfigFilePath { get; set; }
		public List<TestConfig> Configs { get; set; }

		public ConfigTestHelper(ILogger log, string path)
		{
			if (String.IsNullOrEmpty(path)) ConfigFilePath = System.IO.Directory.GetCurrentDirectory();
			else ConfigFilePath = path;

			EnsureConfiguration(log);

		}

		/// <summary>
		/// Load and validate configuration
		/// </summary>
		/// <param name="log"></param>
		/// <returns>Deserialized config from json files found in the current directory</returns>
		private void EnsureConfiguration(ILogger log)
		{
			//load list of config files
			string[] files = FindConfigFiles(log);

			//load json object from config files
			LoadConfigFromFiles(log, files);

			log.LogInformation($"Successfully loaded configuration files from {ConfigFilePath}");
		}

		/// <summary>
		/// Load json from file into POCO
		/// </summary>
		/// <param name="log"></param>
		/// <param name="filename"></param>
		/// <returns>Hydrated Config object from json</returns>
		private TestConfig LoadConfigFromFile(ILogger log, string filename)
		{
			TestConfig config;
			try
			{
				config = (TestConfig)JsonConvert.DeserializeObject(File.ReadAllText(filename), typeof(TestConfig));
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
		/// Load the Config objects from a list of files
		/// </summary>
		/// <param name="log"></param>
		/// <param name="files"></param>
		/// <returns>List of Config hydrated from json</returns>
		private void LoadConfigFromFiles(ILogger log, string[] files)
		{
			//load each config file
			files.ToList().ForEach(o => Configs.Add(LoadConfigFromFile(log, o)));

		}




		/// <summary>
		/// Check the current running directory for files named "Appxxxx.json"
		/// </summary>
		/// <param name="log"></param>
		/// <returns>List of json files in the current directory</returns>
		private string[] FindConfigFiles(ILogger log)
		{
			//check for config files
			var files = Directory.GetFiles(this.ConfigFilePath, "App*.json");
			if (files.Count() == 0)
			{
				log.LogError("There are no configuration files present in the current directory");
				throw new FileNotFoundException("There are no configuration files present in the current directory");
			}

			log.LogInformation($"Found {files.Count()} configuration files.");
			return files;
		}
	}
}
