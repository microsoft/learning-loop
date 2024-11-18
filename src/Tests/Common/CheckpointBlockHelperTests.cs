// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.DecisionService.Common.Storage;
using Newtonsoft.Json;
using System.Text;

namespace Tests.Common
{
    internal static class AssertExtensions
    {
        public static void BothOrNietherAreNull<T>(this Assert _, T expected, T actual)
        {
            if (expected == null)
            {
                Assert.IsNull(actual);
            }
            else
            {
                Assert.IsNotNull(actual);
            }
        }

        public static void AreCheckpointsEqual(this Assert _, ModelCheckpoint expected, ModelCheckpoint actual)
        {
            Assert.That.BothOrNietherAreNull(expected, actual);
            if (expected == null)
            {
                return;
            }

            Assert.AreEqual(expected.WarmstartModelUrl, actual.WarmstartModelUrl);
            Assert.AreEqual(expected.Timestamp, actual.Timestamp);
            CollectionAssert.AreEqual(expected.Model, actual.Model);
        }

        public static void AllAreSameLength(this Assert _, IEnumerable<string> strings)
        {
            if (strings == null)
            {
                return;
            }

            // NOTE: We need to be careful to avoid using methods like First or Any that consume the sequence.
            int? expectedLength = null;
            foreach (var s in strings)
            {
                if (expectedLength == null)
                {
                    expectedLength = s.Length;
                }

                Assert.AreEqual(expectedLength, s.Length);
            }
        }
    }

    internal static class ListExtensions
    {
        private static readonly Random rng = new Random();

        /// <summary>
        /// Shuffles a list randomly. Modifies the original list.
        /// </summary>
        /// <typeparam name="T">The type help by the list.</typeparam>
        /// <param name="list">The <see cref="IList"/> holding type <typeparamref name="T"/> to be shuffled.</param>
        public static void Shuffle<T>(this IList<T> list)
        {
            // Fisher-Yates Shuffle
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    [TestClass]
    public class CheckpointBlockHelperTests
    {
        public TestContext TestContext { get; set; }

        private Mock<IBlockStore> MockBlockStore
        {
            get
            {
                return (Mock<IBlockStore>)TestContext.Properties?[nameof(MockBlockStore)];
            }

            set => TestContext.Properties[nameof(MockBlockStore)] = value;
        }

        private Mock<IBlockStoreProvider> MockStoreProvider
        {
            get
            {
                return (Mock<IBlockStoreProvider>)TestContext.Properties?[nameof(MockStoreProvider)];
            }

            set => TestContext.Properties[nameof(MockStoreProvider)] = value;
        }

        private List<WriteBlockCallParams> WriteBlockCallParamsList
        {
            get
            {
                return (List<WriteBlockCallParams>)TestContext.Properties?[nameof(WriteBlockCallParamsList)];
            }
            set
            {
                TestContext.Properties[nameof(WriteBlockCallParamsList)] = value;
            }
        }

        public class SaveCheckpointThenGetCheckpointTestData
        {
            public string TestCaseDescription = "";
            public CheckpointBlockHelperOptions Options;
            public bool InjectMocksIntoOptions = true;
            public ModelCheckpoint Checkpoint = null;
            public bool UseLegacyBlockNames = false;
            public bool BadBlock = false;
            public int MaxBlockSizeInBytes = 4096;
            public int MinBlockSizeInBytes = 1;
            public bool ShuffleBlocks = false;
            public int ExpectedNumWriteBlockCalls = 0;
            public int ExpectedNumCommitBlocksCalls = 0;
            public int ExpectedNumExistsCalls = 0;
            public int ExpectedNumGetBlockInfoListCalls = 0;
            public int ExpectedNumReadBlockCalls = 0;
            public ModelCheckpoint ExpectedGetCheckpointReturnValue = null;

            /// <summary>
            /// Gets a string representation of the <see cref="SaveCheckpointThenGetCheckpointTestData"/> object.
            /// Only pertinent values used for inputs are present in the string.
            /// This makes it easier to identify individual tests cases from a data driven test
            /// without extra information.
            /// </summary>
            /// <returns>string representation with only pertinent values used as inputs.</returns>
            public override string ToString()
            {
                return $"Test '{TestCaseDescription}' with\n"
                    + $"     {nameof(Options)}: {Options}\n"
                    + $"     {nameof(Checkpoint)}: {Checkpoint}\n"
                    + $"     {nameof(UseLegacyBlockNames)}: {UseLegacyBlockNames}\n"
                    + $"     {nameof(ShuffleBlocks)}: {ShuffleBlocks}\n"
                    + $"     {nameof(MaxBlockSizeInBytes)}: {MaxBlockSizeInBytes}\n"
                    + $"     {nameof(MinBlockSizeInBytes)}: {MinBlockSizeInBytes}\n";
            }
        }

        internal class WriteBlockCallParams
        {
            internal string BlockId;
            internal byte[] Bytes;
        }

        [TestMethod]
        public void ConstructorExceptions()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                var _ = new CheckpointBlockHelper(null, NullLogger.Instance);
            });
        }

