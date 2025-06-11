using System;
using System.Collections.Generic;
using System.IO;
using CodeWalkThrough.Models;
using LiteDB;

namespace CodeWalkThrough.Services
{
    /// <summary>
    /// LiteDB implementation for storing and retrieving file system nodes
    /// </summary>
    public class LiteDbGraphDatabaseService : IGraphDatabaseService
    {
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<FileSystemNode> _nodes;

        /// <summary>
        /// Creates a new instance of the LiteDbGraphDatabaseService
        /// </summary>
        /// <param name="dbPath">Path to the LiteDB database file</param>
        public LiteDbGraphDatabaseService(string dbPath)
        {
            // Make sure the directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _db = new LiteDatabase(dbPath);
            _nodes = _db.GetCollection<FileSystemNode>("nodes");

            // Create indexes for better performance
            _nodes.EnsureIndex(n => n.Path);
            _nodes.EnsureIndex(n => n.ParentId);
        }

        /// <summary>
        /// Stores the file system tree in the database
        /// </summary>
        /// <param name="nodes">Collection of file system nodes to store</param>
        public void StoreFileSystemTree(IEnumerable<FileSystemNode> nodes)
        {
            // Clear existing data
            _nodes.DeleteAll();

            // Insert all nodes
            _nodes.InsertBulk(nodes);
        }

        /// <summary>
        /// Gets all nodes in the database
        /// </summary>
        public IEnumerable<FileSystemNode> GetAllNodes()
        {
            return _nodes.FindAll();
        }

        /// <summary>
        /// Gets a node by its ID
        /// </summary>
        public FileSystemNode GetNodeById(string id)
        {
            return _nodes.FindById(id);
        }

        /// <summary>
        /// Gets the root node of the tree
        /// </summary>
        public FileSystemNode GetRootNode()
        {
            return _nodes.FindOne(n => n.ParentId == null);
        }

        /// <summary>
        /// Disposes the database connection
        /// </summary>
        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
