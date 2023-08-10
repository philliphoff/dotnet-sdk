// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Test.Runtime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors.Runtime;
    using FluentAssertions;
    using NSubstitute;
    using Xunit;

    public sealed class ActorTests
    {
        [Fact]
        public void TestNewActorWithMockStateManager()
        {
            var mockStateManager = Substitute.For<IActorStateManager>();
            var testDemoActor = this.CreateTestDemoActor(mockStateManager);
            testDemoActor.Host.Should().NotBeNull();
            testDemoActor.Id.Should().NotBeNull();
        }

        [Fact]
        public async Task TestSaveState()
        {
            var mockStateManager = Substitute.For<IActorStateManager>();
            mockStateManager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var testDemoActor = this.CreateTestDemoActor(mockStateManager);
            await testDemoActor.SaveTestState();
            await mockStateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task TestResetStateAsync()
        {
            var mockStateManager = Substitute.For<IActorStateManager>();
            mockStateManager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var testDemoActor = this.CreateTestDemoActor(mockStateManager);
            await testDemoActor.ResetTestStateAsync();
            await mockStateManager.Received(1).ClearCacheAsync(Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("NonExistentMethod", "Timer callback method: NonExistentMethod does not exist in the Actor class: TestActor")]
        [InlineData("TimerCallbackTwoArguments", "Timer callback can accept only zero or one parameters")]
        [InlineData("TimerCallbackNonTaskReturnType", "Timer callback can only return type Task")]
        [InlineData("TimerCallbackOverloaded", "Timer callback method: TimerCallbackOverloaded cannot be overloaded.")]
        public void ValidateTimerCallback_CallbackMethodDoesNotMeetRequirements(string callback, string expectedErrorMessage)
        {
            var mockStateManager = Substitute.For<IActorStateManager>();
            mockStateManager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var testDemoActor = this.CreateTestDemoActor(mockStateManager);

            ;
            FluentActions.Invoking(() =>
                testDemoActor.ValidateTimerCallback(testDemoActor.Host, callback))
            .Should().Throw<ArgumentException>()
            .WithMessage(expectedErrorMessage);
        }

        [Theory]
        [InlineData("TimerCallbackPrivate")]
        [InlineData("TimerCallbackProtected")]
        [InlineData("TimerCallbackInternal")]
        [InlineData("TimerCallbackPublicWithNoArguments")]
        [InlineData("TimerCallbackPublicWithOneArgument")]
        [InlineData("TimerCallbackStatic")]
        public void ValidateTimerCallback_CallbackMethodMeetsRequirements(string callback)
        {
            var mockStateManager = Substitute.For<IActorStateManager>();
            mockStateManager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var testDemoActor = this.CreateTestDemoActor(mockStateManager);

            ;
            FluentActions.Invoking(() =>
                testDemoActor.ValidateTimerCallback(testDemoActor.Host, callback))
            .Should().NotThrow();
        }

        [Theory]
        [InlineData("TimerCallbackPrivate")]
        [InlineData("TimerCallbackPublicWithOneArgument")]
        [InlineData("TimerCallbackStatic")]
        public void GetMethodInfoUsingReflection_MethodsMatchingBindingFlags(string callback)
        {
            var mockStateManager = Substitute.For<IActorStateManager>();
            mockStateManager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var testDemoActor = this.CreateTestDemoActor(mockStateManager);
            var methodInfo = testDemoActor.GetMethodInfoUsingReflection(testDemoActor.Host.ActorTypeInfo.ImplementationType, callback);
            Assert.NotNull(methodInfo);
        }

        [Theory]
        [InlineData("TestActor")] // Constructor
        public void GetMethodInfoUsingReflection_MethodsNotMatchingBindingFlags(string callback)
        {
            var mockStateManager = Substitute.For<IActorStateManager>();
            mockStateManager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var testDemoActor = this.CreateTestDemoActor(mockStateManager);
            var methodInfo = testDemoActor.GetMethodInfoUsingReflection(testDemoActor.Host.ActorTypeInfo.ImplementationType, callback);
            Assert.Null(methodInfo);
        }

        /// <summary>
        /// On my test code I want to pass the mock statemanager all the time.
        /// </summary>
        /// <param name="actorStateManager">Mock StateManager.</param>
        /// <returns>TestActor.</returns>
        private TestActor CreateTestDemoActor(IActorStateManager actorStateManager)
        {
            var host = ActorHost.CreateForTest<TestActor>();
            var testActor = new TestActor(host, actorStateManager);
            return testActor;
        }

    }
}
