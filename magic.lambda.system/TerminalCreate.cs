/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.system
{
    /// <summary>
    /// [system.terminal.create] slot that allows you to create a terminal on your server.
    /// </summary>
    [Slot(Name = "system.terminal.create")]
    public class TerminalCreate : ISlot
    {
        internal static readonly ConcurrentDictionary<string, Process> _processes = new ConcurrentDictionary<string, Process>();
        readonly IServiceScope _services;

        /// <summary>
        /// Constructor needed to retrieve service provider to create ISignaler during callbacks.
        /// </summary>
        /// <param name="services"></param>
        public TerminalCreate(IServiceProvider services)
        {
            _services = services.CreateScope();
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            // Retrieving name for terminal, which is needed later to reference it.
            var name = input.GetEx<string>() ?? 
                throw new ArgumentException("No name supplied to [system.terminal.create]");

            // Retrieving name of process that we can use later to reference it.
            if (_processes.ContainsKey(name))
                throw new ArgumentException($"Terminal with name of '{name}' already exists");

            // Checking if we have STDOUT/STDERR callbacks.
            var stdOut = input.Children.FirstOrDefault(x => x.Name == ".stdOut")?.Clone();
            var stdErr = input.Children.FirstOrDefault(x => x.Name == ".stdErr")?.Clone();

            // Configuring our process.
            var startInfo = new ProcessStartInfo();
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                // xNix operating system type.
                startInfo.FileName = "/bin/bash";
            }
            else
            {
                // Windows something.
                startInfo.FileName = "cmd.exe";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;

            // Starting process.
            var process = Process.Start(startInfo);

            // Checking if we have a [.stdOut] callback, and if so making sure we capture output.
            if (stdOut != null)
            {
                process.OutputDataReceived += (sender, args) => 
                {
                    var exe = stdOut.Clone();
                    var argsToExe = new Node(".arguments");
                    argsToExe.Add(new Node("cmd", args.Data));
                    exe.Insert(0, argsToExe);
                    var sign = _services.ServiceProvider.GetService(typeof(ISignaler)) as ISignaler;
                    sign.SignalAsync("eval", exe).GetAwaiter().GetResult();
                };
            }

            // Checking if we have a [.err] callback, and if so making sure we capture output.
            if (stdErr != null)
            {
                process.ErrorDataReceived += (sender, args) => 
                {
                    var exe = stdErr.Clone();
                    var argsToExe = new Node(".arguments");
                    argsToExe.Add(new Node("cmd", args.Data));
                    exe.Insert(0, argsToExe);
                    var sign = _services.ServiceProvider.GetService(typeof(ISignaler)) as ISignaler;
                    sign.SignalAsync("eval", exe).GetAwaiter().GetResult();
                };
            }

            // Adding process to dictionary such that we can later reference it.
            if (!_processes.TryAdd(name, process))
            {
                process.Close();
                throw new ArgumentException($"Process with name of '{name}' already exists");
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
    }
}
