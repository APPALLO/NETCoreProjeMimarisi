# Order Service - Saga Orchestrator

Production-ready order service implementing Saga pattern for distributed transaction management across microservices.

## Saga Pattern Implementation

### Orchestration Flow
```
1. Create Order (Pending)
   ↓
2. Validate Inventory → domain.order.ValidateInventory
   ↓ (success)
3. Reserve Inventory → domain.order.ReserveInventory
   ↓ (success)
4. Process Payment → domain.order.ProcessPayment
   ↓ (success)
5. Order Confirmed → domain.order.OrderCompleted
```

### Compensation Flow (on failure)
```
Payment Failed
   ↓
Release Reserved Inventory → domain.order.ReleaseInventory
   ↓
Order Marked as Failed → domain.order.OrderFailed
```

## Saga State Machine

### States
- **Started**: Saga initiated
- **InProgress**: Steps executing
- **Completed**: All steps successful
- **Failed**: A step failed
- **Compensating**: Rolling back changes
- **Compensated**: Rollback complete

### Steps
1. **ValidateInventory**: Check if products are available
2. **ReserveInventory**: Lock inventory for this order
3. **ProcessPayment**: Charge customer
4. **Compensating**: Rollback on failure

## Events Published

### Order Events
- `domain.order.OrderCreated` - Order initiated
- `domain.order.OrderCompleted` - Order successful
- `domain.order.OrderFailed` - Order failed

### Command Events (to other services)
- `domain.order.ValidateInventory` → Inventory Service
- `domain.order.ReserveInventory` → Inventory Service
- `domain.order.ReleaseInventory` → Inventory Service (compensation)
- `domain.order.ProcessPayment` → Billing Service

## API Endpoints

```bash
POST   /api/orders              # Create order (starts saga)
GET    /api/orders/{id}         # Get order details
GET    /api/orders/user/{userId} # Get user's orders
GET    /api/orders/{id}/saga    # Get saga execution status
```

## Saga Tracking

Each order has an associated `OrderSaga` entity that tracks:
- Current step
- Step history with timestamps
- Success/failure status
- Compensation status

### Example Saga History
```json
{
  "id": "saga-guid",
  "orderId": "order-guid",
  "status": "Completed",
  "currentStep": "Completed",
  "history": [
    {
      "step": "ValidateInventory",
      "status": "Completed",
      "message": "Inventory validated",
      "timestamp": "2024-12-05T10:00:00Z"
    },
    {
      "step": "ReserveInventory",
      "status": "Completed",
      "message": "Inventory reserved",
      "timestamp": "2024-12-05T10:00:01Z"
    },
    {
      "step": "ProcessPayment",
      "status": "Completed",
      "message": "Payment processed",
      "timestamp": "2024-12-05T10:00:02Z"
    }
  ]
}
```

## Database Schema

### Orders Table
```sql
CREATE TABLE orders (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    status VARCHAR(50) NOT NULL,
    items JSONB NOT NULL,
    total_amount DECIMAL(18,2) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP
);
```

### OrderSagas Table
```sql
CREATE TABLE order_sagas (
    id UUID PRIMARY KEY,
    order_id UUID NOT NULL,
    status VARCHAR(50) NOT NULL,
    current_step VARCHAR(50) NOT NULL,
    history JSONB NOT NULL,
    created_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP
);
```

## Example Usage

### Create Order
```bash
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user-guid",
    "items": [
      {
        "productId": "product-guid",
        "productName": "Laptop",
        "quantity": 1,
        "price": 1299.99
      }
    ]
  }'
```

### Check Saga Status
```bash
curl http://localhost:5002/api/orders/{orderId}/saga
```

## Idempotency

All saga steps are idempotent:
- Events include `eventId` for deduplication
- Saga state prevents duplicate step execution
- Compensation can be retried safely

## Failure Scenarios

### Scenario 1: Insufficient Inventory
```
ValidateInventory → FAIL
  ↓
Order marked as Failed
No compensation needed (nothing reserved yet)
```

### Scenario 2: Payment Failed
```
ValidateInventory → SUCCESS
ReserveInventory → SUCCESS
ProcessPayment → FAIL
  ↓
Start Compensation
  ↓
ReleaseInventory → SUCCESS
  ↓
Order marked as Failed
```

### Scenario 3: Inventory Service Down
```
ValidateInventory → TIMEOUT
  ↓
Retry with exponential backoff (3 times)
  ↓
If still failing → DLQ
  ↓
Manual intervention or automatic retry later
```

## Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=orderdb;..."
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": "5672"
  },
  "Saga": {
    "TimeoutSeconds": 300,
    "RetryAttempts": 3,
    "RetryDelaySeconds": 5
  }
}
```

## Monitoring

### Key Metrics
- Saga completion rate
- Average saga duration
- Compensation rate
- Step failure rates
- Timeout occurrences

### Alerts
- Saga timeout > 5 minutes
- Compensation rate > 10%
- Step failure rate > 5%

## Trade-offs

### Eventual Consistency
- Order status may be "Pending" for seconds
- Clients should poll or use webhooks
- Consider WebSocket for real-time updates

### Complexity
- More code than 2PC (two-phase commit)
- Requires careful compensation logic
- Debugging distributed flows is harder

### Benefits
- No distributed locks
- Services remain independent
- Graceful degradation
- Better scalability

## Next Steps

- Add timeout handling for saga steps
- Implement retry with exponential backoff
- Add webhook notifications for order status
- Implement Outbox pattern for guaranteed delivery
- Add saga visualization dashboard
- Implement saga recovery on service restart
