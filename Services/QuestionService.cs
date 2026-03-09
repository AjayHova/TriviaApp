using TriviaApp.Models;

namespace TriviaApp.Services;

public class QuestionService
{
    public Question? GetTodaysQuestion(List<Question> questions)
    {
        if (!questions.Any()) return null;
        var seed = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
        return questions[seed % questions.Count];
    }

    public static string TodayKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");
}
