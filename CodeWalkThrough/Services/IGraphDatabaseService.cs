using System;
using System.Collections.Generic;
using CodeWalkThrough.Models;

namespace CodeWalkThrough.Services
{
    /// <summary>
    /// Interface for graph database services to store and retrieve file system nodes
    /// </summary>
    public interface IGraphDatabaseService : IDisposable
    {
        /// <summary>
        /// Stores the file system tree in the database
        /// </summary>
        /// <param name="nodes">Collection of file system nodes to store</param>
        void StoreFileSystemTree(IEnumerable<FileSystemNode> nodes);

        /// <summary>
        /// Gets all nodes in the database
        /// </summary>
        /// <returns>All file system nodes</returns>
        IEnumerable<FileSystemNode> GetAllNodes();

        /// <summary>
        /// Gets a node by its ID
        /// </summary>
        /// <param name="id">The ID of the node to retrieve</param>
        /// <returns>The requested node, or null if not found</returns>
        FileSystemNode GetNodeById(string id);

        /// <summary>
        /// Gets the root node of the tree
        /// </summary>
        /// <returns>The root file system node</returns>
        FileSystemNode GetRootNode();
    }
}
