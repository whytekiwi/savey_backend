using System;
using System.Diagnostics.CodeAnalysis;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace Savey
{
    public interface IBlobContainerClientProvider
    {
        BlobContainerClient CloudContainer { get; }
    }

    /// <summary>
    /// Provides an instance of <see cref="BlobContainerClient" /> as required
    /// </summary>
    public class BlobContainerClientProvider : IBlobContainerClientProvider
    {
        /// <summary>
        /// The blob container client instance to use
        /// </summary>
        [NotNull]
        public BlobContainerClient CloudContainer { get; private set; }

        /// <summary>
        /// Constructs an instance by reading the application configuration
        /// </summary>
        /// <param name="options">The configuration value</param>
        /// <exception cref="Exception">If the application configuration was missing key values</exception>
        public BlobContainerClientProvider(IOptions<CloudStorageConfiguration> options)
        {
            string? connectionString = options?.Value?.ContainerConnectionString;
            string? containerName = options?.Value?.ContainerName;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Did not set blob container connection string");
            }
            if (string.IsNullOrEmpty(containerName))
            {
                throw new Exception("Did not set blob container name");
            }

            this.CloudContainer = new BlobServiceClient(connectionString)
                .GetBlobContainerClient(containerName);
        }
    }
}