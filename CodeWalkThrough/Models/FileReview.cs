using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeWalkThrough.Models
{    /// <summary>
    /// Represents the results of a file method analysis
    /// </summary>
    public class FileReview
    {
        /// <summary>
        /// The path to the reviewed file
        /// </summary>
        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Collection of methods identified in the file
        /// </summary>
        [JsonPropertyName("methods")]
        public List<MethodInfo> Methods { get; set; } = new List<MethodInfo>();
        
        /// <summary>
        /// Date and time when the analysis was performed
        /// </summary>
        [JsonPropertyName("reviewDate")]
        public DateTime ReviewDate { get; set; } = DateTime.Now;
    }    /// <summary>
    /// Represents information about a method in a file
    /// </summary>
    public class MethodInfo
    {
        /// <summary>
        /// Name of the method
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Source/origin of the method implementation (class or interface name)
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }
}
