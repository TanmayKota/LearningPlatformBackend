public interface IOpenAiService
{
    Task<string> GetAnswerAsync(string userQuery);
    Task<string> ExtractTopicAsync(string userQuery);
}
