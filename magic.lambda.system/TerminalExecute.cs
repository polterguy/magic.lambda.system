/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.system
{
    /// <summary>
    /// [system.execute] slot that allows you to execute a system process,
    /// passing in arguments, and returning the result of the execution.
    /// </summary>
    [Slot(Name = "system.execute")]
    public class TerminalExecute : ISlotAsync, ISlot
    {
        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            Execute(signaler, input).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        /// <returns>Awaitable task</returns>
        public async Task SignalAsync(ISignaler signaler, Node input)
        {
            await Execute(signaler, input);
        }

        #region [ -- Private helper methods -- ]

        async Task Execute(ISignaler signaler, Node input)
        {
            /*
             * Executing node to make sure we're able to correctly retrieve
             * arguments passed into execution of process.
             */
            await signaler.SignalAsync("eval", input);

            // Retrieving arguments to invocation.
            var args = input.Children.FirstOrDefault()?.GetEx<string>();

            // Creating and decorating process.
            ProcessStartInfo startInfo = null;
            if (string.IsNullOrEmpty(args))
                startInfo = new ProcessStartInfo(input.GetEx<string>())
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
            else
                startInfo = new ProcessStartInfo(input.GetEx<string>(), args)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

            // Creating and starting process, making sure we clean up after ourselves.
            using (var process = Process.Start(startInfo))
            {
                // Making sure we wait for process to finish.
                var result = new StringBuilder();
                while (!process.StandardOutput.EndOfStream)
                {
                    if (result.Length != 0)
                        result.Append("\r\n");
                    result.Append(await process.StandardOutput.ReadLineAsync());
                }

                // Returning result of process execution to caller.
                input.Value = result.ToString();
            }
        }


        #endregion
    }
}
