// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;
using Moq;

namespace CommonTest
{
    public class OptionsSnapshotWrapper<T> : IOptionsSnapshot<T> where T: class, new()
    {
        Mock<IOptionsSnapshot<T>> mockObject;

        public OptionsSnapshotWrapper(T t)
        {
            Mock<IOptionsSnapshot<T>> mock = new Mock<IOptionsSnapshot<T>>();
            mock.Setup(m => m.Value).Returns(t);
            mockObject = mock;
        }

        public T Value => mockObject.Object.Value;

        public T Get(string name)
        {
            return mockObject.Object.Get(name);
        }
    }
}
