using System.Diagnostics;
using Stellaris.Checks;

var timer = Stopwatch.StartNew();
var failed = 0;
if (args.Length != 0 && (args.Length != 2 || args[0] != "--game-root" || string.IsNullOrWhiteSpace(args[1])))
{
    Console.Error.WriteLine("Неизвестные или неполные аргументы. Использование: Stellaris.Checks [--game-root PATH].");
    return 2;
}
var suites = new List<(string Name, Action Run)>
{
    ("Paradox Script", ParserChecks.Run),
    ("Экономическое ядро", EconomyChecks.Run),
    ("Бонусная Workforce и содержание", BonusWorkforceChecks.Run),
    ("Импорт контента", ContentChecks.Run),
    ("Импорт Workforce traits", TraitWorkforceChecks.Run),
};
if (args.Length == 2)
{
    suites.Add(("Реальный контент v4.5.2", () => RealContentChecks.Run(args[1])));
    suites.Add(("Реальные Workforce traits v4.5.2", () => TraitWorkforceChecks.RunReal(args[1])));
}

foreach (var suite in suites)
{
    try
    {
        suite.Run();
        Console.WriteLine($"PASS: {suite.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL: {suite.Name}\n{exception}");
    }
}

Console.WriteLine($"Наборов: {suites.Count}; ошибок: {failed}; время: {timer.Elapsed.TotalMilliseconds:F2} мс.");
return failed == 0 ? 0 : 1;
