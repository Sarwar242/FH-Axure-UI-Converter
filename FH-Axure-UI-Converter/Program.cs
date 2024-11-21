using Core.Converters;
using Core.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IConverterLogger, ConverterLogger>();
builder.Services.AddSingleton<IPrimeConverter>(sp =>
{
    var logger = sp.GetRequiredService<IConverterLogger>();
    var mappingFilePath = Path.Combine(builder.Environment.ContentRootPath, "control-mappings.json");
    return new PrimeConverter(mappingFilePath, logger);
});


//var configFolder = Path.Combine(Directory.GetCurrentDirectory(), "Mappings");
//foreach (var file in Directory.GetFiles(configFolder, "*.json"))
//{
//    builder.Configuration.AddJsonFile(file, optional: false, reloadOnChange: true);
//}
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
