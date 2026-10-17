using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<TodoContext>(options =>
	options.UseInMemoryDatabase("TodoList"));

var app = builder.Build();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();