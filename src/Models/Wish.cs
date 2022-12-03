namespace Savey
{
    public class Wish
    {
        public string Id { get; set; } = Utilities.GenerateId();
        public string? Name { get; set; }
        public string[]? Tags { get; set; }
        public string? Color { get; set; }
        public string? PhotoFileName { get; set; }
        public string? VideoFileName { get; set; }
    }
}