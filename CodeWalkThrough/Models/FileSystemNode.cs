using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeWalkThrough.Models
{
    /// <summary>
    /// Represents a node in the file system (file or directory)
    /// </summary>
    public class FileSystemNode
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)] // For PgLite-compatibility
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Full path of the file or directory
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// Name of the file or directory
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether this node is a directory
        /// </summary>
        public bool IsDirectory { get; set; }
        
        /// <summary>
        /// File size in bytes (0 for directories)
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Last modified date
        /// </summary>
        public DateTime LastModified { get; set; }
        
        /// <summary>
        /// Parent node ID
        /// </summary>
        public string? ParentId { get; set; }
        
        /// <summary>
        /// List of child node IDs (for directories)
        /// </summary>
        public List<string> Children { get; set; } = new List<string>();
    }
}