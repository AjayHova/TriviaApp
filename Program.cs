using TriviaApp.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// AppState is Scoped = one instance per Blazor circuit (per browser tab)
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<FirebaseAuthService>();

// FirestoreService and QuestionService are stateless, safe as Singleton
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<QuestionService>();

//Hide the firebase api key
var apiKey = builder.Configuration["Firebase:ApiKey"];


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<TriviaApp.Components.App>().AddInteractiveServerRenderMode();

app.Run();
