// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.DecisionService.Common.Trainer.FlatBuffers
{
    public class CheckpointInfo
    {
        public RewardFunctionType FbRewardType { get; set; }
        public float DefaultReward { get; set; }
    }

    public class BinaryLogHeaderBuilder
    {
        private ArraySegment<byte> checkpoint = ArraySegment<byte>.Empty;
        private ArraySegment<byte> fileHeader = ArraySegment<byte>.Empty;

        const int FILE_MAGIC_LENGTH = 4;
        const int FILE_HEADER_LENGTH = 8;

        public const int FILE_VERSION = 1;
        //constant in little-endian format
        public const int FILE_MAGIC = 0x42465756;


        public void AddFileHeader(Dictionary<string, string> properties)
        {
            BinaryLogBuilder builder = new BinaryLogBuilder(BinaryLogBuilder.FILE_HEADER_SIZE_GUESTIMATE);

            builder.AddFileHeader(properties);
            this.fileHeader = builder.FinishFileHeader();
        }

        public void AddCheckpointInfo(CheckpointInfo info)
        {
            BinaryLogBuilder builder = new BinaryLogBuilder(BinaryLogBuilder.CHECKPOINT_SIZE_GUESTIMATE);

            builder.AddCheckpointInfo(info);
            this.checkpoint = builder.FinishCheckpointInfo();
        }

        public ArraySegment<byte> Finish()
        {
            int checkpointOffset = 0;
            if (fileHeader.Count > 0)
                checkpointOffset += FILE_HEADER_LENGTH + fileHeader.Count;

            byte[] result = new byte[checkpointOffset + checkpoint.Count];

            // file magic
            if (checkpointOffset > 0)
            {
                Span<int> span = MemoryMarshal.Cast<byte, int>(result);

                result[0] = (byte)'V';
                result[1] = (byte)'W';
                result[2] = (byte)'F';
                result[3] = (byte)'B';

                //Version 1
                span[1] = BinaryLogHeaderBuilder.FILE_VERSION;
                Array.Copy(fileHeader.Array, fileHeader.Offset, result, FILE_HEADER_LENGTH, fileHeader.Count);
            }
            Array.Copy(checkpoint.Array, checkpoint.Offset, result, checkpointOffset, checkpoint.Count);
            return result;
        }
    }
}
