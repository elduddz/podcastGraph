using System;

namespace podcastGraph.dto
{
    public class FeedEntry
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string SourceUrl { get; set; }
        public DateTime DatePublished { get; set; }
        public string FeedId { get; set; }
    }
}