using System;
using Moq;

namespace EDM.Tests
{
    /// <summary>
    /// Base class for all EDM unit tests providing common setup, teardown, and utilities.
    /// Uses IDisposable for xUnit setup/teardown semantics.
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        protected MockRepository MockRepository { get; private set; }

        protected TestBase()
        {
            MockRepository = new MockRepository(MockBehavior.Loose);
        }

        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Creates a loose mock that does not require strict verification.
        /// </summary>
        protected Mock<T> CreateMock<T>() where T : class
        {
            return new Mock<T>(MockBehavior.Loose);
        }

        /// <summary>
        /// Creates a strict mock that verifies all calls.
        /// </summary>
        protected Mock<T> CreateStrictMock<T>() where T : class
        {
            return new Mock<T>(MockBehavior.Strict);
        }
    }
}
