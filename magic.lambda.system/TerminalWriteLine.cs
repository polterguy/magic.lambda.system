/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.system
{
    /// <summary>
    /// [system.terminal.write-line] slot that allows you to write a line to a previously opened terminal bash process.
    /// </summary>
    [Slot(Name = "system.terminal.write-line")]
    public class TerminalWriteLine : ISlotAsync
    {
        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public async Task SignalAsync(ISignaler signaler, Node input)
        {
            // Retrieving name of terminal to write to.
            var name = input.GetEx<string>() ??
                throw new ArgumentException("No terminal name supplied to [system.terminal.write-line]");

            // Retrieving command to transmit to terminal.
            var cmd = input.Children.FirstOrDefault(x => x.Name == "cmd")?.GetEx<string>()?.Trim() ?? 
                throw new ArgumentException("No [cmd] passed into terminal");

            // Finding process.
            Process process;
            if (!TerminalCreate._processes.TryGetValue(name, out process))
                throw new ArgumentException($"Terminal with name of '{name}' was not found");

            // Sending code to terminal process.
            await process.StandardInput.WriteLineAsync(cmd);
        }
    }
}
