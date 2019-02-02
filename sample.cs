using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using WebAssembly;
using WebAssembly.Net.Http.HttpClient;
using System.Threading.Tasks;

using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

public class Math {
	public static int IntAdd (int a, int b) {
		// var cp = new Simple.Complex (10, "hello");
		// int c = a + b;
		// int d = c + b;
		// int e = d + a;

		// e += cp.DoStuff ();

        var e = a + b;
		return e;
	}

    public static async Task<string> RunScript(string script)
    {
        
            string CsCode = @"
                using System.Text;
                namespace CompileBlazorInBlazor.Demo
                {
                    public class RunClass
                    {
                        public string Run(string name, int count)
                        {
                            var sb = new StringBuilder();
                            for (int i = 0; i < count; i++)
                            {
                                sb.AppendLine($""{i}) Hello, {name}!"");
                            }
                            return sb.ToString();
                        }
                    }
                }
                ";

            MyCompiler compiler = new MyCompiler();
            string result = await compiler.CompileAndRun(CsCode);
            Console.WriteLine("result completed");
            Console.WriteLine(result);
            return result;
        // return "smurf";
    }


	public int First (int[] x) {
		return x.FirstOrDefault ();
	}
}

public class MyCompiler
{
    private HttpClient _http = new HttpClient();
    public List<string> CompileLog = new List<string>();
    private List<MetadataReference> references { get; set; }

    public async Task Init(string baseurl)
    {
        if (references == null)
        {
            _http.BaseAddress = new Uri("http://127.0.0.1:5500");
            references = new List<MetadataReference>();
            Console.WriteLine("Reading References");
            //byte[] bytes = new byte[20];
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                //string url = "/publish/managed/" + assembly.Location;
                string url = "";
                if (assembly.Location.IndexOf("\\")>-1)
                    url = "/publish/managed/" + assembly.Location.Substring(assembly.Location.LastIndexOf("\\") + 1);
                else
                    url = "/publish/managed/" + assembly.Location;

                //string url = "/publish/mscorlib.dll";


                //HttpResponseMessage response = await this._http.GetAsync(url);

                //if (response.IsSuccessStatusCode)
                //{
                //    bytes = await response.Content.ReadAsByteArrayAsync();
                //}

                Console.WriteLine(url);

                //references.Add(MetadataReference.CreateFromFile(assembly.Location));
                //references.Add(
                //    MetadataReference.CreateFromImage(bytes));
                //var stream = await this._http.GetStreamAsync(url);
                //Console.WriteLine("CanRead: " + stream.CanRead);
                //Console.WriteLine("CanSeek: " + stream.CanSeek);

                //references.Add(
                //    MetadataReference.CreateFromStream(await this._http.GetStreamAsync(url)));

                using (MemoryStream memstream = new MemoryStream())
                {

                    var httpstream = await this._http.GetStreamAsync(url);
                    await httpstream.CopyToAsync(memstream);

                    memstream.Seek(0, SeekOrigin.Begin);

                    references.Add(
                        MetadataReference.CreateFromStream(memstream));
                }

                break;

            }

            //byte[] bytes = new byte[20];
            //string newurl = "/publish/managed/" + typeof(object).Assembly.Location;
            //HttpResponseMessage response = await this._http.GetAsync(newurl);

            //if (response.IsSuccessStatusCode)
            //{
            //    bytes = await response.Content.ReadAsByteArrayAsync();
            //}
            //references.Add(MetadataReference.CreateFromImage(bytes));

            Console.WriteLine("finished reading references");
            //references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        }
    }

    public async Task<Assembly> Compile(string code)
    {
        await Init("http://localhost");

        Console.WriteLine("Compiling");

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
            foreach (var diagnostic in syntaxTree.GetDiagnostics())
            {
                CompileLog.Add(diagnostic.ToString());
                Console.WriteLine("SyntaxTree Diagnostic: " + diagnostic.ToString());
            }

            Console.WriteLine("Parse SyntaxTree Success");
            CompileLog.Add("Parse SyntaxTree Success");

            CSharpCompilation compilation = CSharpCompilation.Create("CompileBlazorInBlazor.Demo", new[] { syntaxTree },
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (MemoryStream stream = new MemoryStream())
            {
                EmitResult result = compilation.Emit(stream);

                foreach (var diagnostic in result.Diagnostics)
                {
                    CompileLog.Add(diagnostic.ToString());
                    Console.WriteLine("Diagnostic: " + diagnostic.ToString());
                }

                if (!result.Success)
                {
                    Console.WriteLine("Build Failed");
                    CompileLog.Add("Compilation error");
                    return null;
                }
                else
                    Console.WriteLine("Build Succeeded");

                CompileLog.Add("Compilation success!");
                Assembly assemby = AppDomain.CurrentDomain.Load(stream.ToArray());
                return assemby;
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message + ex.StackTrace);
        }

        return null;

        
    }


    public async Task<string> CompileAndRun(string code)
    {
        await Init("http://localhost");

        var assemby = await this.Compile(code);
        Console.WriteLine("Running");


        if (assemby != null)
        {
            var type = assemby.GetExportedTypes().FirstOrDefault();
            var methodInfo = type.GetMethod("Run");
            var instance = Activator.CreateInstance(type);
            return (string)methodInfo.Invoke(instance, new object[] { "my UserName", 12 });
        }
        else
            Console.WriteLine("No Assembly");

        return null;
    }
}

namespace GeoLocation
{
    class Program
    {

        static DOMObject navigator;
        static DOMObject global;
        static string BaseApiUrl = string.Empty;
        static HttpClient httpClient;

