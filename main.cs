using System.Text.Json;

Console.OutputEncoding = System.Text.Encoding.UTF8;

using var manager = new TaskManager();
await manager.LoadAsync();
manager.StartBackgroundChecker();


bool running = true;
while (running)
{
    Console.WriteLine("\n=== TaskHub ===");
    Console.WriteLine("1. Добавить задачу");
    Console.WriteLine("2. Просмотр задач");
    Console.WriteLine("3. Редактировать задачу");
    Console.WriteLine("4. Удалить задачу");
    Console.WriteLine("5. Поиск задач");
    Console.WriteLine("6. Статистика");
    Console.WriteLine("7. Сохранить");
    Console.WriteLine("0. Выход");
    Console.Write("Выбор: ");

    switch (Console.ReadLine())
    {
        case "1": AddTask(); break;
        case "2": ViewTasks(); break;
        case "3": EditTask(); break;
        case "4": DeleteTask(); break;
        case "5": SearchTasks(); break;
        case "6": ShowStats(); break;
        case "7": await manager.SaveAsync(); Console.WriteLine("Сохранено."); break;
        case "0": running = false; break;
        default: Console.WriteLine("Неверный выбор."); break;
    }
}



await manager.SaveAsync();




void AddTask()
{
    Console.Write("Название: ");
    var title = Console.ReadLine() ?? "";

    Console.Write("Описание: ");
    var desc = Console.ReadLine() ?? "";

    Console.Write("Приоритет (0=Low, 1=Medium, 2=High): ");
    var priority = ParseEnum<Priority>(Console.ReadLine(), Priority.Medium);

    Console.Write("Дедлайн (дд.мм.гггг): ");
    if (!DateTime.TryParse(Console.ReadLine(), out var deadline))
        deadline = DateTime.Now.AddDays(7);

    manager.Add(new TaskItem
    {
        Id = manager.NextId(),
        Title = title,
        Description = desc,
        Priority = priority,
        Deadline = deadline,
        Status = Status.New
    });

    Console.WriteLine("Задача добавлена.");
}




void ViewTasks()
{
    Console.WriteLine("1. Все\n2. Выполненные\n3. Невыполненные\n4. Высокий приоритет");
    Console.Write("Выбор: ");

    var list = Console.ReadLine() switch
    {
        "2" => Utils.Filter(manager.Tasks, t => t.Status == Status.Done),
        "3" => Utils.Filter(manager.Tasks, t => t.Status != Status.Done),
        "4" => Utils.Filter(manager.Tasks, t => t.Priority == Priority.High),
        _   => manager.Tasks
    };

    PrintList(list);
}




void EditTask()
{
    Console.Write("Id задачи: ");
    if (!int.TryParse(Console.ReadLine(), out int id)) return;

    var task = manager.GetById(id);
    if (task == null) { Console.WriteLine("Не найдено."); return; }

    Console.WriteLine($"Название [{task.Title}]: ");
    var title = Console.ReadLine();
    if (!string.IsNullOrEmpty(title)) task.Title = title;

    Console.WriteLine($"Описание [{task.Description}]: ");
    var desc = Console.ReadLine();
    if (!string.IsNullOrEmpty(desc)) task.Description = desc;

    Console.Write("Приоритет (0=Low, 1=Medium, 2=High, Enter — пропустить): ");
    var pInput = Console.ReadLine();
    if (!string.IsNullOrEmpty(pInput))
        task.Priority = ParseEnum<Priority>(pInput, task.Priority);

    Console.Write("Статус (0=New, 1=InProgress, 2=Done, Enter — пропустить): ");
    var sInput = Console.ReadLine();
    if (!string.IsNullOrEmpty(sInput))
        task.Status = ParseEnum<Status>(sInput, task.Status);

    Console.WriteLine("Задача обновлена.");
}




void DeleteTask()
{
    Console.Write("Id задачи: ");
    if (!int.TryParse(Console.ReadLine(), out int id)) return;

    if (manager.Remove(id))
        Console.WriteLine("Удалено.");
    else
        Console.WriteLine("Не найдено.");
}





