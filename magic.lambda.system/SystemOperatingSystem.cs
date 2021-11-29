﻿/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using magic.node;
using magic.signals.contracts;

namespace magic.lambda.system
{
    /// <summary>
    /// [system.execute] slot that allows you to execute a system process,
    /// passing in arguments, and returning the result of the execution.
    /// </summary>
    [Slot(Name = "system.os")]
    public class SystemOperatingSystem : ISlot
    {
        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            input.Value = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        }
    }
}