        [DataTestMethod]
        [DynamicData(nameof(GenerateSaveCheckpointThenGetCheckpointTestData), DynamicDataSourceType.Method)]
        public async Task SaveCheckpointThenGetCheckpointAsync(SaveCheckpointThenGetCheckpointTestData testData)
        {
            SetupSaveThenGetCheckpointMocks(testData);

            var helper = new CheckpointBlockHelper(testData.Options, NullLogger.Instance);
            CancellationToken cancelToken = new CancellationToken();

            await helper.SaveCheckpointAsync(testData.Checkpoint, DateTime.Now, cancelToken);
            if (testData.BadBlock)
            {
                InsertBadBlock();
            }

            if (testData.ShuffleBlocks)
            {
                WriteBlockCallParamsList.Shuffle();
            }

            var retrievedCheckpoint = await helper.GetCheckpointAsync(DateTime.Now, cancelToken);

            VerifyMocks(testData);
            Assert.That.AreCheckpointsEqual(testData.ExpectedGetCheckpointReturnValue, retrievedCheckpoint);
        }

        private static IEnumerable<object[]> GenerateSaveCheckpointThenGetCheckpointTestData()
        {
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "empty options",
                Options = new CheckpointBlockHelperOptions(),
                InjectMocksIntoOptions = false,
                Checkpoint = new ModelCheckpoint(),
            }};
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "null checkpoint",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                ExpectedNumExistsCalls = 1,
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "checkpoint with null model",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint(),
                ExpectedNumWriteBlockCalls = 1,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 1,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint()
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size 0",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = new byte[0] },
                ExpectedNumWriteBlockCalls = 1,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 1,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint()
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size 1",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(1) },
                ExpectedNumWriteBlockCalls = 2,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 2,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(1) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size 8",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(8) },
                ExpectedNumWriteBlockCalls = 2,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 2,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(8) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model block with legacy block names",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(8) },
                UseLegacyBlockNames = true,
                ExpectedNumWriteBlockCalls = 2,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 2,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(8) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size max - 1",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(4096 - 1) },
                ExpectedNumWriteBlockCalls = 2,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 2,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(4096 - 1) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size max",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(4096) },
                MaxBlockSizeInBytes = 4096,
                ExpectedNumWriteBlockCalls = 2,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 2,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(4096) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size max + 1",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(4096 + 1) },
                MaxBlockSizeInBytes = 4096,
                ExpectedNumWriteBlockCalls = 3,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 3,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(4096 + 1) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size 2*max - 1",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel((2 * 4096) - 1) },
                MaxBlockSizeInBytes = 4096,
                ExpectedNumWriteBlockCalls = 3,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 3,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel((2 * 4096) - 1) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size 2*max",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(2 * 4096) },
                MaxBlockSizeInBytes = 4096,
                ExpectedNumWriteBlockCalls = 3,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 3,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(2 * 4096) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size 2*max + 1",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel((2 * 4096) + 1) },
                MaxBlockSizeInBytes = 4096,
                ExpectedNumWriteBlockCalls = 4,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 4,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel((2 * 4096) + 1) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size max + min",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(256 + 2) },
                MaxBlockSizeInBytes = 256,
                MinBlockSizeInBytes = 2,
                ExpectedNumWriteBlockCalls = 3,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 3,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(256 + 2) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size max + min -1: adjust block size for min",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(257) },
                MinBlockSizeInBytes = 2,
                MaxBlockSizeInBytes = 256,
                ExpectedNumWriteBlockCalls = 3,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 3,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(257) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model size max + min -1: adjust block size for min",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(258) },
                MinBlockSizeInBytes = 3,
                MaxBlockSizeInBytes = 256,
                ExpectedNumWriteBlockCalls = 3,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = 3,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(258) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "model requires lots of block writes",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(4096) },
                MinBlockSizeInBytes = 2,
                MaxBlockSizeInBytes = 8,
                // block writes for model + block writes for checkpoint metadata.
                // + 1 since there is remainder on 140/8 that will require another write.
                ExpectedNumWriteBlockCalls = (4096/8) + (140/8) + 1,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = (4096/8) + (140/8) + 1,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(4096) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "checkpoint blocks out of order",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(4096) },
                ShuffleBlocks = true,
                MinBlockSizeInBytes = 2,
                MaxBlockSizeInBytes = 256,
                // block writes for model + block writes for checkpoint metadata.
                ExpectedNumWriteBlockCalls = (4096 / 256) + 1,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 1,
                ExpectedNumGetBlockInfoListCalls = 1,
                ExpectedNumReadBlockCalls = (4096 / 256) + 1,
                ExpectedGetCheckpointReturnValue = new ModelCheckpoint() { Model = CreateModel(4096) }
            } };
            yield return new object[] {
            new SaveCheckpointThenGetCheckpointTestData()
            {
                TestCaseDescription = "checkpoint with bad block",
                Options = new CheckpointBlockHelperOptions() { AppId = "MyFakeAppId" },
                Checkpoint = new ModelCheckpoint() { Model = CreateModel(4096) },
                BadBlock = true,
                MinBlockSizeInBytes = 2,
                MaxBlockSizeInBytes = 256,
                // block writes for model + block writes for checkpoint metadata.
                ExpectedNumWriteBlockCalls = (4096 / 256) + 1,
                ExpectedNumCommitBlocksCalls = 1,
                ExpectedNumExistsCalls = 5,
                ExpectedNumGetBlockInfoListCalls = 5,
                ExpectedNumReadBlockCalls = 0,
                ExpectedGetCheckpointReturnValue = null
            } };
        }

        private void SetupSaveThenGetCheckpointMocks(SaveCheckpointThenGetCheckpointTestData testData)
        {
            SetupCommonMocks();

            MockBlockStore.Setup(x => x.MinBlockSizeInBytes).Returns(testData.MinBlockSizeInBytes);
            MockBlockStore.Setup(x => x.MaxBlockSizeInBytes).Returns(testData.MaxBlockSizeInBytes);

            if (testData.InjectMocksIntoOptions)
            {
                testData.Options = InjectMocksIntoOptions(testData.Options);
            }

            WriteBlockCallParamsList = new List<WriteBlockCallParams>();
            MockBlockStore.Setup(x => x.WriteBlockAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<string, Stream, CancellationToken>(
                    (blockId, mstream, ct) =>
                    {
                        var callParams = new WriteBlockCallParams()
                        {
                            BlockId = blockId,
                            Bytes = new byte[mstream.Length]
                        };
                        mstream.Read(callParams.Bytes, 0, (int)mstream.Length);
                        WriteBlockCallParamsList.Add(callParams);
                    })
                .Returns(Task.CompletedTask);

            var blockIds = WriteBlockCallParamsList.Select(x => x.BlockId);
            if (testData.ShuffleBlocks)
            {
                MockBlockStore.Setup(x => x.CommitBlocksAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            }
            else
            {
                MockBlockStore.Setup(x => x.CommitBlocksAsync(blockIds, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            }

            // Blob only exists if we have saved to it.
            MockBlockStore.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult((WriteBlockCallParamsList?.Count ?? 0) > 0 ? true : false));


            MockBlockStore.Setup(x => x.GetBlockInfoListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(GetBlockInfoListFromWriteBlockCallParamsList(WriteBlockCallParamsList, testData.UseLegacyBlockNames)));

            int numGetRangeCalls = 0;
            MockBlockStore.Setup(x => x.ReadBlockAsync(It.IsAny<IBlockInfo>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<IBlockInfo, Stream, CancellationToken>(
                    (blockInfo, stream, ct) =>
                    {
                        var writeBlockCallParams = WriteBlockCallParamsList.Find(x => x.BlockId == blockInfo.Name);
                        // Write back what we saved in previous calls to WriteBlockAsync
                        Assert.AreEqual(writeBlockCallParams.Bytes.Length, blockInfo.SizeInBytes);
                        stream.Write(writeBlockCallParams.Bytes, 0, (int)blockInfo.SizeInBytes);
                        numGetRangeCalls++;
                    })
                .Returns(Task.CompletedTask);
        }

        private void SetupCommonMocks()
        {
            MockStoreProvider = new Mock<IBlockStoreProvider>(MockBehavior.Strict);
            MockBlockStore = new Mock<IBlockStore>(MockBehavior.Strict);

            MockStoreProvider.Setup(client => client.GetStore(It.IsAny<string>())).Returns(MockBlockStore.Object);
            MockBlockStore.Setup(x => x.Name).Returns("MyFakeBlobName");
            MockBlockStore.Setup(x => x.MinBlockSizeInBytes).Returns(1);
            MockBlockStore.Setup(x => x.MaxBlockSizeInBytes).Returns(4096);
        }

        private IEnumerable<IBlockInfo> GetBlockInfoListFromWriteBlockCallParamsList(IList<WriteBlockCallParams> p, bool useLegacyNames)
        {
            var infos = new List<IBlockInfo>();
            for (int index = 0; index < p.Count; index++)
            {
                infos.Add(GetBlockInfoFromWriteBlockCallParams(p[index], useLegacyNames, index));
            }

            return infos;
        }

        private IBlockInfo GetBlockInfoFromWriteBlockCallParams(WriteBlockCallParams p, bool useLegacyName, int blockNumber)
        {
            var mockBlockInfo = new Mock<IBlockInfo>();
            mockBlockInfo.Setup(x => x.Name).Returns(p.BlockId);
            if (useLegacyName)
            {
                mockBlockInfo.Setup(x => x.EncodedName).Returns($"{blockNumber:0000}");
            }
            else
            {
                mockBlockInfo.Setup(x => x.EncodedName).Returns(p.BlockId);
            }

            mockBlockInfo.Setup(x => x.SizeInBytes).Returns(p.Bytes.Length);
            return mockBlockInfo.Object;
        }

        private void VerifyMocks(SaveCheckpointThenGetCheckpointTestData testData)
        {
            MockBlockStore.Verify(x => x.WriteBlockAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(testData.ExpectedNumWriteBlockCalls));

            var blockIds = WriteBlockCallParamsList.Select(x => x.BlockId);
            if (testData.ShuffleBlocks || testData.BadBlock)
            {
                MockBlockStore.Verify(x => x.CommitBlocksAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(testData.ExpectedNumCommitBlocksCalls));
            }
            else
            {
                MockBlockStore.Verify(x => x.CommitBlocksAsync(blockIds, It.IsAny<CancellationToken>()), Times.Exactly(testData.ExpectedNumCommitBlocksCalls));
            }


            Assert.That.AllAreSameLength(blockIds);

            MockBlockStore.Verify(x => x.ExistsAsync(It.IsAny<CancellationToken>()), Times.Exactly(testData.ExpectedNumExistsCalls));
            MockBlockStore.Verify(x => x.GetBlockInfoListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(testData.ExpectedNumGetBlockInfoListCalls));
            MockBlockStore.Verify(x => x.ReadBlockAsync(It.IsAny<IBlockInfo>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(testData.ExpectedNumReadBlockCalls));
        }

        private CheckpointBlockHelperOptions InjectMocksIntoOptions(CheckpointBlockHelperOptions options)
        {
            options.BlockStoreProvider = MockStoreProvider.Object;
            return options;
        }

        private static byte[] CreateModel(int sizeInBytes)
        {
            return Enumerable.Range(0, sizeInBytes).Select(GenerateModelByteFromIndex).ToArray();
        }

        private static byte GenerateModelByteFromIndex(int modelIndex)
        {
            return (byte)(modelIndex % byte.MaxValue);
        }

        private void InsertBadBlock()
        {
            WriteBlockCallParamsList[WriteBlockCallParamsList.Count - 1] = new WriteBlockCallParams() { BlockId = "badbkFFFFFF", Bytes = new byte[] { 10, 11, 12 } };
        }
    }
}
