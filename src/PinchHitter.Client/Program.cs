using PinchHitter;

// See https://aka.ms/new-console-template for more information

Server server = new();

server.RegisterHandler("/", new RedirectRequestHandler("/index.html"));
server.RegisterHandler("/index.html", new WebResourceRequestHandler(WebContent.AsHtmlDocument("<h1>Welcome to the PinchHitter web server</h1><p>You can browse using localhost</p>")));
AuthenticatedResourceRequestHandler authHandler = new("Hello World");
authHandler.AddAuthenticator(new BasicWebAuthenticator("myUser", "myPassword"));
server.RegisterHandler("/auth", authHandler);

server.RegisterHandler("/form.html", new WebResourceRequestHandler(WebContent.AsHtmlDocument(@"<form action=""/process"" method=""post""><input name=""name"" /><input type=""submit"" value=""Send form"" /></form>")));
server.RegisterHandler("/process", PinchHitter.HttpMethod.Post, new WebResourceRequestHandler(WebContent.AsHtmlDocument("Done")));

server.Start();
Console.WriteLine($"Serving pages at http://localhost:{server.Port}.");
Console.WriteLine("Press <Enter> to shut down the server.");
Console.ReadLine();
server.Stop();