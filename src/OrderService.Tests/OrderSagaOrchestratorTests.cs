using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderService.Application.Commands;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Events;

namespace OrderService.Tests;

public class OrderSagaOrchestratorTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ISagaRepository> _mockSagaRepo;
    private readonly Mock<IEventPublisher> _mockEventPublisher;
    private readonly Mock<ILogger<OrderSagaOrchestrator>> _mockLogger;
    private readonly OrderSagaOrchestrator _orchestrator;

    public OrderSagaOrchestratorTests()
    {
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockSagaRepo = new Mock<ISagaRepository>();
        _mockEventPublisher = new Mock<IEventPublisher>();
        _mockLogger = new Mock<ILogger<OrderSagaOrchestrator>>();

        _orchestrator = new OrderSagaOrchestrator(
            _mockOrderRepo.Object,
            _mockSagaRepo.Object,
            _mockEventPublisher.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task StartOrderSagaAsync_ShouldCreateOrderAndPublishValidateInventory()
    {
        // Arrange
        var command = new CreateOrderCommand
        {
            UserId = Guid.NewGuid(),
            Items = new List<OrderItem>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 2, Price = 10, ProductName = "Test" }
            }
        };

        // Act
        var orderId = await _orchestrator.StartOrderSagaAsync(command);

        // Assert
        _mockOrderRepo.Verify(x => x.AddAsync(It.Is<Order>(o => o.Id == orderId), It.IsAny<CancellationToken>()), Times.Once);
        _mockSagaRepo.Verify(x => x.AddAsync(It.Is<OrderSaga>(s => s.OrderId == orderId), It.IsAny<CancellationToken>()), Times.Once);
        
        _mockEventPublisher.Verify(x => x.PublishAsync(
            "domain.order.ValidateInventory", 
            It.Is<ValidateInventoryCommand>(e => e.OrderId == orderId), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleInventoryValidatedAsync_WhenValid_ShouldPublishReserveInventory()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var saga = OrderSaga.Start(orderId);
        var order = Order.Create(Guid.NewGuid(), new List<OrderItem> { new() { ProductId = Guid.NewGuid(), Quantity = 1, Price = 10, ProductName = "Test" } });

        _mockSagaRepo.Setup(x => x.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saga);
        _mockOrderRepo.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _orchestrator.HandleInventoryValidatedAsync(orderId, true);

        // Assert
        saga.CurrentStep.Should().Be(SagaStep.ReserveInventory); // Actually it should be ValidateInventory completed, next step started.
        // The orchestrator doesn't update the "CurrentStep" enum explicitly in a state machine way, it just updates the step status.
        // But let's check if it published ReserveInventory
        
        _mockEventPublisher.Verify(x => x.PublishAsync(
            "domain.order.ReserveInventory", 
            It.Is<ReserveInventoryCommand>(e => e.OrderId == orderId), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleInventoryValidatedAsync_WhenInvalid_ShouldFailOrder()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), new List<OrderItem>());
        var orderId = order.Id;
        var saga = OrderSaga.Start(orderId);
        
        _mockSagaRepo.Setup(x => x.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saga);
        _mockOrderRepo.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        
        // Act
        await _orchestrator.HandleInventoryValidatedAsync(orderId, false);

        // Assert
        order.Status.Should().Be(OrderStatus.Failed);
        _mockOrderRepo.Verify(x => x.UpdateAsync(It.Is<Order>(o => o.Id == orderId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
