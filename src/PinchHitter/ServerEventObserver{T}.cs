// <copyright file="ServerEventObserver{T}.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Implementation of an observer in the Observer pattern for events.
/// </summary>
/// <typeparam name="T">The type of event arguments containing information about the observable event.</typeparam>
public class ServerEventObserver<T>
    where T : EventArgs
{
    private readonly ServerObservableEvent<T> observableEvent;
    private readonly string observerId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerEventObserver{T}"/> class.
    /// </summary>
    /// <param name="observableEvent">The observable event being observed.</param>
    /// <param name="observerId">The ID of the handler for the observable event.</param>
    internal ServerEventObserver(ServerObservableEvent<T> observableEvent, string observerId)
    {
        this.observableEvent = observableEvent;
        this.observerId = observerId;
    }

    /// <summary>
    /// Stops observing the event.
    /// </summary>
    public void Unobserve()
    {
        this.observableEvent.RemoveObserver(this.observerId);
    }
}
