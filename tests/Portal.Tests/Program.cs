using System.Reflection;
using Portal.Tests;
using Xunit;

// Alternativa al testhost para entornos que impiden la comunicación TCP local de VSTest.
var passed = 0; var failed = 0;
foreach (var method in typeof(StoreTests).GetMethods().Where(m => m.GetCustomAttribute<FactAttribute>() is not null))
{
    var cases = method.GetCustomAttributes<InlineDataAttribute>().Select(a => a.GetData(method).Single()).ToArray();
    if (cases.Length == 0) cases = [Array.Empty<object>()];
    foreach (var arguments in cases)
    {
        using var instance = new StoreTests();
        try
        {
            if (method.Invoke(instance, arguments) is Task task) await task;
            Console.WriteLine($"PASS {method.Name} {string.Join(',', arguments)}"); passed++;
        }
        catch (Exception ex) { Console.Error.WriteLine($"FAIL {method.Name}: {ex.InnerException ?? ex}"); failed++; }
    }
}
Console.WriteLine($"Passed: {passed}; Failed: {failed}");
return failed == 0 ? 0 : 1;
