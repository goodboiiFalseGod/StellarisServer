using System;
using System.Linq;
using Stellaris.Core.Scripting;

public static class ParserChecks
{
    public static void Run()
    {
        var root = ParadoxParser.Parse("\uFEFF# заголовок\r\n@amount = 4\r\njob = { produces = { energy = @amount } }\njob={ upkeep={energy=1} }", "example.txt");
        Check(root.Entries.Count == 3, "Число корневых элементов.");
        Check(root.Required("@amount").AsScalar() == "4", "Переменная.");
        Check(root.Required("@amount").Line == 2 && root.Required("@amount").Column == 1, "Координаты CRLF.");
        Check(root.Named("job").Count() == 2, "Повторяющиеся ключи.");
        Check(root.Named("job").First().AsBlock().Required("produces").AsBlock().Required("energy").AsScalar() == "@amount", "Вложенность.");
        Check(root.Optional("missing") is null, "Необязательное поле.");
        Throws(() => root.Required("job"), "example.txt:4:1:");
        Throws(() => root.Required("missing"), "missing");
        Throws(() => root.Required("@amount").AsBlock(), "example.txt:2:1:");
        Throws(() => root.Named("job").First().AsScalar(), "example.txt:3:1:");

        var strings = ParadoxParser.Parse("name=\"a \\\"b\\\" \\\\ c\\n\\t # {} =\" path=\"C:\\unknown\" empty=\"\"");
        Check(strings.Required("name").AsScalar() == "a \"b\" \\ c\n\t # {} =", "Экранирование и специальные символы строки.");
        Check(strings.Required("path").AsScalar() == "C:\\unknown", "Неизвестное экранирование сохраняется.");
        Check(strings.Required("empty").AsScalar() == "", "Пустая строка.");

        var list = ParadoxParser.Parse("items={ first second \"third item\" scope:planet|value } a!=1 b<2 c>3 d<=4 e>=5 f=6");
        var items = list.Required("items").AsBlock();
        Check(items.Entries.Count == 4, "Список.");
        Check(items.Entries[3].Key == "scope:planet|value" && items.Entries[3].Operator == "" && items.Entries[3].Scalar == "scope:planet|value", "Двоеточие и pipe остаются частью скаляра.");
        Check(string.Join(",", list.Entries.Skip(1).Select(entry => entry.Operator)) == "!=,<,>,<=,>=,=", "Операторы сравнения.");
        Check(ParadoxParser.Parse("# только комментарий").Entries.Count == 0, "Пустой документ.");
        var math = ParadoxParser.Parse("amount = @[ 2 * @base + [3 - 1] ] next = yes", "math.txt");
        Check(math.Required("amount").AsScalar() == "@[ 2 * @base + [3 - 1] ]", "Inline math остаётся единым скаляром.");
        Check(math.Required("amount").Source == "math.txt" && math.Required("next").AsScalar() == "yes", "Метаданные и продолжение после inline math.");

        Throws(() => ParadoxParser.Parse("}", "bad"), "bad:1:1:");
        Throws(() => ParadoxParser.Parse("{", "bad"), "bad:1:1:");
        Throws(() => ParadoxParser.Parse("a={b=1", "bad"), "bad:1:3:");
        Throws(() => ParadoxParser.Parse("a=\"unfinished", "bad"), "bad:1:3:");
        Throws(() => ParadoxParser.Parse("a=", "bad"), "bad:1:3:");
        Throws(() => ParadoxParser.Parse("a!1", "bad"), "bad:1:2:");
        Throws(() => ParadoxParser.Parse("a==1", "bad"), "bad:1:3:");
        Throws(() => ParadoxParser.Parse("a={b=}", "bad"), "bad:1:6:");
        Throws(() => ParadoxParser.Parse("a=@[1 + 2", "bad"), "bad:1:3:");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Проверка parser: {message}");
        }
    }

    private static void Throws(Action action, string expected)
    {
        try
        {
            action();
        }
        catch (FormatException exception)
        {
            Check(exception.Message.Contains(expected, StringComparison.Ordinal), $"Ожидалось '{expected}', получено '{exception.Message}'.");
            return;
        }

        throw new InvalidOperationException($"Ожидалось исключение parser с '{expected}'.");
    }
}
