public interface IGoogleSearchService
{
    Task<List<ExpertLink>> SearchAsync(string topic, string location, int maxResults = 10);
}