        static void Main(string[] args)
        {
            global = new DOMObject(string.Empty);
            navigator = new DOMObject("navigator");

            using (var window = (JSObject)WebAssembly.Runtime.GetGlobalObject("window"))
                using (var location = (JSObject)window.GetObjectProperty("location"))
                {
                    BaseApiUrl = (string)location.GetObjectProperty("origin");
                }

            httpClient = new HttpClient() { BaseAddress = new Uri(BaseApiUrl) };

        }

        static int requests = 0;
        static void GeoFindMe(JSObject output)
        {
            GeoLocation geoLocation;
            try
            {
                geoLocation = new GeoLocation(navigator.GetProperty("geolocation"));
            }
            catch
            {
                output.SetObjectProperty("innerHTML", "<p>Geolocation is not supported by your browser</p>");
                return;
            }

            output.SetObjectProperty("innerHTML", "<p>Locating…</p>");

            geoLocation.OnSuccess += async (object sender, Position position) =>
            {
                using (position)
                {
                    using (var coords = position.Coordinates)
                    {
                        var latitude = coords.Latitude;
                        var longitude = coords.Longitude;

                        output.SetObjectProperty("innerHTML", $"<p>Latitude is {latitude} ° <br>Longitude is {longitude} °</p>");

                        try {

                            var ApiFile = $"https://maps.googleapis.com/maps/api/staticmap?center={latitude},{longitude}&zoom=13&size=300x300&sensor=false";

                            var rspMsg = await httpClient.GetAsync(ApiFile);
                            if (rspMsg.IsSuccessStatusCode)
                            {

                                var mimeType = getMimeType(rspMsg.Content?.ReadAsByteArrayAsync().Result);
                                Console.WriteLine($"Request: {++requests}  ByteAsync: {rspMsg.Content?.ReadAsByteArrayAsync().Result.Length}  MimeType: {mimeType}");
                                global.Invoke("showMyPosition", mimeType, Convert.ToBase64String(rspMsg.Content?.ReadAsByteArrayAsync().Result));
                            }
                            else
                            {
                                output.SetObjectProperty("innerHTML", $"<p>Latitude is {latitude} ° <br>Longitude is {longitude} </p><br>StatusCode: {rspMsg.StatusCode} <br>Response Message: {rspMsg.Content?.ReadAsStringAsync().Result}</p>");
                            }
                        }
                        catch (Exception exc2)
                        {
                            Console.WriteLine($"GeoLocation HttpClient Exception: {exc2.Message}");
                            Console.WriteLine($"GeoLocation HttpClient InnerException: {exc2.InnerException?.Message}");
                        }

                    }
                }

            };

            geoLocation.OnError += (object sender, PositionError e) =>
            {
                output.SetObjectProperty("innerHTML", $"Unable to retrieve your location: Code: {e.Code} - {e.message}");
            };

            geoLocation.GetCurrentPosition();

            geoLocation = null;
        }

        static string getMimeType (byte[] imageData)
        {
            if (imageData.Length < 4)
                return string.Empty;

            if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
                return "image/png";
            else if (imageData[0] == 0xff && imageData[1] == 0xd8)
                return "image/jpeg";
            else if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46)
                return "image/gif";
            else
                return string.Empty;

        }
    }

    // Serves as a wrapper around a JSObject.
    class DOMObject : IDisposable
    {
        public JSObject ManagedJSObject { get; private set; }

        public DOMObject(object jsobject)
        {
            ManagedJSObject = jsobject as JSObject;
            if (ManagedJSObject == null)
                throw new NullReferenceException($"{nameof(jsobject)} must be of type JSObject and non null!");

        }

        public DOMObject(string globalName) : this((JSObject)Runtime.GetGlobalObject(globalName))
        { }

        public object GetProperty(string property)
        {
            return ManagedJSObject.GetObjectProperty(property);
        }

        public object Invoke(string method, params object[] args)
        {
            return ManagedJSObject.Invoke(method, args);
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {

            if (disposing)
            {

                // Free any other managed objects here.
                //
            }

            // Free any unmanaged objects here.
            //
            ManagedJSObject?.Dispose();
            ManagedJSObject = null;
        }

    }

    class PositionEventArgs : EventArgs
    {
        public Position Position { get; set; }
    }

    class GeoLocation : DOMObject
    {


        public event EventHandler<Position> OnSuccess;
        public event EventHandler<PositionError> OnError;

        public GeoLocation(object jsobject) : base(jsobject)
        {
        }

        public void GetCurrentPosition()
        {
            var success = new Action<object>((pos) =>
            {
                OnSuccess?.Invoke(this, new Position(pos));
            });

            var error = new Action<object>((err) =>
            {
                OnError?.Invoke(this, new PositionError(err));
            });

            ManagedJSObject.Invoke("getCurrentPosition", success, error);
        }

    }

    class Position : DOMObject
    {

        public Position(object jsobject) : base(jsobject)
        {
        }

        public Coordinates Coordinates => new Coordinates(ManagedJSObject.GetObjectProperty("coords"));

    }

    class PositionError : DOMObject
    {

        public PositionError(object jsobject) : base(jsobject)
        {
        }

        public int Code => (int)ManagedJSObject.GetObjectProperty("code");
        public string message => (string)ManagedJSObject.GetObjectProperty("message");

    }

    class Coordinates : DOMObject
    {

        public Coordinates(object jsobject) : base(jsobject)
        {
        }

        public double Latitude => (double)ManagedJSObject.GetObjectProperty("latitude");
        public double Longitude => (double)ManagedJSObject.GetObjectProperty("longitude");

    }

}
