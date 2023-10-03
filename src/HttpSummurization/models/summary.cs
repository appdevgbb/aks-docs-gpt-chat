namespace HttpSummarization.Models
{
    public class Summary
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string ArticleUrl { get; set; }
        public Dictionary<string, string> Links { get; set; }

        public Summary(Guid id, string title, string summary, string articleUrl, Dictionary<string, string> links)
        {
            this.Id = id;
            this.Title = title;
            this.Summary = summary;
            this.ArticleUrl = articleUrl;
            this.Links = links;
        }
    }
}