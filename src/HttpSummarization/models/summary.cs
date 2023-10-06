namespace HttpSummarization.Models
{
    public class Summary
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string SummaryText { get; set; }
        public string OriginalText{ get; set;}
        public string ArticleUrl { get; set; }
        public Dictionary<string, string>? Links { get; set; }

        public Summary(string title, string summaryText, string originalText, string articleUrl, Dictionary<string, string>? links)
        {
            this.Id = Guid.NewGuid();
            this.Title = title;
            this.SummaryText = summaryText;
            this.OriginalText = originalText;
            this.ArticleUrl = articleUrl;
            this.Links = links;
        }
    }
}