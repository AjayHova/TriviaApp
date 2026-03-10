using TriviaApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Required for reading/writing cookies in Blazor Server
builder.Services.AddHttpContextAccessor();

// AppState and auth are Scoped = one instance per Blazor circuit
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<CookieAuthService>();
builder.Services.AddScoped<FirebaseAuthService>();

// Stateless services
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<QuestionService>();

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
