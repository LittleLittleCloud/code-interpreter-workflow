// Copyright (c) Microsoft Corporation. All rights reserved.
// InteractiveService.cs

using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Connection;
using Microsoft.DotNet.Interactive.CSharp;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.FSharp;
using Microsoft.DotNet.Interactive.Jupyter;
using Microsoft.DotNet.Interactive.PackageManagement;
using Microsoft.DotNet.Interactive.PowerShell;
using Microsoft.DotNet.Interactive.Utility;

namespace dotnet_interactive_agent;
public class InteractiveService : IDisposable
{
    private Kernel? kernel = null;
    private Process? process = null;
    private bool disposedValue;
    private const string DotnetInteractiveToolNotInstallMessage = "Cannot find a tool in the manifest file that has a command named 'dotnet-interactive'.";
    //private readonly ProcessJobTracker jobTracker = new ProcessJobTracker();
    private string? installingDirectory;

    public event EventHandler<DisplayEvent>? DisplayEvent;

    public event EventHandler<string>? Output;

    public event EventHandler<CommandFailed>? CommandFailed;

    public event EventHandler<HoverTextProduced>? HoverTextProduced;

    /// <summary>
    /// Install dotnet interactive tool to <paramref name="installingDirectory"/>
    /// and create an instance of <see cref="InteractiveService"/>.
    /// 
    /// When using this constructor, you need to call <see cref="StartAsync(string, CancellationToken)"/> to install dotnet interactive tool
    /// and start the kernel.
    /// </summary>
    /// <param name="installingDirectory">dotnet interactive installing directory</param>
    public InteractiveService(string installingDirectory)
    {
        this.installingDirectory = installingDirectory;
    }

    /// <summary>
    /// Create an instance of <see cref="InteractiveService"/> with a running kernel.
    /// When using this constructor, you don't need to call <see cref="StartAsync(string, CancellationToken)"/> to start the kernel.
    /// </summary>
    /// <param name="kernel"></param>
    public InteractiveService(Kernel kernel)
    {
        this.kernel = kernel;
    }

    public async Task<bool> StartAsync(string workingDirectory, CancellationToken ct = default)
    {
        if (this.kernel != null)
        {
            return true;
        }

        this.kernel = await this.CreateKernelAsync(workingDirectory, true, ct);
        return true;
    }

    public bool RestoreDotnetInteractive()
    {
        if (this.installingDirectory is null)
        {
            throw new Exception("Installing directory is not set");
        }

        // write RestoreInteractive.config from embedded resource to this.workingDirectory
        var assembly = Assembly.GetAssembly(typeof(InteractiveService))!;
        var resourceName = "AutoGen.DotnetInteractive.RestoreInteractive.config";
        using (var stream = assembly.GetManifestResourceStream(resourceName)!)
        using (var fileStream = File.Create(Path.Combine(this.installingDirectory, "RestoreInteractive.config")))
        {
            stream.CopyTo(fileStream);
        }

        // write dotnet-tool.json from embedded resource to this.workingDirectory

        resourceName = "AutoGen.DotnetInteractive.dotnet-tools.json";
        using (var stream2 = assembly.GetManifestResourceStream(resourceName)!)
        using (var fileStream2 = File.Create(Path.Combine(this.installingDirectory, "dotnet-tools.json")))
        {
            stream2.CopyTo(fileStream2);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"tool restore --configfile RestoreInteractive.config",
            WorkingDirectory = this.installingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();

        return process.ExitCode == 0;
    }

    private async Task<Kernel> CreateKernelAsync(string workingDirectory, bool restoreWhenFail = true, CancellationToken ct = default)
    {
        try
        {
            var compositeKernel = new CompositeKernel("composite");

            // create csharp kernel
            var csharpKernel = new CSharpKernel()
                .UseNugetDirective((k, resolvedPackageReference) =>
                {

                    k.AddAssemblyReferences(resolvedPackageReference
                        .SelectMany(r => r.AssemblyPaths));
                    return Task.CompletedTask;
                })
                .UseKernelHelpers()
                .UseWho()
                .UseMathAndLaTeX()
                .UseValueSharing();

            compositeKernel.Add(csharpKernel, ["c#", "C#"]);

            // create fsharp kernel
            var fsharpKernel = new FSharpKernel()
                .UseDefaultFormatting()
                .UseKernelHelpers()
                .UseWho()
                .UseMathAndLaTeX()
                .UseValueSharing();

            compositeKernel.Add(fsharpKernel, ["f#", "F#"]);

            // create powershell kernel
            var powershellKernel = new PowerShellKernel()
                .UseProfiles()
                .UseValueSharing();
            compositeKernel.Add(powershellKernel, ["pwsh", "powershell"]);

            var kernel = compositeKernel
                .UseDefaultMagicCommands()
                .UseImportMagicCommand();

            // add jupyter connector
            kernel.AddKernelConnector(
                new ConnectJupyterKernelCommand()
                .AddConnectionOptions(new JupyterLocalKernelConnectionOptions()));

            // add python kernel
            var venv = "myenv";
            var magicCommand = $"#!connect jupyter --kernel-name pythonkernel --kernel-spec {venv}";
            var connectCommand = new SubmitCode(magicCommand);
            var connectResult = await kernel.SendAsync(connectCommand);

            return kernel;
        }
        catch (CommandLineInvocationException) when (restoreWhenFail)
        {
            var success = this.RestoreDotnetInteractive();

            if (success)
            {
                return await this.CreateKernelAsync(workingDirectory, false, ct);
            }

            throw;
        }
    }

    public bool IsRunning()
    {
        return this.kernel != null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                this.kernel?.Dispose();

                if (this.process != null)
                {
                    this.process.Kill();
                    this.process.Dispose();
                }
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
