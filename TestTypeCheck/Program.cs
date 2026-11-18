using System.Reflection;

var asm = Assembly.LoadFrom(@"C:\Program Files\dotnet\packs\Microsoft.AspNetCore.App.Ref\10.0.12\ref\net10.0\Microsoft.AspNetCore.Http.dll");
Console.WriteLine("Microsoft.AspNetCore.Http types with Endpoint/Convention:");
foreach (var t in asm.GetTypes())
{
    if (t.Name.Contains("Endpoint") || t.Name.Contains("Convention"))
    {
        Console.WriteLine($"  {t.FullName}");
    }
}

var asm2 = Assembly.LoadFrom(@"C:\Program Files\dotnet\packs\Microsoft.AspNetCore.App.Ref\10.0.12\ref\net10.0\Microsoft.AspNetCore.Http.Abstractions.dll");
Console.WriteLine("\nMicrosoft.AspNetCore.Http.Abstractions types with Endpoint/Convention:");
foreach (var t in asm2.GetTypes())
{
    if (t.Name.Contains("Endpoint") || t.Name.Contains("Convention"))
    {
        Console.WriteLine($"  {t.FullName}");
    }
}

var asm3 = Assembly.LoadFrom(@"C:\Program Files\dotnet\packs\Microsoft.AspNetCore.App.Ref\10.0.12\ref\net10.0\Microsoft.AspNetCore.Routing.Abstractions.dll");
Console.WriteLine("\nMicrosoft.AspNetCore.Routing.Abstractions types with Endpoint/Convention:");
foreach (var t in asm3.GetTypes())
{
    if (t.Name.Contains("Endpoint") || t.Name.Contains("Convention"))
    {
        Console.WriteLine($"  {t.FullName}");
    }
}