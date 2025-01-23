// <copyright file="ServerObservableEventHandlerOptions.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Enumerated value describing options for the execution of a handler for an ObservableEvent.
/// </summary>
[Flags]
public enum ServerObservableEventHandlerOptions
{
    /// <summary>
    /// No options, meaning handlers attempt to run synchronously, awaiting the completion of execution. This is the default.
    /// </summary>
    None = 0,

    /// <summary>
    /// The handler will execute asynchronously. Order of multiple executions of the handler is not guaranteed.
    /// </summary>
    RunHandlerAsynchronously = 1,
}