// Models/SearchRequest.cs
public class SearchRequest
{
    public string Query { get; set; }
    public string Location { get; set; }
}

// Models/ExpertLink.cs
public class ExpertLink
{
    public string Title { get; set; }
    public string Url { get; set; }
    public string Snippet { get; set; }
}

// Models/SearchResponse.cs
public class SearchResponse
{
    public string Answer { get; set; }     // ChatGPT answer
    public string Topic { get; set; }      // Extracted topic
    public List<ExpertLink> Experts { get; set; } = new();
}
