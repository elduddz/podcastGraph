using System;

namespace podcastAggregate.dto
{
    public class item
    {
        public string title { get; set; }
        public Uri link { get; set; }
        public DateTime pubDate { get; set; }
    }
}