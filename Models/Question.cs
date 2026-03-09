namespace TriviaApp.Models;

public class Question
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string Answer { get; set; } = "";
}
