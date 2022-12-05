using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AssetRipper.GUI;
using AssetRipper.Core.Logging;

namespace Plateup_GetItems;

internal class Program
{
    private readonly string _path, _tmp_path, _extracted;
    private ConcurrentDictionary<string, Item> items;
    Program(string path)
    {
        _path = Path.GetFullPath(path);
        _tmp_path = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LFInteractive", "Plateup", Path.GetRandomFileName())).FullName;
        _extracted = Directory.CreateDirectory(Path.Combine(_tmp_path, "extracted")).FullName;

        ConsoleApp.Run(new string[] { _path }, _extracted);

        items = new();

        string mono = Path.Combine(_extracted, "ExportedProject", "Assets", "MonoBehaviour");
        string[] extracted_files = Directory.GetFiles(mono, "*.asset", SearchOption.AllDirectories);

        GetBaseInfo(extracted_files.Where(i => !new FileInfo(i).Name.Contains(" - Info")).ToArray());
        GetEnglishInfo(extracted_files.Where(i => new FileInfo(i).Name.StartsWith("English - Info")).ToArray());
        Export();
        Task.Run(() => Cleanup()).Wait();
    }

    void Cleanup(int index = 0)
    {
        try
        {
            Directory.Delete(_tmp_path, true);
        }
        catch
        {
            if (index < 10)
            {
                Logger.Info($"Attempting to Cleanup: {index}");
                Thread.Sleep(1000);
                Cleanup(index + 1);
            }
        }
    }

    static void Main(string[] args)
    {
        Logger.Add(new ConsoleLogger(false));
        if (!args.Any())
        {
            Logger.Error($"No input was specified!");
            Logger.Error($"Please specify the PlateUp install location!");
            return;
        }
        if (!Directory.Exists(Path.GetFullPath(args[0])))
        {
            Logger.Error($"Input is not a Directory: \"{Path.GetFullPath(args[0])}\"");
            return;
        }
        long start = DateTime.Now.Ticks;
        _ = new Program(Path.GetFullPath(args[0]));
        long end = DateTime.Now.Ticks;
        TimeSpan time = new(end - start);
        Logger.Info($"Process took: {GetTime(time)}");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }


    private static string GetTime(TimeSpan span)
    {
        StringBuilder time_builder = new();
        if (span.Days > 0)
        {
            time_builder.Append($"{span.Days} days ");
        }
        if (span.Hours > 0)
        {
            time_builder.Append($"{span.Hours} hours ");
        }
        if (span.Minutes > 0)
        {
            time_builder.Append($"{span.Minutes} minutes ");
        }
        if (span.Seconds > 0)
        {
            time_builder.Append($"{span.Seconds} seconds ");
        }
        if (span.Milliseconds > 0)
        {
            time_builder.Append($"{span.Milliseconds} milliseconds");
        }
        return time_builder.ToString();
    }

    void GetBaseInfo(string[] files)
    {
        Parallel.ForEach(files, file =>
        {
            using FileStream fs = new(file, FileMode.Open, FileAccess.Read, FileShare.None);
            using StreamReader reader = new(fs);

            string? line = reader.ReadLine();

            string name = "";
            string id_string = "";
            string price_string = "";

            int index = 0;
            while (line != null && !reader.EndOfStream && index < 3)
            {
                line = line.Trim();
                if (line.StartsWith("m_Name:"))
                {
                    name = line.Replace("m_Name:", "").Trim();
                    index++;
                }
                else if (line.StartsWith("ID:"))
                {
                    id_string = line.Replace("ID:", "").Trim();
                    index++;
                }
                else if (line.StartsWith("PurchaseCost:"))
                {
                    price_string = line.Replace("PurchaseCost:", "").Trim();
                    index++;
                }
                line = reader.ReadLine();
            }
            if (index == 3 && int.TryParse(id_string, out int id) && int.TryParse(price_string, out int price))
            {
                if (id > int.MaxValue)
                {
                    Console.WriteLine();
                }
                items.TryAdd(name, new Item
                {
                    ID = id,
                    Name = name,
                    Price = price,
                });
            }
        });
    }

    void GetEnglishInfo(string[] files)
    {
        Parallel.ForEach(files, file =>
        {
            string name = "";
            string? description = null;

            using FileStream fs = new(file, FileMode.Open, FileAccess.Read, FileShare.None);
            using StreamReader reader = new(fs);

            string? line = reader.ReadLine();
            int found = 0;
            while (line != null && !reader.EndOfStream && found < 2)
            {
                line = line.Trim();
                if (line.StartsWith("Name:"))
                {
                    name = line.Replace("Name:", "").Trim();
                    found++;
                }
                if (line.StartsWith("Description:"))
                {
                    description = line.Replace("Description:", "").Trim();
                    found++;
                }
                line = reader.ReadLine();
            }
            if (items.ContainsKey(name))
            {
                items[name].Description = description;
            }
        });
    }

    void Export()
    {
        string file = Path.Combine(Environment.CurrentDirectory, $"plate_up_item_info.json");
        using (FileStream fs = new(file, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using StreamWriter writer = new(fs);
            writer.Write(JsonConvert.SerializeObject(items.Values.OrderBy(i => i.Name), Formatting.Indented));
        }
        Process.Start(new ProcessStartInfo()
        {
            FileName = file,
            UseShellExecute = true,
        });
    }


}

public record Item
{
    public int ID { get; init; }
    public string Name { get; init; }
    public int Price { get; init; }
    public string? Description { get; set; }

}