using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class Data {
    public Line[] Lines { get; set; }
}

public class Line {
    public string Message { get; set; }
    public string Color { get; set; }
}

class Program {
    static async Task Main(string[] args) {
        // Create an instance of HttpListener
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/"); // URL to listen to
        listener.Start();
        Console.WriteLine("Listening for connections on http://localhost:8080/");

        ThreadPool.SetMaxThreads(6, 6);

        // Main loop to accept requests asynchronously
        while (true) {
            // Accept an incoming request asynchronously
            var context = await listener.GetContextAsync();

            ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
        }
    }

    static async void HandleRequest(object input) {
        var context = input as HttpListenerContext;

        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        response.AddHeader("Access-Control-Allow-Origin", "*");

        if (request.HttpMethod == "POST" && request.HasEntityBody) {
            // Read the body of the request (JSON data)
            using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding)) {
                string json = await reader.ReadToEndAsync();
                Console.WriteLine("Received JSON: " + json);

                // Optionally, you can deserialize the JSON into a C# object
                Data data;

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

                var chatgui = new SAMPChatGUI();

                chatgui.Render(data);

                var image = chatgui.SaveSurfaceToPngInMemory();

                chatgui.Dispose();

                response.ContentLength64 = image.Length;
                response.ContentType = "image/png";
                await response.OutputStream.WriteAsync(image, 0, image.Length);
                response.OutputStream.Close();
            }
        }
        else {
            // Handle invalid request
            response.StatusCode = 400; // Bad Request
            response.Close();
        }
    }
}
