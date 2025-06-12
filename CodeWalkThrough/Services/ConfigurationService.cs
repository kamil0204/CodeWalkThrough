using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using CodeWalkThrough.Models;

namespace CodeWalkThrough.Services
{
    /// <summary>
    /// Service for loading and managing application configuration
    /// </summary>
    public class ConfigurationService
    {
        private readonly IConfiguration _configuration;
        private static ConfigurationService? _instance;
        private static readonly object _lock = new object();
        
        /// <summary>
        /// Gets the singleton instance of the ConfigurationService
        /// </summary>
        public static ConfigurationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ConfigurationService();
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Private constructor for singleton pattern
        /// </summary>
        private ConfigurationService()
        {
            // Build the configuration
            _configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
        }
        
        /// <summary>
        /// Gets the application settings from configuration
        /// </summary>
        /// <returns>The application settings</returns>
        public AppSettings GetAppSettings()
        {
            var settings = new AppSettings();
            _configuration.Bind(settings);
            return settings;
        }
        
        /// <summary>
        /// Gets a section from the configuration
        /// </summary>
        /// <typeparam name="T">The type to bind to</typeparam>
        /// <param name="sectionName">Name of the section</param>
        /// <returns>The bound section or default if not found</returns>
        public T? GetSection<T>(string sectionName) where T : class, new()
        {
            var section = _configuration.GetSection(sectionName);
            if (!section.Exists())
            {
                return new T();
            }
            
            var result = new T();
            section.Bind(result);
            return result;
        }
        
        /// <summary>
        /// Gets a configuration value
        /// </summary>
        /// <param name="key">Configuration key</param>
        /// <returns>Configuration value or null if not found</returns>
        public string? GetValue(string key)
        {
            return _configuration[key];
        }
    }
}
