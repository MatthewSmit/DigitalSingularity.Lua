namespace DigitalSingularity.Lua;

internal sealed class LineBufferedStream(Stream stream, int bufferSize) : Stream
{
    private readonly BufferedStream stream = new(stream, bufferSize);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.stream.Dispose();
        }
    }

    public override ValueTask DisposeAsync()
    {
        return this.stream.DisposeAsync();
    }

    public override void Flush()
    {
        this.stream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return this.stream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return this.stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return this.stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        this.stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        this.stream.Write(buffer, offset, count);
        ReadOnlySpan<byte> span = new(buffer, offset, count);
        if (span.Contains((byte)'\n'))
        {
            this.Flush();
        }
    }

    public override bool CanRead => this.stream.CanRead;
    public override bool CanSeek => this.stream.CanSeek;
    
    public override bool CanWrite => this.stream.CanWrite;
    
    public override long Length => this.stream.Length;

    public override long Position
    {
        get => this.stream.Position;
        set => this.stream.Position = value;
    }
}
