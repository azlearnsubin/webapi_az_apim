using WebApi.Models;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using WebApi.EFCoreContext;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var serviceBusNamespace = builder.Configuration["ServiceBus:Namespace"];
var useConnectionString = builder.Configuration.GetValue<bool>("ServiceBus:UseConnectionString", false);
builder.Services.AddSingleton<ServiceBusClient>(provider =>
{
    if (useConnectionString)
    {
        var connectionString = builder.Configuration.GetConnectionString("ServiceBus");
        return new ServiceBusClient(connectionString);
    }
    else
    {
        // Use Managed Identity (recommended for production)
        var credential = new DefaultAzureCredential();
        return new ServiceBusClient($"https://{serviceBusNamespace}/", credential);
    }
    
});

//Register Sender and processors
builder.Services.AddScoped<ServiceBusSender>(provider =>
{
    var client = provider.GetService<ServiceBusClient>();
    return client!.CreateSender("myqueue");
});

builder.Services.AddScoped<ServiceBusProcessor>(provider =>
{
    var client = provider.GetService<ServiceBusClient>();
    return client!.CreateProcessor("myqueue", new ServiceBusProcessorOptions());
});

//Azure SQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

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

app.MapGet("/jwt-info", (HttpContext context) =>
{
    var jwtInfo = new
    {
        Message = "JWT validation successful!",
        ExtractedClaims = new
        {
            UserId = context.Request.Headers["X-User-ID"].FirstOrDefault(),
            UserName = context.Request.Headers["X-User-Name"].FirstOrDefault(),
            UserEmail = context.Request.Headers["X-User-Email"].FirstOrDefault(),
            UserRoles = context.Request.Headers["X-User-Roles"].FirstOrDefault()
        },
        AllHeaders = context.Request.Headers
            .Where(h => h.Key.StartsWith("X-"))
            .ToDictionary(h => h.Key, h => h.Value.ToString()),
        Timestamp = DateTime.UtcNow
    };
    
    return Results.Ok(jwtInfo);
});

app.MapGet("/admin-only", (HttpContext context) =>
{
    var userRoles = context.Request.Headers["X-User-Roles"].FirstOrDefault() ?? "";
    
    if (!userRoles.Contains("admin"))
    {
        return Results.Forbid();
    }
    
    return Results.Ok(new
    {
        Message = "Welcome admin!",
        AdminData = "Secret admin information",
        UserId = context.Request.Headers["X-User-ID"].FirstOrDefault()
    });
});

// Send message to queue
app.MapPost("/send-message", async (ServiceBusSender sender, string message) =>
{
    try
    {
        var serviceBusMessage = new ServiceBusMessage(message);
        await sender.SendMessageAsync(serviceBusMessage);
        return Results.Ok($"Message sent: {message}");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

// Send message to topic
app.MapPost("/send-topic-message", async (ServiceBusClient client, string message) =>
{
    try
    {
        var sender = client.CreateSender("mytopic");
        var serviceBusMessage = new ServiceBusMessage(message);
        await sender.SendMessageAsync(serviceBusMessage);
        await sender.DisposeAsync();
        return Results.Ok($"Topic message sent: {message}");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

// Get messages from queue
app.MapGet("/receive-messages", async (ServiceBusClient client) =>
{
    try
    {
        //PeekLock: Message is locked for processing, must be completed/abandoned
        //ReceiveAndDelete: Message is immediately removed (less reliable)
        var receiver = client.CreateReceiver("myqueue");
        var messages = new List<string>();
        
        // Receive up to 10 messages
        var receivedMessages = await receiver.ReceiveMessagesAsync(maxMessages: 10, maxWaitTime: TimeSpan.FromSeconds(5));
        
        foreach (var message in receivedMessages)
        {
            messages.Add(message.Body.ToString());
            await receiver.CompleteMessageAsync(message); // Remove from queue
        }
        
        await receiver.DisposeAsync();
        return Results.Ok(messages);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

// Background service for processing messages
app.MapPost("/start-message-processor", async (ServiceBusProcessor processor) =>
{
    processor.ProcessMessageAsync += async args =>
    {
        var message = args.Message.Body.ToString();
        Console.WriteLine($"Processed message: {message}");
        await args.CompleteMessageAsync(args.Message);
    };

    processor.ProcessErrorAsync += args =>
    {
        Console.WriteLine($"Error: {args.Exception}");
        return Task.CompletedTask;
    };

    await processor.StartProcessingAsync();
    return Results.Ok("Message processor started");
});

// Database operations
app.MapGet("/products", async (AppDbContext context) =>
{
    try
    {
        var products = await context.Products.ToListAsync();
        return Results.Ok(products);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving products: {ex.Message}");
    }
});


//Apply migrations
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Test connection first
        if (await context.Database.CanConnectAsync())
        {
            try
            {
                if (!await context.Products.AnyAsync())
                {
                    Console.WriteLine("Seeding initial data...");
                    context.Products.AddRange(
                        new Product { Name = "Laptop", Price = 999.99m, Description = "Gaming laptop" },
                        new Product { Name = "Mouse", Price = 29.99m, Description = "Wireless mouse" },
                        new Product { Name = "Keyboard", Price = 79.99m, Description = "Mechanical keyboard" }
                    );
                    await context.SaveChangesAsync();
                    Console.WriteLine("Data seeding completed");
                }
                else
                {
                    Console.WriteLine("Data already exists, skipping seeding");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        else
        {
            Console.WriteLine("Could not connect to database during startup");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Startup database operation error: {ex.Message}");
    }
}

app.Run();