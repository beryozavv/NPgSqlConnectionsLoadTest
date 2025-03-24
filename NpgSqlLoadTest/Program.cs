using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Npgsql;

namespace NpgSqlLoadTest;

using System;

class Program
{
    private const int SleepTimeout = 10;
    private const int TotalConnections = 600;
    private const int Parallelism = 600;

    private static readonly string ConnectionString =
        "Server=localhost;Port=7432;Database=postgres;User Id=postgres;Password=postgres;" +
        "Pooling=true;Connection Idle Lifetime=300;Minimum Pool Size=10;Maximum Pool Size=480;" +
        "Keepalive=100;Tcp Keepalive=true;Tcp Keepalive Time=300;Tcp Keepalive Interval=30;" +
        "No Reset On Close=false";

    static async Task Main()
    {
        var testId = Guid.NewGuid();

        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} Test with Id = {testId} started. Total connections: {TotalConnections}. SleepTimeout: {SleepTimeout}");

        // Настройка ActionBlock с контролем параллелизма
        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Parallelism // Ограничение параллельных задач [[3]]
        };

        var actionBlock = new ActionBlock<int>(async i =>
        {
            try
            {
                await TestConnection(i, testId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now.ToString("O")} Ошибка: {ex.Message}. Exception: {ex}");
            }
        }, options);

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        // Запуск итераций
        for (var i = 0; i < TotalConnections; i++)
        {
            actionBlock.Post(i); // Отправка данных в блок [[1]]
        }

        actionBlock.Complete(); // Завершение приёма сообщений
        await actionBlock.Completion; // Ожидание завершения всех операций [[5]]

        stopwatch.Stop();
        Console.WriteLine($"{DateTime.Now.ToString("O")} Test completed in {stopwatch.ElapsedMilliseconds} ms.");

        var rowsCount = await CheckRowsCount(testId);
        
        Console.WriteLine($"{DateTime.Now.ToString("O")} Test {testId} added {rowsCount} rows. Total connections: {TotalConnections}");
    }

    private static async Task<int> CheckRowsCount(Guid testId)
    {
        using (var conn = new NpgsqlConnection(ConnectionString))
        {
            conn.Open();
            using (var cmd = new NpgsqlCommand(
                       $"SELECT COUNT(*) FROM test WHERE testid = @testid",
                       conn))
            {
                cmd.Parameters.AddWithValue("testid", testId); // Безопасная передача значения [[1]]
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
        }
    }

    static async Task TestConnection(int i, Guid testId)
    {
        try
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand("SELECT pg_sleep(@timeout)", conn))
                {
                    cmd.Parameters.AddWithValue("timeout", SleepTimeout); // Фиксированная задержка
                    //cmd.CommandTimeout = tm;
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new NpgsqlCommand(
                           "INSERT INTO test (testvalue, datevalue, testid) VALUES (@testvalue, @datevalue, @testid)",
                           conn))
                {
                    var dateTime = DateTime.Now;
                    // Добавление параметров
                    cmd.Parameters.AddWithValue("testvalue", $"index = {i}, TS = {dateTime.ToString("O")}");
                    cmd.Parameters.AddWithValue("datevalue", dateTime);
                    cmd.Parameters.AddWithValue("testid", testId);
                    // Выполнение запроса
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now.ToString("O")} Error: {ex.Message} {ex}");
        }
    }
}