using dotnet_interactive_agent;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.CSharp;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.DotNet.Interactive.Jupyter;
using System.Reflection;

var cwd = System.IO.Directory.GetCurrentDirectory();
using var interactiveService = new InteractiveService(cwd);

await interactiveService.StartAsync(cwd);

// get kernel using reflection
// interactiveService.kernel is private
var kernel = interactiveService.GetType().GetField("kernel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(interactiveService) as Kernel;
if (kernel is null)
{
    Console.WriteLine("Failed to get kernel");
    return;
}

//run python code
var pythonCode = @"
print('Hello from Python')
";

var pythonCodeCommand = new SubmitCode(pythonCode, "pythonkernel");
var pythonResult = await kernel.SendAsync(pythonCodeCommand);
PrintDisplayValue(pythonResult);

// run csharp code
var code = """
    Console.WriteLine("Hello from C#");
    """;

var csharpCodeCommand = new SubmitCode(code, "csharp");
var result = await kernel.SendAsync(csharpCodeCommand);
PrintDisplayValue(result);

// run fsharp code
code = """
    printfn "Hello from F#"
    """;

var fsharpCodeCommand = new SubmitCode(code, "fsharp");
result = await kernel.SendAsync(fsharpCodeCommand);
PrintDisplayValue(result);

// run pwsh code
code = @"
    Write-Host 'Hello from PowerShell'
    ";

var pwshCodeCommand = new SubmitCode(code, "pwsh");
result = await kernel.SendAsync(pwshCodeCommand);
PrintDisplayValue(result);



void PrintDisplayValue(KernelCommandResult result)
{
    var displayValues = result.Events.Where(x => x is StandardErrorValueProduced || x is StandardOutputValueProduced || x is ReturnValueProduced || x is DisplayedValueProduced)
                    .SelectMany(x => (x as DisplayEvent)!.FormattedValues);

    if (displayValues is null || displayValues.Count() == 0)
    {
        return;
    }

    Console.WriteLine(string.Join("\n", displayValues.Select(x => x.Value)));
}