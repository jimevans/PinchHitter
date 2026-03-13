// <copyright file="ServerObservableEventSource{T}.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Provides the raise side of a <see cref="ServerObservableEvent{T}"/> for use within the
/// PinchHitter library. Consumers receive references typed as <see cref="ServerObservableEvent{T}"/>,
/// which exposes only the subscribe and unsubscribe operations.
/// </summary>
/// <typeparam name="T">The type of event arguments containing information about the observable event.</typeparam>
internal sealed class ServerObservableEventSource<T> : ServerObservableEvent<T>
    where T : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerObservableEventSource{T}"/> class.
    /// </summary>
    public ServerObservableEventSource()
        : base()
    {
    }

    /// <summary>
    /// Asynchronously notifies observers when this observable event occurs.
    /// </summary>
    /// <param name="notifyData">The data of the event.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    internal new Task NotifyObserversAsync(T notifyData) => base.NotifyObserversAsync(notifyData);
}
