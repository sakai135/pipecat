using System;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Threading.Tasks;
using PipeOptions = System.IO.Pipes.PipeOptions;

const string serverName = ".";
const int minimumBufferSize = 8192;

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

    var pipeFromInput = new Pipe();
    var pipeToOutput = new Pipe();

    await Task.WhenAll
    (
        Read(pipeFromInput.Writer, input),
        Write(pipeFromInput.Reader, namedPipe),
        Read(pipeToOutput.Writer, namedPipe),
        Write(pipeToOutput.Reader, output)
    );
}

void Log(string msg)
{
    Console.Error.WriteLine(msg);
}

void LogException(Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(ex.StackTrace);
}

async Task Read(PipeWriter writer, Stream input)
{
    while (true)
    {
        var memory = writer.GetMemory(minimumBufferSize);
        try
        {
            var bytesRead = await input.ReadAsync(memory);
            // Log($"read {bytesRead}");
            if (bytesRead == 0)
            {
                break;
            }
            writer.Advance(bytesRead);
        }
        catch (Exception ex)
        {
            LogException(ex);
            break;
        }
        var result = await writer.FlushAsync();
        if (result.IsCompleted)
        {
            break;
        }
    }
    writer.Complete();
}

async Task Write(PipeReader reader, Stream output)
{
    while (true)
    {
        var result = await reader.ReadAsync();
        var seq = result.Buffer;
        if (seq.IsSingleSegment)
        {
            await output.WriteAsync(seq.First);
        }
        else
        {
            foreach (var mem in seq)
            {
                await output.WriteAsync(mem);
            }
        }
        reader.AdvanceTo(seq.End);
        if (result.IsCompleted)
        {
            break;
        }
    }
    reader.Complete();
}
