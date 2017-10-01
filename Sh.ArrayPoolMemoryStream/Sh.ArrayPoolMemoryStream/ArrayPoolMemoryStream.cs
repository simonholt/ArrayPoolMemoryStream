using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;

namespace Sh.ArrayPoolMemoryStream
{
    public sealed class ArrayPoolMemoryStream : Stream
    {
        const int minimumSegmentSize = 64 * 1024;

        const int maximumSegmentSize = 1024 * 1024;

        private readonly ArrayPool<byte> arrayPool;

        private readonly List<byte[]> segments = new List<byte[]>();

        private int dataInLastSegment = 0;

        #region Seek pointers

        private int seekOffset = 0;

        private int segmentIndex = 0;

        private byte[] seekSegment;

        #endregion

        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override bool CanSeek => true;

        public override bool CanTimeout => false;

        public override long Position
        {
            get
            {
                var length = 0;
                for (int i = 0; i < segmentIndex; i++)
                {
                    length += segments[i].Length;
                }
                length += seekOffset;
                return length;
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public override long Length
        {
            get
            {
                var length = 0;
                for (int i = 0; i < segments.Count - 1; i++)
                {
                    length += segments[i].Length;
                }
                length += dataInLastSegment;
                return length;
            }
        }

        public ArrayPoolMemoryStream() : this(ArrayPool<byte>.Shared)
        {
        }

        public ArrayPoolMemoryStream(ArrayPool<byte> arrayPool, int initialCapacity = 64 * 1024)
        {
            this.arrayPool = arrayPool ?? throw new ArgumentNullException(nameof(arrayPool));
            if (initialCapacity < 0) throw new ArgumentException("Initial Capacity must not be negative", nameof(initialCapacity));

            initialCapacity = (initialCapacity < minimumSegmentSize) ? minimumSegmentSize : (initialCapacity > maximumSegmentSize) ? maximumSegmentSize : initialCapacity;
            var newSegment = this.arrayPool.Rent(initialCapacity);
            segments.Add(newSegment);
            Seek(0, SeekOrigin.Begin);
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var segment in segments)
            {
                arrayPool.Return(segment, true);
            }
            segments.Clear();
        }

        public override void SetLength(long value)
        {
            // TODO:
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            // TODO
            if (offset == 0 && origin == SeekOrigin.Begin)
            {
                seekOffset = 0;
                segmentIndex = 0;
                seekSegment = segments[0];
                return 0;
            }
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Contract.Requires(buffer != null);
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(offset + count <= buffer.Length);

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            while (count > 0)
            {
                var bytesWritten = WriteToSeekSegment(buffer, offset, count);

                offset += bytesWritten;
                count -= bytesWritten;
                seekOffset += bytesWritten;

                if (segmentIndex == segments.Count - 1)
                {
                    dataInLastSegment = Math.Max(seekOffset, dataInLastSegment);
                }

                if (count > 0)
                {
                    if (segmentIndex == segments.Count - 1)
                    {
                        var suggestedSize = segments.Count >= 3 ? maximumSegmentSize :
                                            (count < minimumSegmentSize) ? minimumSegmentSize :
                                            (count > maximumSegmentSize) ? maximumSegmentSize :
                                            count;

                        var newSegment = arrayPool.Rent(suggestedSize);
                        segments.Add(newSegment);
                        dataInLastSegment = 0;
                    }

                    segmentIndex += 1;
                    seekOffset = 0;
                    seekSegment = segments[segmentIndex];
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            int totalBytesRead = 0;
            while (count > 0)
            {
                var remainingBytesInSeekSegment = segmentIndex == segments.Count - 1 ?
                                                    dataInLastSegment - seekOffset :
                                                    seekSegment.Length - seekOffset;


                var bytesToReadFromSeekSegment = Math.Min(remainingBytesInSeekSegment, count);

                Buffer.BlockCopy(seekSegment, seekOffset, buffer, offset, bytesToReadFromSeekSegment);

                offset += bytesToReadFromSeekSegment;
                count -= bytesToReadFromSeekSegment;

                totalBytesRead += bytesToReadFromSeekSegment;
                seekOffset += bytesToReadFromSeekSegment;

                if (count == 0)
                {
                    return totalBytesRead;
                }
                else if (segmentIndex == segments.Count - 1 && seekOffset >= dataInLastSegment)
                {
                    return totalBytesRead;
                }
                else
                {
                    seekOffset = 0;
                    segmentIndex += 1;
                    seekSegment = segments[segmentIndex];
                }
            }
            return totalBytesRead;
        }

        public override void WriteByte(byte value)
        {
            // TODO - zero-alloc version
            byte[] tempVal = new byte[1];
            tempVal[0] = value;
            this.Write(tempVal, 0, 1);
        }

        public override int ReadByte()
        {
            // TODO - zero-alloc version
            byte[] tempVal = new byte[1];
            int result = Read(tempVal, 0, 1);
            if (result == 1)
                return tempVal[0];
            else
                return -1;
        }

        public byte[] ToArray()
        {
            Seek(0, SeekOrigin.Begin);
            var length = (int)this.Length;
            var target = new byte[length];
            this.Read(target, 0, length);
            return target;
        }

        private int WriteToSeekSegment(byte[] source, int sourceOffset, int sourceCount)
        {
            var remainingBytesInSegment = seekSegment.Length - seekOffset;
            var bytesToWrite = Math.Min(remainingBytesInSegment, sourceCount);

            Contract.Assert(bytesToWrite >= 0);
            Buffer.BlockCopy(source, sourceOffset, seekSegment, seekOffset, bytesToWrite);
            return bytesToWrite;
        }
    }
}