void SearchTasks()
{
    Console.WriteLine("1. По названию\n2. По статусу\n3. По приоритету");
    Console.Write("Выбор: ");

    List<TaskItem> result;
    switch (Console.ReadLine())
    {
        case "1":
            Console.Write("Название: ");
            var name = Console.ReadLine() ?? "";
            result = Utils.Filter(manager.Tasks, t => t.Title.Contains(name, StringComparison.OrdinalIgnoreCase));
            break;
        case "2":
            Console.Write("Статус (0=New, 1=InProgress, 2=Done): ");
            var status = ParseEnum<Status>(Console.ReadLine(), Status.New);
            result = Utils.Filter(manager.Tasks, t => t.Status == status);
            break;
        case "3":
            Console.Write("Приоритет (0=Low, 1=Medium, 2=High): ");
            var priority = ParseEnum<Priority>(Console.ReadLine(), Priority.Medium);
            result = Utils.Filter(manager.Tasks, t => t.Priority == priority);
            break;
        default:
            return;
    }

    PrintList(result);
}




void ShowStats()
{
    var tasks = manager.Tasks;
    int done = Utils.Filter(tasks, t => t.Status == Status.Done).Count;
    int overdue = Utils.Filter(tasks, t => t.Deadline < DateTime.Now && t.Status != Status.Done).Count;

    var byPriority = new Dictionary<Priority, int>();
    foreach (Priority p in Enum.GetValues<Priority>())
        byPriority[p] = Utils.Filter(tasks, t => t.Priority == p).Count;

    Console.WriteLine($"\nВсего задач: {tasks.Count}");
    Console.WriteLine($"Выполнено: {done}");
    Console.WriteLine($"Просрочено: {overdue}");
    Console.WriteLine("По приоритетам:");
    foreach (var kv in byPriority)
        Console.WriteLine($"  {kv.Key}: {kv.Value}");
}




void PrintList(List<TaskItem> list)
{
    if (list.Count == 0) { Console.WriteLine("Нет задач."); return; }
    foreach (var t in list)
        Console.WriteLine($"[{t.Id}] {t.Title} | {t.Priority} | {t.Status} | до {t.Deadline:dd.MM.yyyy} | {t.Description}");
}





T ParseEnum<T>(string? input, T fallback) where T : struct, Enum
{
    if (int.TryParse(input, out int val) && Enum.IsDefined(typeof(T), val))
        return (T)(object)val;
    return fallback;
}






enum Priority { Low, Medium, High }
enum Status { New, InProgress, Done }




class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Priority Priority { get; set; }
    public DateTime Deadline { get; set; }
    public Status Status { get; set; }
}







class TaskManager : IDisposable
{
    private List<TaskItem> tasks = new();
    private CancellationTokenSource cts = new();
    private const string FilePath = "tasks.json";

    public List<TaskItem> Tasks => tasks;

    public int NextId() => tasks.Count == 0 ? 1 : tasks.Max(t => t.Id) + 1;

    public void Add(TaskItem task) => tasks.Add(task);

    public bool Remove(int id)
    {
        var task = GetById(id);
        if (task == null) return false;
        tasks.Remove(task);
        return true;
    }

    public TaskItem? GetById(int id) => tasks.FirstOrDefault(t => t.Id == id);

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(FilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения: {ex.Message}");
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = await File.ReadAllTextAsync(FilePath);
            tasks = JsonSerializer.Deserialize<List<TaskItem>>(json) ?? new();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки: {ex.Message}");
        }
    }

    public void StartBackgroundChecker()
    {
        Task.Run(() => CheckDeadlines(cts.Token));
    }

    private async Task CheckDeadlines(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(5000, token); }
            catch (TaskCanceledException) { break; }

            foreach (var t in tasks.ToList())
            {
                if (t.Deadline < DateTime.Now && t.Status != Status.Done)
                    Console.WriteLine($"\n[!] Просрочена: {t.Title} (Id={t.Id})");
            }
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        cts.Dispose();
    }
}






static class Utils
{
    public static List<T> Filter<T>(List<T> source, Func<T, bool> predicate)
    {
        var result = new List<T>();
        foreach (var item in source)
            if (predicate(item))
                result.Add(item);
        return result;
    }
}
