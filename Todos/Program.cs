using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.VisualBasic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());

var app = builder.Build();

app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

var todos = new List<Todo>();

app.MapGet("/todos", (ITaskService service) => service.GetTodos());

app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITaskService service) =>
{
    var targetTodo = service.GetTodoById(id);
    return targetTodo is null
    ? TypedResults.NotFound()
    : TypedResults.Ok(targetTodo);

});

app.MapPost("/todos", (Todo task, ITaskService service) =>
{
    service.AddTodo(task);
    return TypedResults.Created("/todos/{id}", task);
})
.AddEndpointFilter(async (context, next) =>
{
    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();
    if (taskArgument.DueDate < DateTime.UtcNow)
    {
        errors.Add(nameof(Todo.DueDate), ["Cannot have due date in the past"]);
    }
    if (taskArgument.IsCompleted)
    {
        errors.Add(nameof(Todo.IsCompleted), ["Cannot add completed Todo."]);
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    return await next(context);
});

app.MapPut("/todos/{id}", Results<Ok<Todo>, NotFound>(int id, Todo task, ITaskService service) =>
{
    var updatedTask = service.UpdateTodo(id, task);
    if (updatedTask == null) return TypedResults.NotFound();

    return TypedResults.Ok(task);
});

app.MapDelete("/todos/{id}", (int id, ITaskService service) =>
{
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});

app.MapDelete("/todos", (ITaskService service) =>
{
    service.DeleteAll();
    
    return TypedResults.NoContent();
});

app.Run();

// app.MapGet("/todos", () => todos);

// app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id) =>
// {
//     var targetTodo = todos.SingleOrDefault(t => id == t.Id);
//     return targetTodo is null
//     ? TypedResults.NotFound()
//     : TypedResults.Ok(targetTodo);

// });

// app.MapPost("/todos", (Todo task) =>
// {
//     todos.Add(task);
//     return TypedResults.Created("/todos/{id}", task);
// })
// .AddEndpointFilter(async (context, next) =>
// {
//     var taskArgument = context.GetArgument<Todo>(0);
//     var errors = new Dictionary<string, string[]>();
//     if (taskArgument.DueDate < DateTime.UtcNow)
//     {
//         errors.Add(nameof(Todo.DueDate), ["Cannot have due date in the past"]);
//     }
//     if (taskArgument.IsCompleted)
//     {
//         errors.Add(nameof(Todo.IsCompleted), ["Cannot add completed Todo."]);
//     }

//     if (errors.Count > 0)
//     {
//         return Results.ValidationProblem(errors);
//     }

//     return await next(context);
// });

// app.MapPut("/todos/{id}", Results<Ok<Todo>, NotFound>(int id, Todo task) =>
// {
//     var index = todos.FindIndex(t => id == t.Id);
//     if (index == -1) return TypedResults.NotFound();
//     todos[index] = task;

//     return TypedResults.Ok(task);
// });

// app.MapDelete("/todos/{id}", (int id) =>
// {
//     todos.RemoveAll(t => id == t.Id);
//     return TypedResults.NoContent();
// });

// app.MapDelete("/todos", () =>
// {
//     todos.Clear();
//     return TypedResults.NoContent();
// });

// app.Run();

public record Todo(int Id, string Name, DateTime DueDate, bool IsCompleted);

interface ITaskService
{
    Todo? GetTodoById(int id);

    List<Todo> GetTodos();

    void DeleteTodoById(int id);

    void DeleteAll();

    Todo AddTodo(Todo task);

    Todo? UpdateTodo(int id, Todo task);
}

class InMemoryTaskService : ITaskService
{
    private readonly List<Todo> _todos = [];

    public Todo AddTodo(Todo task)
    {
        _todos.Add(task);
        return task;
    }

    public void DeleteTodoById(int id)
    {
        _todos.RemoveAll(task => id == task.Id);
    }

    public void DeleteAll()
    {
        _todos.Clear();
    }

    public Todo? GetTodoById(int id)
    {
        return _todos.SingleOrDefault(t => id == t.Id);
    }

    public List<Todo> GetTodos()
    {
        return _todos;
    }

    public Todo? UpdateTodo(int id, Todo task)
    {
        var index = _todos.FindIndex(t => id == t.Id);
        if (index == -1) return null;

        _todos[index] = task;

        return _todos[index];
}
}