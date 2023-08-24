# PinchHitter
A basic in-memory web server designed for testing .NET applications

![Unit tests](https://github.com/jimevans/PinchHitter/actions/workflows/dotnet.yml/badge.svg)
[![Coverage Status](https://coveralls.io/repos/github/jimevans/PinchHitter/badge.svg?branch=main&kill_cache=1)](https://coveralls.io/github/jimevans/PinchHitter?branch=main)

This repository contains a class library implementing an in-memory simple web server
capable of serving HTTP content as well as acting as a server for WebSocket traffic.
The project uses a`System.Net.Sockets.TcpListener` to avoid the necessity of registering
URL prefixes on Windows, which using `System.Net.Sockets.HttpListener` requires, and which
needs administrative access. It is provided as an alternative to 
[Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/?view=aspnetcore-6.0),
as Kestral is tuned for use with ASP.NET Core, and including it into a test project
brings in many additional dependencies, even when the project being tested is not an
ASP.NET project.

This project is not intended to be a fully-featured, production-ready web server. It does
not support many of the features of a modern web server.

## Getting Started
To use the server, you can instantiate it and call the `Start()` method. You can have the
server instance return HTML traffic to a browser.

```csharp
using PinchHitter;

// Start a new server to listen on a random port.
Server server = new();
server.Start();

// Register some content to be returned when a URL is browsed.
server.RegisterResource("/index.html", WebResource.CreateHtmlResource(
    "<h1>Welcome to the PinchHitter web server</h1><p>You can browse using localhost</p>"));

// Browse to the registered URL, and retrieve the content. You can also
// use a browser to browse to the same URL, and the content will be
// rendered there as a standard web page.
using HttpClient client = new();
HttpResponseMessage responseMessage = await client.GetAsync(
    $"http://localhost:{server.Port}/index.html");
string responseContent = await responseMessage.Content.ReadAsStringAsync();
Console.WriteLine(responseContent);

// Stop the server from listening to incoming requests.
server.Stop();
```

The PinchHitter server also supports the WebSocket protocol to allow you to mock activity for testing
purposes.

```csharp
using System.Net.WebSockets;
using System.Text;
using PinchHitter;

// Start a new server to listen on a random port.
Server server = new();
server.Start();

// Set up an event handler for when clients connect to
// this server.
ManualResetEvent connectionEvent = new(false);
string connectionId = string.Empty;
server.ClientConnected += (sender, e) =>
{
    connectionId = e.ConnectionId;
    connectionEvent.Set();
};

// Connect to the server with a ClientWebSocket instance.
// The PinchHitter server handles the HTTP-to-WebSocket
// connection upgrade handshake automatically.
using ClientWebSocket client = new();
await client.ConnectAsync(
    new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
connectionEvent.WaitOne(TimeSpan.FromSeconds(1));

// Set up an event handler to monitor when the server
// receives data from an attached client. Note that
// we can check the connection ID to validate which
// connected client is sending the data.
ManualResetEvent serverReceiveSyncEvent = new(false);
string? dataReceivedFromClient = null;
server.DataReceived += (sender, e) =>
{
    if (e.ConnectionId == connectionId)
    {
        dataReceivedFromClient = e.Data;
        serverReceiveSyncEvent.Set();
    }
};

// Send the data to the server asynchronously, and wait
// for the server to have received the data.
string dataToSend = "Hello from a WebSocket client";
byte[] sendBuffer = Encoding.UTF8.GetBytes(dataToSend);
await client.SendAsync(
    sendBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
serverReceiveSyncEvent.WaitOne(TimeSpan.FromSeconds(1));
Console.WriteLine($"Data received from client: {dataReceivedFromClient}");

// The WebSocket connection is full duplex, and data can
// be sent in either direction. Set up an asynchronous task
// to receive data from the server.
ArraySegment<byte> receiveBuffer = WebSocket.CreateClientBuffer(1024, 1024);
Task<WebSocketReceiveResult> clientReceiveTask = 
    Task.Run(() => client.ReceiveAsync(receiveBuffer, CancellationToken.None));

// Send data from the server to the client, and wait for
// the client receive task to complete.
await server.SendData(connectionId, "Hello back from the PinchHitter server");
await clientReceiveTask;
WebSocketReceiveResult result = clientReceiveTask.Result;
string dataSentToClient =
    Encoding.UTF8.GetString(receiveBuffer.Array!, 0, result.Count);
Console.WriteLine($"Data sent to client: {dataSentToClient}");

// Stop the server from listening to WebSocket data.
server.Stop();
```

## Development
The library is built to support .NETStandard 2.0. This should allow the widest usage of the library across
the largest number of framework versions, including .NET Framework, .NET Core, and .NET 5 and higher.

To build the library, after cloning the repository, execute the following in a terminal window
in the root of your clone:

    dotnet build

To run the project unit tests, execute the following in a terminal window:

    dotnet test

There are three projects in this repository:
* src/PinchHitter/PinchHitter.csproj - The main library source code
* src/PinchHitter.Client/PinchHitter.Client.csproj - A console application used as a "playground"
for practice using the library. Changes to this project are not canonical at this time, and this
project should not be viewed as having desirable coding practices.
* test/PinchHitter.Tests/PinchHitter.Tests.csproj - The unit tests for the main library

[Visual Studio Code](https://code.visualstudio.com/) is the preferred IDE for development of this library.
It can be used across multiple operating systems, and there should be nothing platform-specific in the
library or its unit tests that would require platform-specific code. For working with C# code, we recommend
using the [C# for Visual Studio Code plugin](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp).

The project uses [NUnit](https://nunit.org/) for its unit tests.

The project has enabled Roslyn analyzers to help with code quality, and uses the
[StyleCop analyzers](https://www.nuget.org/packages/StyleCop.Analyzers) to enforce a consistent code style.
PRs should contain no warnings from any of the analyzers. Use of warning suppression in the source code
is mostly prohibited, and will only be allowed on a very strictly reviewed case-by-case basis.

The project uses [GitHub Actions](https://github.com/jimevans/PinchHitter/actions) for continuous
integration (CI). Code coverage statistics are generated and gathered by
[Coverlet](https://www.nuget.org/packages/coverlet.collector/), and uploaded to
[coveralls.io](https://coveralls.io/github/jimevans/PinchHitter?branch=main). PRs for which
the code coverage drops from the current percentage on the `main` branch will need to be carefully
reviewed.

Some useful plugins in your Visual Studio Code environment for this project are:
* [.NET Core Test Explorer](https://marketplace.visualstudio.com/items?itemName=formulahendry.dotnet-test-explorer):
This plugin allows one to execute any or all of the unit tests from within the IDE. By changing the settings
of the plugin to add `/p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=../coverage/lcov` to
the test arguments, code coverage data can be collected locally when the tests are executed using the explorer.
* [Coverage Gutters](https://marketplace.visualstudio.com/items?itemName=ryanluker.vscode-coverage-gutters):
This plugin allows visualization of code coverage directly within the IDE.

## A Word About the Project Name
I am a fan of the American sport of [baseball](https://en.wikipedia.org/wiki/Baseball).
My experience with the game is related to my family, and comes to me from my late grandfather.
He played the game at a semi-professional level in the 1940s, and he and I bonded over it when
I was a child. Because of my love for the game, I've taken to naming individual projects I've
created after various terms in the game. A "pinch hitter" is a player who bats in place of a
teammate, substituting for them. Similar to association football (known as "soccer" in the
United States), the replaced player may not return to the game. The name of this project has
no significance other than it is a term from a sport I enjoy watching and discussing.