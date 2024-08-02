using AutoGen.DotnetInteractive;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.CSharp;
using Microsoft.DotNet.Interactive.Events;
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

// print all subkernels
foreach (var subKernel in kernel.SubkernelsAndSelf())
{
    Console.WriteLine(subKernel.Name);
}

// connet jupyter kernel
var venv = ".venv";
var magicCommand = $"#!connect jupyter --kernel-name python --kernel-spec {venv}";
var connectCommand = new SubmitCode(magicCommand);
var connectResult = await kernel.SendAsync(connectCommand);
PrintDisplayValue(connectResult);



// run python code
var pythonCode = @"
print('Hello from Python')
";

var pythonCodeCommand = new SubmitCode(pythonCode, "python");
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

// run html code
code = @"
    <h1>Hello from HTML</h1>
    ";

var htmlCodeCommand = new SubmitCode(code, "html");
result = await kernel.SendAsync(htmlCodeCommand);
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