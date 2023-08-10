using System;
using System.Threading.Tasks;
using Dapr.Workflow;
using NSubstitute;
using WorkflowConsoleApp.Activities;
using WorkflowConsoleApp.Models;
using WorkflowConsoleApp.Workflows;
using Xunit;

namespace WorkflowUnitTest
{
    [Trait("Example", "true")]
    public class OrderProcessingTests
    {
        [Fact]
        public async Task TestSuccessfulOrder()
        {
            // Test payloads
            OrderPayload order = new(Name: "Paperclips", TotalCost: 99.95m, Quantity: 10);
            InventoryResult inventoryResult = new(Success: true, new InventoryItem(order.Name, 9.99m, order.Quantity));

            // Mock the call to ReserveInventoryActivity
            var mockContext = Substitute.For<WorkflowContext>();
            mockContext
                .CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), Arg.Any<InventoryRequest>(), Arg.Any<WorkflowTaskOptions>())
                .Returns(Task.FromResult(inventoryResult));

            // Run the workflow directly
            OrderResult result = await new OrderProcessingWorkflow().RunAsync(mockContext, order);
            
            // Verify that workflow result matches what we expect
            Assert.NotNull(result);
            Assert.True(result.Processed);

            // Verify that ReserveInventoryActivity was called with a specific input
            await mockContext
                .Received(1)
                .CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), Arg.Is<InventoryRequest>(request => request.ItemName == order.Name && request.Quantity == order.Quantity), Arg.Any<WorkflowTaskOptions>());

            // Verify that ProcessPaymentActivity was called with a specific input
            await mockContext
                .Received(1)
                .CallActivityAsync(nameof(ProcessPaymentActivity), Arg.Is<PaymentRequest>(request => request.ItemName == order.Name && request.Amount == order.Quantity && request.Currency == order.TotalCost), Arg.Any<WorkflowTaskOptions>());

            // Verify that there were two calls to NotifyActivity
            await mockContext
                .Received(2)
                .CallActivityAsync(nameof(NotifyActivity), Arg.Any<Notification>(), Arg.Any<WorkflowTaskOptions>());
        }

        [Fact]
        public async Task TestInsufficientInventory()
        {
            // Test payloads
            OrderPayload order = new(Name: "Paperclips", TotalCost: 99.95m, Quantity: int.MaxValue);
            InventoryResult inventoryResult = new(Success: false, null);

            // Mock the call to ReserveInventoryActivity
            var mockContext = Substitute.For<WorkflowContext>();
            mockContext
                .CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), Arg.Any<InventoryRequest>(), Arg.Any<WorkflowTaskOptions>())
                .Returns(Task.FromResult(inventoryResult));

            // Run the workflow directly
            OrderResult result = await new OrderProcessingWorkflow().RunAsync(mockContext, order);

            // Verify that ReserveInventoryActivity was called with a specific input
            await mockContext
                .Received(1)
                .CallActivityAsync<InventoryResult>(nameof(ReserveInventoryActivity), Arg.Is<InventoryRequest>(request => request.ItemName == order.Name && request.Quantity == order.Quantity), Arg.Any<WorkflowTaskOptions>());

            // Verify that ProcessPaymentActivity was never called
            await mockContext
                .DidNotReceive()
                .CallActivityAsync(nameof(ProcessPaymentActivity), Arg.Any<PaymentRequest>(), Arg.Any<WorkflowTaskOptions>());

            // Verify that there were two calls to NotifyActivity
            await mockContext
                .Received(2)
                .CallActivityAsync(nameof(NotifyActivity), Arg.Any<Notification>(), Arg.Any<WorkflowTaskOptions>());
        }
    }
}
