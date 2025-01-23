// <copyright file="ServerObservableEvent{T}.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Implementation of an subject in the Observer pattern for events. It can optionally be limited
/// to a specific number of observers.
/// </summary>
/// <typeparam name="T">The type of event arguments containing information about the observable event.</typeparam>
public class ServerObservableEvent<T>
    where T : EventArgs
{
    private readonly Dictionary<string, ServerObservableEventHandler<T>> observers = new();
    private readonly int maxObserverCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerObservableEvent{T}"/> class.
    /// </summary>
    public ServerObservableEvent()
        : this(0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerObservableEvent{T}"/> class.
    /// </summary>
    /// <param name="maxObserverCount">The maximum number of handlers that may observe this event.</param>
    public ServerObservableEvent(int maxObserverCount)
    {
        this.maxObserverCount = maxObserverCount;
    }

    /// <summary>
    /// Gets the maximum number of observers that may observe this event.
    /// A value of zero (0) indicates an unlimited number of observers.
    /// </summary>
    public int MaxObserverCount => this.maxObserverCount;

    /// <summary>
    /// Gets the current number of observers that are observing this event.
    /// </summary>
    public int CurrentObserverCount => this.observers.Count;

    /// <summary>
    /// Adds a function to observe the event that takes an argument of type T and returns void.
    /// It will be wrapped in a Task so that it can be awaited.
    /// </summary>
    /// <param name="handler">An action that handles the observed event.</param>
    /// <param name="handlerOptions">
    /// The options for executing the handler. Defaults to ObservableEventHandlerOptions.None,
    /// meaning the handler will attempt to execute synchronously, awaiting the result of execution.
    /// </param>
    /// <param name="description">An optional description for this observer.</param>
    /// <returns>An observer for this observable event.</returns>
    /// <exception cref="PinchHitterException">
    /// Thrown when the user attempts to add more observers than this event allows.
    /// </exception>
    public ServerEventObserver<T> AddObserver(Action<T> handler, ServerObservableEventHandlerOptions handlerOptions = ServerObservableEventHandlerOptions.None, string description = "")
    {
        Func<T, Task> wrappedHandler = (T args) =>
        {
            // Note that if an exception is thrown during the execution of
            // the handler, it will bubble up when NotifyObserversAsync is
            // called, and the Task will be set to Faulted, so no need to
            // add code for that here.
            TaskCompletionSource<bool> taskCompletionSource = new();
            handler(args);
            taskCompletionSource.SetResult(true);
            return taskCompletionSource.Task;
        };

        return this.AddObserver(wrappedHandler, handlerOptions, description);
    }

    /// <summary>
    /// Adds a function to observe the event that takes an argument of type T and returns a Task.
    /// </summary>
    /// <param name="handler">A function returning a Task that handles the observed event.</param>
    /// <param name="handlerOptions">
    /// The options for executing the handler. Defaults to ObservableEventHandlerOptions.None,
    /// meaning the handler will attempt to execute synchronously, awaiting the result of execution.
    /// </param>
    /// <param name="description">An optional description for this observer.</param>
    /// <returns>An observer for this observable event.</returns>
    /// <exception cref="PinchHitterException">
    /// Thrown when the user attempts to add more observers than this event allows.
    /// </exception>
    public ServerEventObserver<T> AddObserver(Func<T, Task> handler, ServerObservableEventHandlerOptions handlerOptions = ServerObservableEventHandlerOptions.None, string description = "")
    {
        if (this.maxObserverCount > 0 && this.observers.Count == this.maxObserverCount)
        {
            throw new PinchHitterException($"""This observable event only allows {this.maxObserverCount} {(this.maxObserverCount == 1 ? "handler" : "handlers")}.""");
        }

        string observerId = Guid.NewGuid().ToString();
        if (string.IsNullOrEmpty(description))
        {
            description = $"ServerEventObserver<{typeof(T).Name}> (id: {observerId})";
        }

        this.observers.Add(observerId, new ServerObservableEventHandler<T>(handler, handlerOptions, description));
        return new ServerEventObserver<T>(this, observerId);
    }

    /// <summary>
    /// Removes a handler for this observable event.
    /// </summary>
    /// <param name="observerId">The ID of the handler handling the event.</param>
    public void RemoveObserver(string observerId)
    {
        this.observers.Remove(observerId);
    }

    /// <summary>
    /// Asynchronously notifies observers when this observable event occurs.
    /// </summary>
    /// <param name="notifyData">The data of the event.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task NotifyObserversAsync(T notifyData)
    {
        foreach (ServerObservableEventHandler<T> observer in this.observers.Values)
        {
            if ((observer.Options & ServerObservableEventHandlerOptions.RunHandlerAsynchronously) == ServerObservableEventHandlerOptions.RunHandlerAsynchronously)
            {
                _ = Task.Run(() => observer.HandleObservedEvent(notifyData)).ConfigureAwait(false);
            }
            else
            {
                await observer.HandleObservedEvent(notifyData).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"ServerObservableEvent<{typeof(T).Name}> with observers:\n    {string.Join("\n    ", this.observers.Values)}";
    }

    private class ServerObservableEventHandler<TEventArgs>
    {
        private readonly Func<TEventArgs, Task> handler;
        private readonly ServerObservableEventHandlerOptions handlerOptions;
        private readonly string description;

        public ServerObservableEventHandler(Func<TEventArgs, Task> handler, ServerObservableEventHandlerOptions handlerOptions, string description)
        {
            this.handler = handler;
            this.handlerOptions = handlerOptions;
            this.description = description;
        }

        public Func<TEventArgs, Task> HandleObservedEvent => this.handler;

        public ServerObservableEventHandlerOptions Options => this.handlerOptions;

        public override string ToString()
        {
            return this.description;
        }
    }
}
