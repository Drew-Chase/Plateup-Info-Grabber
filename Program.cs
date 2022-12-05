using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Plateup_GetItems;

//record Item(int ID, string Name, int Price, string? Description = null);

internal class Program
{
    private readonly string _path, _asset_dir, _english_dir;
    private ConcurrentDictionary<string, Item> items;
    Program(string path)
    {
        _path = Path.GetFullPath(path);
        _asset_dir = Path.Combine(_path, "assets");
        _english_dir = Path.Combine(_path, "english");
        items = new();

        if (!Directory.Exists(_asset_dir))
        {
            Console.Error.WriteLine($"Asset directory doesn't exist");
            return;
        }
        if (!Directory.Exists(_english_dir))
        {
            Console.Error.WriteLine($"English directory doesn't exist");
            return;
        }

        GetBaseInfo();
        GetEnglishInfo();
        Export();
    }
    static void Main(string[] args)
    {
        long start = DateTime.Now.Ticks;
        _ = new Program(args.Any() ? args[0] : Environment.CurrentDirectory);
        long end = DateTime.Now.Ticks;
        TimeSpan time = new(end - start);
        Console.WriteLine($"Process took: {GetTime(time)}");
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


    void GetBaseInfo()
    {
        string[] files = Directory.GetFiles(_asset_dir, "*.asset", SearchOption.TopDirectoryOnly);
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

    void GetEnglishInfo()
    {
        string[] files = Directory.GetFiles(_english_dir, "*.asset", SearchOption.TopDirectoryOnly);
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
        string file = Path.Combine(Path.GetFullPath("./"), $"plate_up_item_info.json");
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