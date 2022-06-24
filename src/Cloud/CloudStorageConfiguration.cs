namespace Savey
{
    /// <summary>
    /// Key information read from application configuration inside <see cref="Startup.Configure"/>
    /// </summary>
    public class CloudStorageConfiguration
    {
        /// <summary>
        /// The connection string for the blob storage account
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// The container name for the blob storage container
        /// </summary>
        public string? ContainerName { get; set; }
    }
}