﻿/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.system
{
    /// <summary>
    /// [system.terminal.destroy] slot that allows you to destroy a previously created terminal on your server.
    /// </summary>
    [Slot(Name = "system.terminal.destroy")]
    public class TerminalDestroy : ISlot
    {
        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            // Retrieving name of terminal to destroy.
            var name = input.GetEx<string>() ??
                throw new ArgumentException("No name supplied to [system.terminal.destroy]");

            // Finding process and doing basic sanity check.
            if (!TerminalCreate._processes.TryRemove(name, out var process))
                return; // Notice, we might come here if terminal was already destroyed using e.g. "exit" command in terminal itself.

            // Closing process.
            process.Process.Close();
            process.Process.Dispose();
            process.Scope.Dispose();
        }
    }
}
