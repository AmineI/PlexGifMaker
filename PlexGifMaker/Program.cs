using log4net;
using log4net.Config;
using Microsoft.AspNetCore.DataProtection;
using PlexGifMaker.Data;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure log4net
var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();
builder.Services.AddTransient<PlexService>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(@"/usr/shared/plexgifmaker_keys"));

// Configure HTTPS redirection
//builder.Services.AddHttpsRedirection(options =>
//{
//    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
//    options.HttpsPort = 443;
//});

// Configure Kestrel and HTTPS
//builder.WebHost.UseKestrel(options =>
//{
//    options.Listen(IPAddress.Any, 443, listenOptions =>
//    {
//        listenOptions.UseHttps("/https/aspnetapp.pfx", "abcde"); 
//    });
//});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    //app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
//app.UseAuthentication();
//app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
public partial class Program { }