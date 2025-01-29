var builder = WebApplication.CreateBuilder(args);

// config.json dosyasını yükle
builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
