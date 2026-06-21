using BaseballGame.Components;
using BaseballGame.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<NpbScraperService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BaseballGame/1.0 (personal local use)");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<PredictionService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// HTTPリクエストパイプラインを設定する
// 本番環境のみエラーハンドラーとHSTSを有効化する
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // HSTSのデフォルト有効期間は30日（本番運用では変更を検討すること）
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
