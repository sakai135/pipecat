using System;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Threading.Tasks;
using NamedPipeOptions = System.IO.Pipes.PipeOptions;

const string serverName = ".";

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
    Stream npipe = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, NamedPipeOptions.Asynchronous),
    stdin = Console.OpenStandardInput(),
    stdout = Console.OpenStandardOutput()
)
{
    ((NamedPipeClientStream)npipe).Connect();
    Log("Connected");

    await Task.WhenAll
    (     
        Connect(stdin, npipe),
        Connect(npipe, stdout)
    );
}

void Log(string msg)
{
    Console.Error.WriteLine(msg);
}

async Task Connect(Stream input, Stream output)
{
    var reader = PipeReader.Create(input);
    while (true)
    {
        await reader.CopyToAsync(output);
    }
}
