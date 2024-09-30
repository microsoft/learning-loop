// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DecisionService.Common
{
    public sealed class ConcatenatedByteStreams : Stream
    {
        private readonly long[] sizes;
        private readonly int[] offsets;
        private readonly byte[][] buffers;
        private readonly int buffersCount;
        private long length;
        private int index;

        public ConcatenatedByteStreams(IList<ArraySegment<byte>> streams)
        {
            int count = streams.Count;
            this.sizes = new long[count + 1];
            this.offsets = new int[count];
            this.buffers = new byte[count][];

            length = 0;
            var i = 0;

            foreach (var segment in streams)
            {
                if (segment.Count < 1)
                    continue;
                length += segment.Count;

                this.sizes[i + 1] = length;
                this.offsets[i] = segment.Offset;
                this.buffers[i] = segment.Array;

                i++;
            }
            this.buffersCount = i;
        }

        public int SegmentCount => this.offsets.Length;

        public override bool CanRead
        {
            get { return true; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.position == this.length)
                return 0;

            int totalRead = 0;
            while (count > 0)
            {
                var localPosition = this.position - this.sizes[index];

                var bytesRead = (int)Math.Min(count, (this.sizes[index + 1] - this.sizes[index]) - localPosition);
                Array.Copy(this.buffers[this.index], this.offsets[index] + localPosition, buffer, offset, bytesRead);

                this.position += bytesRead;
                count -= bytesRead;
                totalRead += bytesRead;
                offset += bytesRead;

                if (count > 0)
                {
                    this.index++;
                    if (this.index >= this.buffersCount)
                        break;
                }
            }

            return totalRead;
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length => this.length;

        private long position;
        public override long Position
        {
            get { return position;  }
            set { Seek(value, SeekOrigin.Begin);  }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long targetOffset = 0;

            switch(origin)
            {
                case SeekOrigin.Begin:
                    targetOffset = offset;
                    break;
                case SeekOrigin.Current:
                    targetOffset = this.position + offset;
                    break;
                case SeekOrigin.End:
                    targetOffset = this.length - offset;
                    break;
            }

            if (targetOffset > this.length)
                return this.position = this.length;

            if (targetOffset < 0)
                return this.position = 0;


            this.index = Array.BinarySearch(this.sizes, targetOffset);
            if (this.index < 0)
                this.index = ~this.index - 1;
            else if (this.index == this.sizes.Length)
                return this.position = this.length;

            return this.position = targetOffset;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
