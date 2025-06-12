using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CodeWalkThrough.Models;

namespace CodeWalkThrough.Services
{
    /// <summary>
    /// LiteGraph implementation for storing and retrieving file system nodes
    /// This implementation is a simulation as we're still learning the LiteGraph API
    /// It demonstrates how the code would be structured with LiteGraph but uses an in-memory cache
    /// </summary>
    public class LiteGraphDatabaseService : IGraphDatabaseService
    {
        private readonly Dictionary<string, FileSystemNode> _nodeCache = new Dictionary<string, FileSystemNode>();
        private readonly string _databasePath;
        private FileSystemNode? _rootNode;

        /// <summary>
        /// Creates a new instance of the LiteGraphDatabaseService
        /// </summary>
        /// <param name="dbPath">Path to the LiteGraph database file</param>
        public LiteGraphDatabaseService(string dbPath)
        {
            // Make sure the directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _databasePath = Path.ChangeExtension(dbPath, ".lgdb");
            
            // In a real implementation, here we would initialize the LiteGraph client
            // For demonstration purposes, we'll just use an in-memory dictionary
            _nodeCache.Clear();
            _rootNode = null;
            
            // Log that we're using the LiteGraph implementation (simulated)
            Console.WriteLine($"Initialized LiteGraphDatabaseService with database path: {_databasePath}");
            Console.WriteLine("NOTE: This is a simulated implementation for demonstration purposes");
        }

        /// <summary>
        /// Stores the file system tree in the database
        /// </summary>
        /// <param name="nodes">Collection of file system nodes to store</param>
        public void StoreFileSystemTree(IEnumerable<FileSystemNode> nodes)
        {
            // Clear cache
            _nodeCache.Clear();
            
            // Store all nodes in memory cache
            foreach (var node in nodes)
            {
                _nodeCache[node.Id] = node;
                
                // Identify the root node (node with no parent)
                if (string.IsNullOrEmpty(node.ParentId))
                {
                    _rootNode = node;
                }
            }
            
            // Save to a file to simulate persistence
            try
            {
                var json = JsonSerializer.Serialize(nodes);
                File.WriteAllText(_databasePath, json);
                
                Console.WriteLine($"Stored {_nodeCache.Count} nodes in LiteGraph database (simulated)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to simulated LiteGraph database: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all nodes in the database
        /// </summary>
        public IEnumerable<FileSystemNode> GetAllNodes()
        {
            // If the cache is empty, try to load from file
            if (_nodeCache.Count == 0)
            {
                TryLoadFromFile();
            }
            
            return _nodeCache.Values;
        }        /// <summary>
        /// Gets a node by its ID
        /// </summary>
        public FileSystemNode GetNodeById(string id)
        {
            // If the cache is empty, try to load from file
            if (_nodeCache.Count == 0)
            {
                TryLoadFromFile();
            }
            
            _nodeCache.TryGetValue(id, out var node);
            return node ?? new FileSystemNode { Id = id, Name = "Not Found" }; // Return empty node if not found
        }

        /// <summary>
        /// Gets the root node of the tree
        /// </summary>
        public FileSystemNode GetRootNode()
        {
            // If the cache is empty, try to load from file
            if (_nodeCache.Count == 0 || _rootNode == null)
            {
                TryLoadFromFile();
            }
            
            return _rootNode ?? new FileSystemNode { Id = "root", Name = "Root", IsDirectory = true }; // Return default root if not found
        }
        
        /// <summary>
        /// Helper method to load data from file into memory
        /// </summary>
        private void TryLoadFromFile()
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    var json = File.ReadAllText(_databasePath);
                    var nodes = JsonSerializer.Deserialize<List<FileSystemNode>>(json);
                    
                    if (nodes != null)
                    {
                        foreach (var node in nodes)
                        {
                            _nodeCache[node.Id] = node;
                            
                            if (string.IsNullOrEmpty(node.ParentId))
                            {
                                _rootNode = node;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading from simulated LiteGraph database: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the database connection
        /// </summary>
        public void Dispose()
        {
            // No real resources to dispose, but we would dispose the LiteGraph client here
            _nodeCache.Clear();
            _rootNode = null;
        }
    }
}