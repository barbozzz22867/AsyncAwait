using AsyncMonitor;
using System.Diagnostics;

var service = new CheckService();
var checks = new (string Name, TimeSpan Delay)[]
{
    ("Сеть",        TimeSpan.FromMilliseconds(700)),
    ("База данных", TimeSpan.FromMilliseconds(900)),
    ("Файлы",       TimeSpan.FromMilliseconds(500))
};

// Для отмены «долгой» проверки, запущенной в фоне.
CancellationTokenSource? longRunCts = null;
Task? longRunTask = null;

while (true)
{
    Console.WriteLine("\n1 — одна проверка; 2 — последовательно; 3 — параллельно;");
    Console.WriteLine("4 — длительная проверка (в фоне); c — отменить длительную; 0 — выход");
    var cmd = Console.ReadLine();

    if (cmd == "0")
        break;

    try
    {
        switch (cmd)
        {
            case "1":
                // TODO 8: await, без .Wait()/.Result.
                {
                    var r = await service.CheckServiceAsync("Сеть", TimeSpan.FromMilliseconds(700));
                    Print(new[] { r });
                }
                break;

            case "2":
                {
                    var sw = Stopwatch.StartNew();
                    var r = await service.RunSequentialAsync(checks);
                    sw.Stop();
                    Console.WriteLine($"Последовательно: {sw.ElapsedMilliseconds} мс");
                    Print(r);
                }
                break;

            case "3":
                {
                    var sw = Stopwatch.StartNew();
                    var r = await service.RunParallelAsync(checks);
                    sw.Stop();
                    Console.WriteLine($"Параллельно: {sw.ElapsedMilliseconds} мс");
                    Print(r);
                }
                break;

            // TODO 10: длительная проверка в фоне — меню не блокируется.
            case "4":
                {
                    if (longRunTask is { IsCompleted: false })
                    {
                        Console.WriteLine("Длительная проверка ещё выполняется. Дождитесь или отмените (c).");
                        break;
                    }
                    longRunCts = new CancellationTokenSource();
                    var token = longRunCts.Token;
                    Console.WriteLine("Запуск длительной проверки (5 с) в фоне. Меню доступно: нажмите 'c' для отмены.");

                    // ВАЖНО: НЕ await-им здесь, чтобы вернуться в меню.
                    longRunTask = Task.Run(async () =>
                    {
                        try
                        {
                            var r = await service.CheckServiceAsync("Длительная", TimeSpan.FromSeconds(5), token);
                            Print(new[] { r });
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("[фон] Длительная проверка отменена.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[фон] Ошибка: {ex.GetType().Name}: {ex.Message}");
                        }
                    });
                }
                break;

            case "c":
                if (longRunCts is null || longRunTask is null || longRunTask.IsCompleted)
                    Console.WriteLine("Нечего отменять.");
                else
                {
                    longRunCts.Cancel();
                    Console.WriteLine("Запрошена отмена длительной проверки.");
                }
                break;

            default:
                Console.WriteLine("Неизвестная команда.");
                break;
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Проверка отменена.");
    }
    catch (Exception ex)
    {
        // TODO 9: Не скрывать — сообщать тип и причину.
        Console.WriteLine($"Ошибка ({ex.GetType().Name}): {ex.Message}");
    }
}

static void Print(IEnumerable<CheckResult> xs)
{
    foreach (var r in xs)
        Console.WriteLine($"  [{(r.Success ? "OK" : "FAIL")}] {r.Name} — {r.Duration.TotalMilliseconds:F0} мс — {r.Message}");
}