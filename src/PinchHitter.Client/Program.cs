using PinchHitter;

// See https://aka.ms/new-console-template for more information

WebServer server = new();


server.RegisterResource("/", new WebResource("/index.html") { IsRedirect = true });
server.RegisterResource("/index.html", WebResource.CreateHtmlResource("<h1>Welcome to the PinchHitter web server</h1><p>You can browse using localhost</p>"));

server.Start();
Console.WriteLine($"Serving pages at http://localhost:{server.Port}.");
Console.WriteLine("Press <Enter> to shut down the server.");
Console.ReadLine();
server.Stop();