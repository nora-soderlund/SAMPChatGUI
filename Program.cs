using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

public class Data {
    public int FontSize { get; set; }

    public Line[] Top { get; set; }
    public Line[] Bottom { get; set; }
}

public class Line {
    public string Message { get; set; }
    public string Color { get; set; }
}

class Program {
    static async Task Main(string[] args) {
        Console.SetOut(new PrefixedWriter());

        // Create an instance of HttpListener
        HttpListener listener = new HttpListener();

#if DEBUG
        listener.Prefixes.Add("http://localhost:8080/");
#else
        listener.Prefixes.Add("http://206.168.212.227:80/"); // URL to listen to
        listener.Prefixes.Add("http://gui.sampscreens.com:80/"); // URL to listen to
#endif

        listener.Start();
        Console.WriteLine("Listening for connections");

        ThreadPool.SetMaxThreads(6, 6);

        // Main loop to accept requests asynchronously
        while (true) {
            // Accept an incoming request asynchronously
            var context = await listener.GetContextAsync();

            ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
        }
    }

    static async void HandleRequest(object input) {
        try {
            var context = input as HttpListenerContext;

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

#if DEBUG
            response.AddHeader("Access-Control-Allow-Origin", "http://localhost:5173");
#else
            response.AddHeader("Access-Control-Allow-Origin", "https://sampscreens.com");

#endif

            if (request.HttpMethod == "POST" && request.HasEntityBody) {
                // Read the body of the request (JSON data)
                using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding)) {
                    string json = await reader.ReadToEndAsync();
                    // Optionally, you can deserialize the JSON into a C# object
                    Data data;

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    try {
                        data = JsonSerializer.Deserialize<Data>(json, new JsonSerializerOptions() {
                            PropertyNameCaseInsensitive = true
                        });

                        Console.WriteLine("Parsed data");
                    }
                    catch (Exception ex) {
                        Console.WriteLine("Error parsing JSON: " + ex.Message);

                        response.StatusCode = 400; // Bad Request
                        response.Close();

                        return;
                    }

                    string connectingIp = request.Headers.Get("CF-Connecting-IP") ?? request.RemoteEndPoint.ToString();

                    Console.WriteLine("Received request (" + connectingIp + ") with " + data.Top.Length + " top lines and " + data.Bottom.Length + " bottom lines...");

                    var chatgui = new SAMPChatGUI();

                    chatgui.Render(data);

                    var image = chatgui.SaveSurfaceToPngInMemory();

                    chatgui.Dispose();

                    response.ContentLength64 = image.Length;
                    response.ContentType = "image/png";
                    
                    stopwatch.Stop();

                    if (request.InputStream.CanRead) {
                        await response.OutputStream.WriteAsync(image, 0, image.Length);

                        Console.WriteLine("Finished request (" + connectingIp + ") after " + stopwatch.ElapsedMilliseconds + "ms.");
                    }
                    else {
                        Console.WriteLine("Canceling request (" + connectingIp + ").");
                    }
             
                    response.OutputStream.Close();
                }
            }
            else {
                // Handle invalid request
                response.StatusCode = 400; // Bad Request
                response.Close();
            }
        }
        catch(Exception exception) {
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);
        }
    }
}
