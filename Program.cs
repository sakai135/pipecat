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
    Stream namedPipe = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, NamedPipeOptions.Asynchronous),
    input = Console.OpenStandardInput(),
    output = Console.OpenStandardOutput()
)
{
    ((NamedPipeClientStream)namedPipe).Connect();
    Log("Connected");

    var pipeFromInput = new Pipe();
    var pipeToOutput = new Pipe();

    Parallel.Invoke
    (
        () => Read(pipeFromInput.Writer, input).Wait(),
        () => Write(pipeFromInput.Reader, namedPipe).Wait(),
        () => Read(pipeToOutput.Writer, namedPipe).Wait(),
        () => Write(pipeToOutput.Reader, output).Wait()
    );
}

void Log(string msg)
{
    Console.Error.WriteLine(msg);
}

async Task Read(PipeWriter writer, Stream input)
{
    while (true)
    {
        var task = InnerRead(writer, input);
        var result = task.IsCompletedSuccessfully ? task.Result : await task;
        if (result.IsCompleted)
        {
            break;
        }
    }
    writer.Complete();
}

ValueTask<FlushResult> InnerRead(PipeWriter writer, Stream input)
{
    var span = writer.GetSpan();
    var bytesRead = input.Read(span);
    writer.Advance(bytesRead);
    return writer.FlushAsync();
}

async Task Write(PipeReader reader, Stream output)
{
    while (true)
    {
        var result = await reader.ReadAsync();
        var seq = result.Buffer;
        if (seq.IsSingleSegment)
        {
            output.Write(seq.FirstSpan);
        }
        else
        {
            foreach (var mem in seq)
            {
                output.Write(mem.Span);
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
