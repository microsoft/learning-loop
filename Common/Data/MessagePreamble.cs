// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Data
{
    /*
     * All messages send to Trainer will contain a 8 byte message
     * preamble consisting of the following fields.
     *
     * Reserved - 1 byte
     * Version  - 1 byte
     * MsgType  - 2 bytes,  unsigned int 16
     * MsgSize  - 4 bytes,  unsigned int 32
     *
     * Interpretation of messages will depend on the MsgType field.
     * Check MessageType.cs for MsgTypes
     */
    public class MessagePreamble
    {
        public const int PREAMBLE_SIZE = 8;
        public const int MESSAGE_TYPE_OFFSET = 2;
        public const int MESSAGE_SIZE_OFFSET = 4;

        public byte Reserved { get; set; } = 0;
        public byte Version { get; set; } = 0;
        public ushort MsgType { get; set; } = 0;
        public uint MsgSize { get; set; } = 0;
        public static int SerializedSize { get; } = 8;

        public bool ReadFromBytes(ArraySegment<byte> data)
        {
            var off = data.Offset;

            if (data.Count < SerializedSize)
                return false;
            // Reserve should be 0 for now
            if (data.Array[off + 0] != 0)
                return false;
            // Version should be 0 for now
            if (data.Array[off + 1] != 0)
                return false;
            // First byte is reserved.
            Reserved = data.Array[off + 0];
            // Second byte is version.
            Version = data.Array[off + 1];

            // Network format is BigEndian
            if (BitConverter.IsLittleEndian)
            {
                byte[] arrMsgType = {
                    data.Array[off + 3],
                    data.Array[off + 2]
                };

                byte[] arrMsgSize = {
                    data.Array[off + 7],
                    data.Array[off + 6],
                    data.Array[off + 5],
                    data.Array[off + 4]
                };

                MsgType = BitConverter.ToUInt16(arrMsgType,0);
                MsgSize = BitConverter.ToUInt32(arrMsgSize,0);
            }
            else
            {
                MsgType = BitConverter.ToUInt16(data.Array, off + 2);
                MsgSize = BitConverter.ToUInt32(data.Array, off + 4);
            }

            return true;
        }

        public byte[] ToBytes()
        {
            var data = new byte[SerializedSize];
            data[0] = Reserved;
            data[1] = Version;

            byte[] msgTypeArr  = BitConverter.GetBytes(MsgType);
            byte[] msgSizeArr = BitConverter.GetBytes(MsgSize);

            // Network order is BigEndian.  Convert to BigEndian if necessary 
            if (BitConverter.IsLittleEndian)
            {
                // Two bytes for msg type
                data[3] = msgTypeArr[0];
                data[2] = msgTypeArr[1];

                // Four bytes for msg size
                data[7] = msgSizeArr[0];
                data[6] = msgSizeArr[1];
                data[5] = msgSizeArr[2];
                data[4] = msgSizeArr[3];
            }
            else
            {
                // Two bytes for msg type
                data[2] = msgTypeArr[0];
                data[3] = msgTypeArr[1];

                // Four bytes for msg size
                data[4] = msgSizeArr[0];
                data[5] = msgSizeArr[1];
                data[6] = msgSizeArr[2];
                data[7] = msgSizeArr[3];
            }

            return data;
        }
    }
}
