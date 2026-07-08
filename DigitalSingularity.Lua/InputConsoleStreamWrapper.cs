namespace DigitalSingularity.Lua;

internal sealed class InputConsoleStreamWrapper(TextReader input) : Stream
{
    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return 0;
        }
        
        int next = input.Read();
        if (next < 0)
        {
            return 0;
        }

        if (next > byte.MaxValue)
        {
            throw new NotImplementedException();
        }

        buffer[offset] = (byte)next;
        return 1;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
}
