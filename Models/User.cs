namespace TriviaApp.Models;

public class User
{
    public string Uid { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public int Score { get; set; }
    public Dictionary<string, AnswerRecord> History { get; set; } = new();
}

public class AnswerRecord
{
    public string Selected { get; set; } = "";
    public bool Correct { get; set; }
}
