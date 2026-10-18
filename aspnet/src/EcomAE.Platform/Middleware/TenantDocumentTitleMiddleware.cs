using System.Text;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Rewrites the document title after the page renders, so every ERP screen
/// picks up the open company without a per-page title edit.
/// </summary>
public sealed class TenantDocumentTitleMiddleware
{
    private readonly RequestDelegate _next;

    public TenantDocumentTitleMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var brand = ErpDocumentTitle.Brand(context);
        if (brand is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var original = context.Response.Body;
        var rewrite = new HeadTitleRewriteStream(original, brand);
        context.Response.Body = rewrite;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.ContentLength = null;
            return Task.CompletedTask;
        });
        try
        {
            await _next(context).ConfigureAwait(false);
            await rewrite.CompleteAsync(context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = original;
        }
    }

    private sealed class HeadTitleRewriteStream : Stream
    {
        private static readonly byte[] HeadClose = "</head>"u8.ToArray();
        private readonly Stream _inner;
        private readonly string _brand;
        private readonly MemoryStream _buffer = new();
        private bool _done;

        public HeadTitleRewriteStream(Stream inner, string brand)
        {
            _inner = inner;
            _brand = brand;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_done)
            {
                _inner.Write(buffer);
                return;
            }

            _buffer.Write(buffer);
            TryRelease(final: false);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_done)
            {
                return _inner.WriteAsync(buffer, cancellationToken);
            }

            _buffer.Write(buffer.Span);
            return ReleaseAsync(final: false, cancellationToken);
        }

        public Task CompleteAsync(CancellationToken cancellationToken)
            => _done ? Task.CompletedTask : ReleaseAsync(final: true, cancellationToken).AsTask();

        private void TryRelease(bool final)
        {
            if (!ShouldRelease(final))
            {
                return;
            }

            var payload = _buffer.ToArray();
            _buffer.SetLength(0);
            var rewrite = IndexOf(payload, HeadClose) >= 0;
            var bytes = rewrite
                ? Encoding.UTF8.GetBytes(ErpDocumentTitle.RewriteDocument(Encoding.UTF8.GetString(payload), _brand))
                : payload;
            _inner.Write(bytes);
            _done = true;
        }

        private async ValueTask ReleaseAsync(bool final, CancellationToken cancellationToken)
        {
            if (!ShouldRelease(final))
            {
                return;
            }

            var payload = _buffer.ToArray();
            _buffer.SetLength(0);
            var rewrite = IndexOf(payload, HeadClose) >= 0;
            var bytes = rewrite
                ? Encoding.UTF8.GetBytes(ErpDocumentTitle.RewriteDocument(Encoding.UTF8.GetString(payload), _brand))
                : payload;
            await _inner.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            _done = true;
        }

        private bool ShouldRelease(bool final)
        {
            if (_done || _buffer.Length == 0)
            {
                return false;
            }

            var payload = _buffer.GetBuffer();
            var length = (int)_buffer.Length;
            return IndexOf(payload.AsSpan(0, length), HeadClose) >= 0 || length >= 65_536 || final;
        }

        private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
            {
                return -1;
            }

            var last = haystack.Length - needle.Length;
            for (var i = 0; i <= last; i++)
            {
                var matched = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    var left = haystack[i + j];
                    var right = needle[j];
                    if (left is >= (byte)'A' and <= (byte)'Z')
                    {
                        left += 32;
                    }

                    if (right is >= (byte)'A' and <= (byte)'Z')
                    {
                        right += 32;
                    }

                    if (left != right)
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return i;
                }
            }

            return -1;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
