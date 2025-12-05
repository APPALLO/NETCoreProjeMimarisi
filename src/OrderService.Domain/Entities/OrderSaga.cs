namespace OrderService.Domain.Entities;

public class OrderSaga
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public SagaStatus Status { get; private set; }
    public SagaStep CurrentStep { get; private set; }
    public List<SagaStepHistory> History { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private OrderSaga() { } // EF Core

    public static OrderSaga Start(Guid orderId)
    {
        var saga = new OrderSaga
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = SagaStatus.Started,
            CurrentStep = SagaStep.ValidateInventory,
            CreatedAt = DateTime.UtcNow
        };

        saga.AddHistory(SagaStep.ValidateInventory, SagaStepStatus.Pending, "Saga started");
        return saga;
    }

    public void CompleteStep(SagaStep step, string message = "")
    {
        AddHistory(step, SagaStepStatus.Completed, message);
        CurrentStep = GetNextStep(step);

        if (CurrentStep == SagaStep.Completed)
        {
            Status = SagaStatus.Completed;
            CompletedAt = DateTime.UtcNow;
        }
    }

    public void FailStep(SagaStep step, string reason)
    {
        AddHistory(step, SagaStepStatus.Failed, reason);
        Status = SagaStatus.Failed;
        CurrentStep = SagaStep.Compensating;
    }

    public void StartCompensation()
    {
        Status = SagaStatus.Compensating;
        CurrentStep = SagaStep.Compensating;
        AddHistory(SagaStep.Compensating, SagaStepStatus.Pending, "Starting compensation");
    }

    public void CompleteCompensation()
    {
        Status = SagaStatus.Compensated;
        CurrentStep = SagaStep.Completed;
        CompletedAt = DateTime.UtcNow;
        AddHistory(SagaStep.Compensating, SagaStepStatus.Completed, "Compensation completed");
    }

    private void AddHistory(SagaStep step, SagaStepStatus status, string message)
    {
        History.Add(new SagaStepHistory
        {
            Step = step,
            Status = status,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    private static SagaStep GetNextStep(SagaStep current) => current switch
    {
        SagaStep.ValidateInventory => SagaStep.ReserveInventory,
        SagaStep.ReserveInventory => SagaStep.ProcessPayment,
        SagaStep.ProcessPayment => SagaStep.Completed,
        _ => SagaStep.Completed
    };
}

public class SagaStepHistory
{
    public SagaStep Step { get; set; }
    public SagaStepStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public enum SagaStatus
{
    Started,
    InProgress,
    Completed,
    Failed,
    Compensating,
    Compensated
}

public enum SagaStep
{
    ValidateInventory,
    ReserveInventory,
    ProcessPayment,
    Compensating,
    Completed
}

public enum SagaStepStatus
{
    Pending,
    Completed,
    Failed
}
