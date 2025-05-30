using System.IO.Pipes;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            return;
        }

        var uri = args[0];
        NamedPipeClientStream pipe = null;

        try
        {
            pipe = new NamedPipeClientStream(".", "CodeAiAuth", PipeDirection.Out);
            pipe.Connect(2000);
            using (var sw = new StreamWriter(pipe) { AutoFlush = true })
            {
                sw.WriteLine(uri);

                Console.WriteLine("Done");
            }
        }
        catch (TimeoutException)
        {
            Console.WriteLine("Timeout connecting to pipe.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex);
        }
        finally
        {
            pipe?.Dispose();
        }
    }
}
