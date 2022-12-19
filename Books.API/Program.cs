using Books.API.Contexts;
using Books.API.Services;
using Microsoft.EntityFrameworkCore;

// throttle the thread pool (set available threads to amount of processors)
ThreadPool.SetMaxThreads(Environment.ProcessorCount, Environment.ProcessorCount);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<BookContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration["ConnectionStrings:BooksDBConnectionString"]);
});

builder.Services.AddScoped<IBooksRepository, BooksRepository>();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
