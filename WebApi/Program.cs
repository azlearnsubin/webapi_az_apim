using WebApi.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/posts", async (HttpClient httpClient) =>
    {
        var response = await httpClient.GetStringAsync("https://jsonplaceholder.typicode.com/posts");
        var posts = JsonSerializer.Deserialize<Post[]>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return posts;
    })
    .WithName("GetPosts")
    .WithOpenApi();

app.MapGet("/posts/{id}", async (int id, HttpClient httpClient) =>
    {
        var response = await httpClient.GetStringAsync($"https://jsonplaceholder.typicode.com/posts/{id}");
        var post = JsonSerializer.Deserialize<Post>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return post;
    })
    .WithName("GetPost")
    .WithOpenApi();

app.MapGet("/users", async (HttpClient httpClient) =>
    {
        var response = await httpClient.GetStringAsync("https://jsonplaceholder.typicode.com/users");
        var users = JsonSerializer.Deserialize<User[]>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return users;
    })
    .WithName("GetUsers")
    .WithOpenApi();

app.MapGet("/users/{id}", async (int id, HttpClient httpClient) =>
    {
        var response = await httpClient.GetStringAsync($"https://jsonplaceholder.typicode.com/users/{id}");
        var user = JsonSerializer.Deserialize<User>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return user;
    })
    .WithName("GetUser")
    .WithOpenApi();

app.MapPost("/posts", async (Post post, HttpClient httpClient) =>
    {
        var json = JsonSerializer.Serialize(post, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("https://jsonplaceholder.typicode.com/posts", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var createdPost = JsonSerializer.Deserialize<Post>(responseContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Results.Created($"/posts/{createdPost?.Id}", createdPost);
    })
    .WithName("CreatePost")
    .WithOpenApi();

app.MapPost("/users", async (User user, HttpClient httpClient) =>
    {
        var json = JsonSerializer.Serialize(user, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("https://jsonplaceholder.typicode.com/users", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var createdUser = JsonSerializer.Deserialize<User>(responseContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Results.Created($"/users/{createdUser?.Id}", createdUser);
    })
    .WithName("CreateUser")
    .WithOpenApi();

app.Run();