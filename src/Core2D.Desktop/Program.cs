﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Dialogs;
using Avalonia.Headless;
using Avalonia.OpenGL;
using Avalonia.Threading;
using Core2D.Configuration.Themes;
using Core2D.ViewModels.Editor;
using Core2D.Views;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Core2D.Desktop
{
    internal static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool AttachConsole(int processId);

        private static Thread? s_replThread;

        private static void Log(Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            if (ex.InnerException is { })
            {
                Log(ex.InnerException);
            }
        }

        private static void Repl()
        {
            s_replThread = new Thread(ReplThread)
            {
                IsBackground = true
            };
            s_replThread?.Start();
        }

        private static async void ReplThread()
        {
            ScriptState<object>? state = null;

            while (true)
            {
                try
                {
                    var code = Console.ReadLine();

                    if (state is { } previous)
                    {
                        await Util.RunUiJob(async () => { state = await previous.ContinueWithAsync(code); });
                    }
                    else
                    {
                        await Util.RunUiJob(async () =>
                        {
                            var options = ScriptOptions.Default.WithImports("System");
                            state = await CSharpScript.RunAsync(code, options, new ScriptGlobals());
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log(ex);
                }
            }
        }

        private static async Task CreateScreenshots(string extension, double with, double height)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var applicationLifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
                var mainWindow = applicationLifetime?.MainWindow;
                var headlessWindow = mainWindow?.PlatformImpl as IHeadlessWindow;

                if (mainWindow?.FindControl<Panel>("ContentPanel") is Panel contentPanel)
                {
                    var editor = contentPanel?.DataContext as ProjectEditorViewModel;

                    var pt = new Point(-1, -1);
                    headlessWindow?.MouseMove(pt);
                    Dispatcher.UIThread.RunJobs();

                    var size = new Size(with, height);
                    var pathDashboard = $"Core2D-Dashboard-{App.DefaultTheme}.{extension}";
                    var pathEditor = $"Core2D-Editor-{App.DefaultTheme}.{extension}";

                    if (string.Equals(extension, "png", StringComparison.OrdinalIgnoreCase))
                    {
                        Util.RenderAsPng(contentPanel, size, pathDashboard);
                    }
                    if (string.Equals(extension, "svg", StringComparison.OrdinalIgnoreCase))
                    {
                        Util.RenderAsSvg(contentPanel, size, pathDashboard);
                    }
                    Dispatcher.UIThread.RunJobs();

                    editor?.OnNew(null);
                    Dispatcher.UIThread.RunJobs();

                    if (string.Equals(extension, "png", StringComparison.OrdinalIgnoreCase))
                    {
                        Util.RenderAsPng(contentPanel, size, pathEditor);
                    }
                    if (string.Equals(extension, "svg", StringComparison.OrdinalIgnoreCase))
                    {
                        Util.RenderAsSvg(contentPanel, size, pathEditor);
                    }
                    Dispatcher.UIThread.RunJobs();
                }

                applicationLifetime?.Shutdown();
            });
        }

        private static async Task ProcessSettings(Settings settings)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var applicationLifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
                var mainWindow = applicationLifetime?.MainWindow;
                var mainView = mainWindow?.Content as MainView;
                var editor = mainView?.DataContext as ProjectEditorViewModel;

                if (mainView is { })
                {
                    if (settings.Scripts is { })
                    {
                        foreach (var script in settings.Scripts)
                        {
                            editor?.OnExecuteScriptFile(script.FullName);
                            Dispatcher.UIThread.RunJobs();
                        }
                    }

                    if (settings.Project is { })
                    {
                        editor?.OnOpenProject(settings.Project.FullName);
                        Dispatcher.UIThread.RunJobs();
                    }
                }
            });
        }

        private static void StartAvaloniaApp(Settings settings, string[] args)
        {
            var builder = BuildAvaloniaApp();

            try
            {
                if (settings.Theme is { })
                {
                    App.DefaultTheme = settings.Theme.Value;
                }

                if (settings.Repl)
                {
                    Repl();
                }

                if (settings.UseSkia)
                {
                    builder.UseSkia();
                }

                builder.With(new X11PlatformOptions
                {
                    EnableMultiTouch = settings.EnableMultiTouch,
                    UseGpu = settings.UseGpu,
                    UseDeferredRendering = settings.UseDeferredRendering
                });

                builder.With(new Win32PlatformOptions
                {
                    EnableMultitouch = settings.EnableMultiTouch,
                    AllowEglInitialization = settings.AllowEglInitialization,
                    UseWgl = settings.UseWgl,
                    UseDeferredRendering = settings.UseDeferredRendering,
                    UseWindowsUIComposition = settings.UseWindowsUIComposition
                });

                if (settings.UseDirectX11)
                {
                    builder.With(new AngleOptions()
                    {
                        AllowedPlatformApis = new List<AngleOptions.PlatformApi>
                        {
                            AngleOptions.PlatformApi.DirectX11
                        }
                    });
                }

                if (settings.UseManagedSystemDialogs)
                {
                    builder.UseManagedSystemDialogs();
                }

                if (settings.CreateHeadlessScreenshots)
                {
                    builder.UseHeadless(false)
                           .AfterSetup(async _ => await CreateScreenshots(settings.ScreenshotExtension, settings.ScreenshotWidth, settings.ScreenshotHeight))
                           .StartWithClassicDesktopLifetime(args);
                    return;
                }

                if (settings.UseHeadless)
                {
                    builder.UseHeadless(settings.UseHeadlessDrawing);
                }

                if (settings.UseHeadlessVnc)
                {
                    builder.AfterSetup(async _ => await ProcessSettings(settings))
                           .StartWithHeadlessVncPlatform(settings.VncHost, settings.VncPort, args, ShutdownMode.OnMainWindowClose);
                    return;
                }

                builder.AfterSetup(async _ => await ProcessSettings(settings))
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private static RootCommand CreateRootCommand()
        {
            var rootCommand = new RootCommand()
            {
                Description = "A multi-platform data driven 2D diagram editor."
            };

            var optionTheme = new Option(new[] { "--theme", "-t" }, "Set application theme")
            {
                Argument = new Argument<ThemeName?>()
            };
            rootCommand.AddOption(optionTheme);

            var optionScripts = new Option(new[] { "--scripts", "-s" }, "The relative or absolute path to the script files")
            {
                Argument = new Argument<FileInfo[]?>()
            };
            rootCommand.AddOption(optionScripts);

            var optionProject = new Option(new[] { "--project", "-p" }, "The relative or absolute path to the project file")
            {
                Argument = new Argument<FileInfo?>()
            };
            rootCommand.AddOption(optionProject);

            var optionRepl = new Option(new[] { "--repl" }, "Run scripting repl")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionRepl);

            var optionUseManagedSystemDialogs = new Option(new[] { "--useManagedSystemDialogs" }, "Use managed system dialogs")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionUseManagedSystemDialogs);

            var optionUseSkia = new Option(new[] { "--useSkia" }, "Use Skia renderer")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionUseSkia);

            var optionEnableMultiTouch = new Option(new[] { "--enableMultiTouch" }, "Enable multi-touch")
            {
                Argument = new Argument<bool>(getDefaultValue: () => true)
            };
            rootCommand.AddOption(optionEnableMultiTouch);

            var optionUseGpu = new Option(new[] { "--useGpu" }, "Use Gpu")
            {
                Argument = new Argument<bool>(getDefaultValue: () => true)
            };
            rootCommand.AddOption(optionUseGpu);

            var optionAllowEglInitialization = new Option(new[] { "--allowEglInitialization" }, "Allow EGL initialization")
            {
                Argument = new Argument<bool>(getDefaultValue: () => true)
            };
            rootCommand.AddOption(optionAllowEglInitialization);

            var optionUseWgl = new Option(new[] { "--useWgl" }, "Use Windows GL")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionUseWgl);

            var optionUseDeferredRendering = new Option(new[] { "--useDeferredRendering" }, "Use deferred rendering")
            {
                Argument = new Argument<bool>(getDefaultValue: () => true)
            };
            rootCommand.AddOption(optionUseDeferredRendering);

            var optionUseWindowsUIComposition = new Option(new[] { "--useWindowsUIComposition" }, "Use Windows UI composition")
            {
                Argument = new Argument<bool>(getDefaultValue: () => true)
            };
            rootCommand.AddOption(optionUseWindowsUIComposition);
            
            var optionUseDirectX11 = new Option(new[] { "--useDirectX11" }, "Use DirectX11 platform api")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionUseDirectX11);

            var optionUseHeadless = new Option(new[] { "--useHeadless" }, "Use headless")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionUseHeadless);

            var optionUseHeadlessDrawing = new Option(new[] { "--useHeadlessDrawing" }, "Use headless drawing")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionUseHeadlessDrawing);

            var optionUseHeadlessVnc = new Option(new[] { "--useHeadlessVnc" }, "Use headless vnc")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionUseHeadlessVnc);

            var optionCreateHeadlessScreenshots = new Option(new[] { "--createHeadlessScreenshots" }, "Create headless screenshots")
            {
                Argument = new Argument<bool>()
            };
            rootCommand.AddOption(optionCreateHeadlessScreenshots);

            var optionScreenshotExtension = new Option(new[] { "--screenshotExtension" }, "Screenshots file extension")
            {
                Argument = new Argument<string?>(getDefaultValue: () => "png")
            };
            rootCommand.AddOption(optionScreenshotExtension);
            
            var optionScreenshotWidth = new Option(new[] { "--screenshotWidth" }, "Screenshots width")
            {
                Argument = new Argument<double>(getDefaultValue: () => 1366)
            };
            rootCommand.AddOption(optionScreenshotWidth);
            
            var optionScreenshotHeight = new Option(new[] { "--screenshotHeight" }, "Screenshots height")
            {
                Argument = new Argument<double>(getDefaultValue: () => 690)
            };
            rootCommand.AddOption(optionScreenshotHeight);

            var optionVncHost = new Option(new[] { "--vncHost" }, "Vnc host")
            {
                Argument = new Argument<string?>()
            };
            rootCommand.AddOption(optionVncHost);

            var optionVncPort = new Option(new[] { "--vncPort" }, "Vnc port")
            {
                Argument = new Argument<int>(getDefaultValue: () => 5901)
            };
            rootCommand.AddOption(optionVncPort);

            return rootCommand;
        }

        [STAThread]
        internal static void Main(string[] args)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                AttachConsole(-1);
            }

            var rootCommand = CreateRootCommand();
            var rootSettings = default(Settings?);

            rootCommand.Handler = CommandHandler.Create((Settings settings) =>
            {
                rootSettings = settings;
            });

            rootCommand.Invoke(args);

            if (rootSettings is { })
            {
                StartAvaloniaApp(rootSettings, args);
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .LogToTrace();
    }
}
