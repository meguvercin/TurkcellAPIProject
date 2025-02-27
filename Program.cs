var builder = WebApplication.CreateBuilder(args);

// config.json dosyasını yükle
builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllers();

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("http://localhost:5173") // React'in çalıştığı URL
                                .AllowAnyMethod()
                                .AllowAnyHeader();
                      });
});


var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseCors(MyAllowSpecificOrigins);


app.Run();
