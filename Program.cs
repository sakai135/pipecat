using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using PipeOptions = System.IO.Pipes.PipeOptions;

const string serverName = ".";
const int bufferSize = 8192;

if (!Console.IsInputRedirected || !Console.IsOutputRedirected)
{
    Log("STDIO not redirected");
    Environment.Exit(1);
}
if (args.Length <= 0)
{
    Log("pipe name not specified");
    Environment.Exit(1);
}

var pipeName = args[0];
Log($"Connecting to \\\\{serverName}\\pipe\\{pipeName} ...");
using
(
    Stream namedPipe = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous),
    input = Console.OpenStandardInput(),
    output = Console.OpenStandardOutput()
)
{
    ((NamedPipeClientStream)namedPipe).Connect();
    Log("Connected");

    Parallel.Invoke
    (
        () => Copy(input, namedPipe),
        () => Copy(namedPipe, output)
    );
}

void Log(string msg)
{
    Console.Error.WriteLine(msg);
}

void Copy(Stream input, Stream output)
{
    Span<byte> buffer = stackalloc byte[bufferSize];
    while (true)
    {
        var bytesRead = input.Read(buffer);
        if (bytesRead > 0)
        {
            output.Write(buffer.Slice(0, bytesRead));
        }
    }
}
