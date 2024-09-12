// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace Tests
{
    [TestClass]
    public class PathHelperTest
    {
        public static IEnumerable<object[]> GetParseIndexAndDateCases()
        {
            return new[] {
                new object[] { "/myappid/20180923171200/data/2018/06/30_02.json", 2, new DateTime(2018, 06, 30), 0 },
                new object[] { "/myappid/20180923171200/data/2020/02/02_0000000001_05.json", 1, new DateTime(2020, 02, 02), 5 },
                new object[] { "/myappid/20180923171200/data/2019/02/03_05.vw", 5, new DateTime(2019, 02, 03), 0 },
                new object[] { "/myappid/20180923171200/data/2019/02/03_05.fb", 5, new DateTime(2019, 02, 03), 0 },
                new object[] { "/myappid/20180923171200/data/2019/02/03_05.notdefined", 5, new DateTime(2019, 02, 03), 0 },
                new object[] { "/20180923171200/data/2019/02/03_05.notdefined", 5, new DateTime(2019, 02, 03), 0 },
                new object[] { "/20180923171200/2019/02/03_05.notdefined", 5, new DateTime(2019, 02, 03), 0 },
                new object[] { "/20180923171200/2019/02/03_05_01.notdefined", 5, new DateTime(2019, 02, 03), 1 },
                new object[] { "/20180923171200/2019/02/03_00000005_01.notdefined", 5, new DateTime(2019, 02, 03), 1 },
                new object[] { "/20180923171200/2019/02/03_00000005_01", 0, DateTime.MinValue, 0 },
                new object[] { "/20180923171200/2019", 0, DateTime.MinValue, 0 },
                new object[] { "garbage-chars/that-areN0TExpected90393.383", 0, DateTime.MinValue, 0 },
                new object[] { "", 0, DateTime.MinValue, 0 },
                new object[] { null, 0, DateTime.MinValue, 0 },
            };
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DynamicData(nameof(GetParseIndexAndDateCases), DynamicDataSourceType.Method)]
        public void ParseIndexAndDate_AllExtensions(string testPath, int testExpectedIndex, DateTime testExpectedDateTime, int textExpectedHour)
        {
            PathHelper.ParseIndexAndDate(testPath, out var index, out var hour, out var date);
            Assert.AreEqual(testExpectedIndex, index, $"failed to parse the index with path: {testPath}");
            Assert.AreEqual(textExpectedHour, hour, $"failed to parse the hour with path: {testPath}");
            Assert.AreEqual(testExpectedDateTime, date, $"failed to parse the date with path: {testPath}");
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void GetLogFormatExtension()
        {
            Assert.AreEqual("json", PathHelper.GetLogFormatExtension(JoinedLogFormat.DSJSON));
            Assert.AreEqual("fb", PathHelper.GetLogFormatExtension(JoinedLogFormat.Binary));
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void BuildBlobName_format()
        {
            var name = PathHelper.BuildBlobName(
                new DateTime(2019, 1, 1),
                new DateTime(2020, 2, 2),
                1,
                "subpath",
                JoinedLogFormat.DSJSON);

            Assert.AreEqual("20190101000000/subpath/2020/02/02_0000000001.json", name);

            name = PathHelper.BuildBlobName(
                            new DateTime(2019, 1, 1),
                            new DateTime(2020, 2, 2),
                            1,
                            "subpath",
                            JoinedLogFormat.Binary);

            Assert.AreEqual("20190101000000/subpath/2020/02/02_0000000001.fb", name);

            name = PathHelper.BuildBlobName(
                new DateTime(2019, 1, 1),
                new DateTime(2020, 2, 2),
                1,
                "subpath",
                JoinedLogFormat.DSJSON,
                5);

            Assert.AreEqual("20190101000000/subpath/2020/02/02_0000000001_05.json", name);
        }

        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow("../mydir/file.json", JoinedLogFormat.DSJSON)]
        [DataRow("../mydir/file.fb", JoinedLogFormat.Binary)]
        [DataRow("20210512165531/data/2021/05/12_0.json", JoinedLogFormat.DSJSON)]
        [DataRow("20210512165531/data/2021/05/12_0.fb", JoinedLogFormat.Binary)]

        public void JoinedLogFormat_From_Path(string path, JoinedLogFormat joinedLogFormat)
        {
            Assert.AreEqual(joinedLogFormat, PathHelper.GetLogFormatFromFilePath(path));
        }

        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow("json", JoinedLogFormat.DSJSON)]
        [DataRow("fb", JoinedLogFormat.Binary)]
        public void JoinedLogFormat_From_Extension(string path, JoinedLogFormat joinedLogFormat)
        {
            Assert.AreEqual(joinedLogFormat, PathHelper.GetLogFormatFromFileExtension(path));
        }

        [DataTestMethod]
        [DataRow("../mydir/file.unknown")]
        [DataRow("../mydir")]
        [DataRow(" ")]
        [DataRow("")]
        [DataRow(null)]
        [TestCategory("Decision Service/Online Trainer")]
        public void JoinedLogFormat_From_Path_ThrowsWhenInvalidFileExtension(string path)
        {
            Assert.ThrowsException<ArgumentException>(() => PathHelper.GetLogFormatFromFilePath(path));
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void JoinedLogFormat_From_Extension_ThrowsWhenInvalidFileExtension()
        {
            Assert.ThrowsException<ArgumentException>(() => PathHelper.GetLogFormatFromFileExtension("/not/an/extension.json"));
            Assert.ThrowsException<ArgumentException>(() => PathHelper.GetLogFormatFromFileExtension(" "));
            Assert.ThrowsException<ArgumentException>(() => PathHelper.GetLogFormatFromFileExtension(""));
            Assert.ThrowsException<ArgumentNullException>(() => PathHelper.GetLogFormatFromFileExtension(null));
        }
    }
}
