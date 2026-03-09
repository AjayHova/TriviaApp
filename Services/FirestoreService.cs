using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using TriviaApp.Models;

namespace TriviaApp.Services;

public class FirestoreService
{
    private readonly HttpClient _http;
    private readonly string _projectId;
    private readonly string _baseUrl;

    public FirestoreService(IConfiguration config)
    {
        _http = new HttpClient();
        _projectId = config["Firebase:ProjectId"]!;
        _baseUrl = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static JsonObject ToFirestoreValue(object? val) => val switch
    {
        string s => new JsonObject { ["stringValue"] = s },
        int i    => new JsonObject { ["integerValue"] = i.ToString() },
        bool b   => new JsonObject { ["booleanValue"] = b },
        _        => new JsonObject { ["nullValue"] = JsonValue.Create<object?>(null) }
    };

    private static string? StringVal(JsonElement el)
    {
        if (el.TryGetProperty("stringValue", out var v)) return v.GetString();
        if (el.TryGetProperty("integerValue", out var i)) return i.GetString();
        return null;
    }

    private static int IntVal(JsonElement el)
    {
        if (el.TryGetProperty("integerValue", out var v))
            return int.TryParse(v.GetString(), out var n) ? n : 0;
        return 0;
    }

    private static bool BoolVal(JsonElement el)
    {
        if (el.TryGetProperty("booleanValue", out var v)) return v.GetBoolean();
        return false;
    }

    private HttpRequestMessage AuthRequest(HttpMethod method, string url, string? token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (token != null)
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (body != null)
            req.Content = JsonContent.Create(body);
        return req;
    }

    // ── Questions ─────────────────────────────────────────────────────────────

    public async Task<List<Question>> GetQuestionsAsync()
    {
        var res = await _http.GetAsync($"{_baseUrl}/questions");
        if (!res.IsSuccessStatusCode) return new();

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        if (!json.TryGetProperty("documents", out var docs)) return new();

        return docs.EnumerateArray().Select(d =>
        {
            var fields = d.GetProperty("fields");
            var name = d.GetProperty("name").GetString()!;
            var id = name.Split('/').Last();

            var options = new List<string>();
            if (fields.TryGetProperty("options", out var opts) &&
                opts.TryGetProperty("arrayValue", out var arrVal) &&
                arrVal.TryGetProperty("values", out var values))
            {
                options = values.EnumerateArray()
                    .Select(v => v.TryGetProperty("stringValue", out var sv) ? sv.GetString()! : "")
                    .ToList();
            }

            return new Question
            {
                Id      = id,
                Text    = StringVal(fields.GetProperty("question")) ?? "",
                Options = options,
                Answer  = StringVal(fields.GetProperty("answer")) ?? ""
            };
        }).ToList();
    }

    public async Task SaveQuestionsAsync(List<Question> questions, string token)
    {
        foreach (var (q, i) in questions.Select((q, i) => (q, i)))
        {
            var body = new JsonObject
            {
                ["fields"] = new JsonObject
                {
                    ["question"] = ToFirestoreValue(q.Text),
                    ["answer"]   = ToFirestoreValue(q.Answer),
                    ["options"]  = new JsonObject
                    {
                        ["arrayValue"] = new JsonObject
                        {
                            ["values"] = new JsonArray(
                                q.Options.Select(o => (JsonNode)new JsonObject { ["stringValue"] = o }).ToArray()
                            )
                        }
                    }
                }
            };

            var req = AuthRequest(HttpMethod.Patch, $"{_baseUrl}/questions/q_{i}", token, body);
            await _http.SendAsync(req);
        }
    }

    // ── User ──────────────────────────────────────────────────────────────────

    public async Task<User?> GetUserAsync(string uid, string token)
    {
        var req = AuthRequest(HttpMethod.Get, $"{_baseUrl}/users/{uid}", token);
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        if (!json.TryGetProperty("fields", out var fields)) return null;

        var history = new Dictionary<string, AnswerRecord>();
        if (fields.TryGetProperty("history", out var hist) &&
            hist.TryGetProperty("mapValue", out var mapVal) &&
            mapVal.TryGetProperty("fields", out var histFields))
        {
            foreach (var entry in histFields.EnumerateObject())
            {
                if (entry.Value.TryGetProperty("mapValue", out var mv) &&
                    mv.TryGetProperty("fields", out var rf))
                {
                    history[entry.Name] = new AnswerRecord
                    {
                        Selected = StringVal(rf.GetProperty("selected")) ?? "",
                        Correct  = BoolVal(rf.GetProperty("correct"))
                    };
                }
            }
        }

        return new User
        {
            Uid      = uid,
            Username = StringVal(fields.GetProperty("username")) ?? "",
            Email    = StringVal(fields.GetProperty("email")) ?? "",
            Score    = IntVal(fields.GetProperty("score")),
            History  = history
        };
    }

    public async Task SaveUserAsync(User user, string token)
    {
        var historyFields = new JsonObject();
        foreach (var (date, record) in user.History)
        {
            historyFields[date] = new JsonObject
            {
                ["mapValue"] = new JsonObject
                {
                    ["fields"] = new JsonObject
                    {
                        ["selected"] = ToFirestoreValue(record.Selected),
                        ["correct"]  = ToFirestoreValue(record.Correct)
                    }
                }
            };
        }

        var body = new JsonObject
        {
            ["fields"] = new JsonObject
            {
                ["username"] = ToFirestoreValue(user.Username),
                ["email"]    = ToFirestoreValue(user.Email),
                ["score"]    = ToFirestoreValue(user.Score),
                ["history"]  = new JsonObject
                {
                    ["mapValue"] = new JsonObject { ["fields"] = historyFields }
                }
            }
        };

        var req = AuthRequest(HttpMethod.Patch, $"{_baseUrl}/users/{user.Uid}", token, body);
        await _http.SendAsync(req);
    }

    public async Task CreateUserAsync(User user, string token)
    {
        await SaveUserAsync(user, token);
        await UpdateLeaderboardAsync(user.Uid, user.Username, 0, token);
    }

    // ── Leaderboard ───────────────────────────────────────────────────────────

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync()
    {
        var res = await _http.GetAsync($"{_baseUrl}/leaderboard");
        if (!res.IsSuccessStatusCode) return new();

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        if (!json.TryGetProperty("documents", out var docs)) return new();

        return docs.EnumerateArray().Select(d =>
        {
            var fields = d.GetProperty("fields");
            var name = d.GetProperty("name").GetString()!;
            return new LeaderboardEntry
            {
                Uid      = name.Split('/').Last(),
                Username = StringVal(fields.GetProperty("username")) ?? "",
                Score    = IntVal(fields.GetProperty("score"))
            };
        }).OrderByDescending(e => e.Score).ToList();
    }

    public async Task UpdateLeaderboardAsync(string uid, string username, int score, string token)
    {
        var body = new JsonObject
        {
            ["fields"] = new JsonObject
            {
                ["username"] = ToFirestoreValue(username),
                ["score"]    = ToFirestoreValue(score)
            }
        };

        var req = AuthRequest(HttpMethod.Patch, $"{_baseUrl}/leaderboard/{uid}", token, body);
        await _http.SendAsync(req);
    }
}
