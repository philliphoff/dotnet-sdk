﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using NSubstitute;
using Xunit;
using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Client.Test
{
    public class ConfigurationSourceTest
    {
        private readonly string StoreName = "testStore";
        private readonly string SubscribeId = "testSubscribe";

        [Fact]
        public async Task TestStreamingConfigurationSourceCanBeRead()
        {
            // Standard values that we don't need to Mock.
            using var cts = new CancellationTokenSource();
            var streamRequest = new Autogenerated.SubscribeConfigurationRequest()
            {
                StoreName = StoreName
            };
            var callOptions = new CallOptions(cancellationToken: cts.Token);
            var item1 = new Autogenerated.ConfigurationItem()
            {
                Value = "testValue1",
                Version = "V1",
            };
            var item2 = new Autogenerated.ConfigurationItem()
            {
                Value = "testValue2",
                Version = "V1",
            };
            var responses = new List<Autogenerated.SubscribeConfigurationResponse>()
            {
                new Autogenerated.SubscribeConfigurationResponse() { Id = SubscribeId },
                new Autogenerated.SubscribeConfigurationResponse() { Id = SubscribeId },
            };
            responses[0].Items["testKey1"] = item1;
            responses[1].Items["testKey2"] = item2;

            // Setup the Mock and actions.
            var internalClient = Substitute.For<Autogenerated.Dapr.DaprClient>();
            var responseStream = new TestAsyncStreamReader<Autogenerated.SubscribeConfigurationResponse>(responses, TimeSpan.FromMilliseconds(100));
            var response = new AsyncServerStreamingCall<Autogenerated.SubscribeConfigurationResponse>(responseStream, null, null, null, async () => await Task.Delay(TimeSpan.FromMilliseconds(1)));
            internalClient.SubscribeConfiguration(Arg.Is<Autogenerated.SubscribeConfigurationRequest>(arg => arg == streamRequest), Arg.Is<CallOptions>(arg => EqualityComparer<CallOptions>.Default.Equals(arg, callOptions)))
                .Returns(response);

            // Try and actually use the source.
            var source = new SubscribeConfigurationResponse(new DaprSubscribeConfigurationSource(response));
            Dictionary<string, ConfigurationItem> readItems = new Dictionary<string, ConfigurationItem>();
            await foreach (var items in source.Source)
            {
                foreach (var item in items)
                {
                    readItems[item.Key] = new ConfigurationItem(item.Value.Value, item.Value.Version, item.Value.Metadata);
                }
            }

            var expectedItems = new Dictionary<string, ConfigurationItem>();
            expectedItems["testKey1"] = new ConfigurationItem("testValue1", "V1", null); 
            expectedItems["testKey2"] = new ConfigurationItem("testValue2", "V1", null); 
            Assert.Equal(SubscribeId, source.Id);
            Assert.Equal(expectedItems.Count, readItems.Count);
            // The gRPC metadata stops us from just doing the direct list comparison.

            var expectedConfigItem1 = expectedItems["testKey1"];
            var expectedConfigItem2 = expectedItems["testKey2"];
            var readConfigItem1 = expectedItems["testKey1"];
            var readConfigItem2 = expectedItems["testKey2"];

            Assert.Equal(expectedConfigItem1.Value, readConfigItem1.Value);
            Assert.Equal(expectedConfigItem1.Version, readConfigItem1.Version);
            Assert.Equal(expectedConfigItem1.Metadata, readConfigItem1.Metadata);
            Assert.Equal(expectedConfigItem2.Value, readConfigItem2.Value);
            Assert.Equal(expectedConfigItem2.Version, readConfigItem2.Version);
            Assert.Equal(expectedConfigItem2.Metadata, readConfigItem2.Metadata);
        }

        private class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
        {
            private IEnumerator<T> enumerator;
            private TimeSpan simulatedWaitTime;

            public TestAsyncStreamReader(IList<T> items, TimeSpan simulatedWaitTime)
            {
                this.enumerator = items.GetEnumerator();
                this.simulatedWaitTime = simulatedWaitTime;
            }

            public T Current => enumerator.Current;

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                // Add a little delay to pretend we're getting responses from a server stream.
                await Task.Delay(simulatedWaitTime, cancellationToken);
                return enumerator.MoveNext();
            }
        }
    }
}
