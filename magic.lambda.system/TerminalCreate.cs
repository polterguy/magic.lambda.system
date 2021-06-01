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
        internal static readonly ConcurrentDictionary<string, (Process Process, IServiceScope Scope)> _processes = new ConcurrentDictionary<string, (Process, IServiceScope)>();
        readonly IServiceProvider _services;

        /// <summary>
        /// Constructor needed to retrieve service provider to create ISignaler during callbacks.
        /// </summary>
        /// <param name="services"></param>
        public TerminalCreate(IServiceProvider services)
        {
            _services = services;
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            // Creating and decorating our start info.
            var si = GetStartInfo(signaler, input);

            // Starting process.
            var process = Process.Start(si.StartInfo);

            // Explicitly stating we're interested in events for process, to capture STDOUT and STDERR.
            process.EnableRaisingEvents = true;

            // Creating scope making sure it's disposed if an exception occurs further down.
            var scope = _services.CreateScope();
            try
            {
                // Capturing STDOUT
                if (si.StdOut != null)
                    process.OutputDataReceived += (sender, args) => ExecuteCallback(scope, si.StdOut, args.Data);

                // Capturing exit.
                process.Exited += (sender, args) =>
                {
                    ExecuteCallback(scope, si.StdOut, null, true);
                    _processes.TryRemove(si.Name, out var _);
                };

                if (si.StdErr != null)
                    process.ErrorDataReceived += (sender, args) => ExecuteCallback(scope, si.StdErr, args.Data);

                // Adding process to dictionary such that we can later reference it.
                if (!_processes.TryAdd(si.Name, (process, scope)))
                {
                    process.Close();
                    throw new ArgumentException($"Process with name of '{si.Name}' already exists");
                }
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch
            {
                process.Dispose();
                scope.Dispose();
                throw;
            }
        }

        #region [ -- Private helper methods -- ]

        /*
         * Common callback for invoking callback given during initialisation
         * when output of some sort is available.
         */
        void ExecuteCallback(
            IServiceScope scope,
            Node lambda,
            string cmd,
            bool sendNull = false)
        {
            // Checking if we should send message at all, which we only do if message is not null, or caller explicitly wants null messages.
            if (sendNull || !string.IsNullOrEmpty(cmd))
            {
                // Creating and decorating our invocation/execution lambda.
                var exe = lambda.Clone();
                var argsToExe = new Node(".arguments");
                argsToExe.Add(new Node("cmd", cmd));
                exe.Insert(0, argsToExe);

                // Creating a signaler using our current scope for terminal.
                var sign = scope.ServiceProvider.GetService(typeof(ISignaler)) as ISignaler;

                // Making sure we dispose signaler once done with it.
                using (var disposer = sign as IDisposable)
                {
                    sign.SignalAsync("eval", exe).GetAwaiter().GetResult();
                }
            }
        }

        /*
         * Private method to help extract arguments and create our ProcessStartInfo object.
         */
        (ProcessStartInfo StartInfo, string Name, Node StdOut, Node StdErr) GetStartInfo(ISignaler signaler, Node input)
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

            // Retrieving working folder.
            var workingFolder = input.Children.FirstOrDefault(x => x.Name == "folder")?.GetEx<string>() ?? "/";
            var rootFolderNode = new Node();
            signaler.Signal(".io.folder.root", rootFolderNode);
            workingFolder = rootFolderNode.Get<string>() + workingFolder.TrimStart('/');

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
            startInfo.WorkingDirectory = workingFolder;

            // Returning start info to caller.
            return (startInfo, name, stdOut, stdErr);
        }

        #endregion
    }
}
