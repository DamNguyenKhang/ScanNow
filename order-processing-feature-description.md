# Order Processing Feature Description

## 1. Context

This feature belongs to a multi-restaurant management system with QR code ordering.

Main roles:

- `Customer`: scans QR code at a table and creates an order.
- `Waiter`: confirms customer orders and serves ready dishes.
- `Chef`: views kitchen tasks, prepares dishes, and marks dishes as ready.

The system should support efficient kitchen operation by allowing chefs to group the same dishes from different orders and process them together. Since dishes in the same order may not be completed at the same time, the system should track status at both order level and order item level.

---

## 2. Main Design Decision

The system should not only update the whole order status.

The system should maintain:

```text
Order.Status      = overall status of the order
OrderItem.Status  = actual processing status of each dish/item
```

Reason:

- Different dishes in the same order can be prepared at different times.
- Chef may group the same dishes from different orders to cook together.
- Waiter needs to know exactly which dishes are ready to serve.
- Customer does not need to see complex item-level statuses, but the backend should still track them.

---

## 3. Core Entities

### 3.1 Order

Represents one customer order.

Suggested fields:

```text
Id
RestaurantId
TableId
CustomerId nullable
OrderCode
Status
TotalAmount
Note
CreatedAt
ConfirmedAt
CompletedAt
CancelledAt
CreatedByUserId nullable
ConfirmedByUserId nullable
```

### 3.2 OrderItem

Represents one dish inside an order.

Suggested fields:

```text
Id
OrderId
MenuItemId
Quantity
UnitPrice
TotalPrice
Note
Status
Priority
EstimatedCookingMinutes
CreatedAt
ConfirmedAt
CookingStartedAt
ReadyAt
ServedAt
CancelledAt
```

Notes:

- `EstimatedCookingMinutes` should be copied from `MenuItem.AverageCookingTime` at the time the order item is created.
- Do not always read cooking time directly from `MenuItem` after order creation, because menu data may change later.
- `Priority` can be calculated based on waiting time, estimated cooking time, and order age.

### 3.3 MenuItem

Assumed existing entity.

Important field:

```text
AverageCookingTime
```

This value is used to estimate preparation time and calculate kitchen priority.

### 3.4 Optional KitchenBatch

For MVP, this table is optional.

The recommended MVP approach is:

```text
Do not create KitchenBatch table yet.
Group OrderItems dynamically in the backend query.
```

Create `KitchenBatch` only if the system later needs:

- Assign batch to specific chef.
- Track batch start and finish time.
- Print kitchen tickets.
- Split large quantities into multiple cooking rounds.
- Track chef performance.
- Support multiple kitchen stations more deeply.

---

## 4. Order Status

Suggested `OrderStatus` enum:

```text
PendingConfirmation
Confirmed
Preparing
PartiallyReady
ReadyToServe
PartiallyServed
Served
Completed
Cancelled
```

Meaning:

| Status | Description |
|---|---|
| PendingConfirmation | Customer submitted the order, waiting for waiter confirmation |
| Confirmed | Waiter confirmed the order |
| Preparing | At least one item is being prepared |
| PartiallyReady | Some items are ready, but not all |
| ReadyToServe | All active items are ready |
| PartiallyServed | Some items have been served, but not all |
| Served | All active items have been served |
| Completed | Order has been paid/completed |
| Cancelled | Entire order has been cancelled |

---

## 5. Order Item Status

Suggested `OrderItemStatus` enum:

```text
Pending
Confirmed
Cooking
Ready
Served
Cancelled
```

Meaning:

| Status | Description |
|---|---|
| Pending | Item was created but order has not been confirmed |
| Confirmed | Item was confirmed and is waiting for kitchen processing |
| Cooking | Chef has started preparing this item |
| Ready | Item is ready to be served |
| Served | Waiter has served this item |
| Cancelled | Item has been cancelled |

---

## 6. Status Transition Rules

### 6.1 OrderItem status flow

Normal flow:

```text
Pending
→ Confirmed
→ Cooking
→ Ready
→ Served
```

Cancel flow:

```text
Pending / Confirmed → Cancelled
```

Optional restricted cancel flow:

```text
Cooking / Ready / Served should not be cancellable by normal users.
```

If cancellation after cooking is required, it should need manager permission.

---

### 6.2 Order status should be calculated from OrderItems

`Order.Status` should be derived from active `OrderItems`, not manually updated everywhere.

Ignore cancelled items when calculating the general order progress.

Suggested logic:

```csharp
private OrderStatus CalculateOrderStatus(List<OrderItem> items)
{
    var activeItems = items.Where(x => x.Status != OrderItemStatus.Cancelled).ToList();

    if (!activeItems.Any())
        return OrderStatus.Cancelled;

    if (activeItems.All(x => x.Status == OrderItemStatus.Served))
        return OrderStatus.Served;

    if (activeItems.Any(x => x.Status == OrderItemStatus.Served))
        return OrderStatus.PartiallyServed;

    if (activeItems.All(x => x.Status == OrderItemStatus.Ready))
        return OrderStatus.ReadyToServe;

    if (activeItems.Any(x => x.Status == OrderItemStatus.Ready))
        return OrderStatus.PartiallyReady;

    if (activeItems.Any(x => x.Status == OrderItemStatus.Cooking))
        return OrderStatus.Preparing;

    if (activeItems.All(x => x.Status == OrderItemStatus.Confirmed))
        return OrderStatus.Confirmed;

    return OrderStatus.PendingConfirmation;
}
```

Important:

- After every order item status update, recalculate and update the parent order status.
- `Completed` should usually be set after payment, not only from item statuses.
- `Cancelled` for the whole order should be handled explicitly.

---

## 7. Main Business Flow

## 7.1 Customer creates order by QR code

Actor: `Customer`

Flow:

```text
1. Customer scans QR code at a table.
2. Customer selects dishes and quantity.
3. Customer submits the order.
4. System creates Order with status PendingConfirmation.
5. System creates OrderItems with status Pending.
```

Expected result:

```text
Order.Status = PendingConfirmation
OrderItem.Status = Pending
```

Validation:

- Table must belong to the restaurant.
- Restaurant must be active.
- Menu items must belong to the restaurant.
- Menu items must be available.
- Quantity must be greater than zero.
- Price should be copied from menu item at order time.
- Average cooking time should be copied from menu item at order time.

---

## 7.2 Waiter confirms order

Actor: `Waiter`

Flow:

```text
1. Waiter views pending orders.
2. Waiter checks table, items, notes, and availability.
3. Waiter confirms the order.
4. System updates order items from Pending to Confirmed.
5. System updates order to Confirmed.
6. Confirmed items become visible on kitchen screen.
```

Expected result:

```text
Order.Status = Confirmed
OrderItem.Status = Confirmed
```

Validation:

- Only waiter or manager can confirm.
- Order must currently be PendingConfirmation.
- Items must still be available.
- If an item is unavailable, waiter can reject/cancel that item before confirmation.

---

## 7.3 Chef views grouped kitchen items

Actor: `Chef`

This is the key feature for optimizing kitchen productivity.

The chef should not only see orders one by one.

The chef should see grouped dishes from all confirmed/cooking items.

Example source data:

```text
Order #001 - Table 01 - Chicken Rice x2
Order #002 - Table 03 - Chicken Rice x1
Order #003 - Table 05 - Chicken Rice x3
```

Kitchen grouped view:

```text
Chicken Rice - Total: 6 portions
- Table 01: x2
- Table 03: x1
- Table 05: x3
```

The backend should provide an API that returns grouped items.

Frontend should not be the only place that groups items.

Reason:

- Grouping is business logic.
- Backend must control status updates.
- Multiple chefs may use the kitchen screen at the same time.
- Backend must avoid duplicate processing or invalid status changes.

---

## 7.4 Chef starts cooking grouped items

Actor: `Chef`

Flow:

```text
1. Chef selects one grouped dish.
2. Chef clicks Start Cooking.
3. Frontend sends selected OrderItemIds to backend.
4. Backend validates that selected items are still Confirmed.
5. Backend updates selected OrderItems to Cooking.
6. Backend recalculates affected Orders.
```

Expected result:

```text
Selected OrderItems.Status = Cooking
Affected Orders.Status = Preparing
```

Validation:

- Only chef or manager can perform this action.
- Order items must belong to the same restaurant.
- Order items must be in Confirmed status.
- Order items must not already be Cooking, Ready, Served, or Cancelled.
- Use transaction to avoid partial update.

---

## 7.5 Chef marks grouped items as ready

Actor: `Chef`

Flow:

```text
1. Chef finishes preparing a grouped dish.
2. Chef clicks Mark Ready.
3. Frontend sends selected OrderItemIds to backend.
4. Backend validates that selected items are Cooking.
5. Backend updates selected OrderItems to Ready.
6. Backend recalculates affected Orders.
7. Ready items become visible to waiter.
```

Expected result:

```text
Selected OrderItems.Status = Ready
Affected Orders.Status = PartiallyReady or ReadyToServe
```

Validation:

- Only chef or manager can perform this action.
- Items should normally be in Cooking status.
- Use transaction.
- Update `ReadyAt`.

---

## 7.6 Waiter serves ready items

Actor: `Waiter`

Flow:

```text
1. Waiter views ready-to-serve items grouped by table/order.
2. Waiter brings dishes to the correct table.
3. Waiter marks selected items as Served.
4. Backend updates selected OrderItems to Served.
5. Backend recalculates affected Orders.
```

Expected result:

```text
OrderItem.Status = Served
Order.Status = PartiallyServed or Served
```

Validation:

- Only waiter or manager can mark served.
- Items must be Ready.
- Use transaction.
- Update `ServedAt`.

---

## 7.7 Order completion

Actor: `Waiter`, `Cashier`, or `Manager`

Flow:

```text
1. All active items are served.
2. Customer requests payment.
3. Payment is created and completed.
4. System updates Order.Status to Completed.
```

Expected result:

```text
Order.Status = Completed
```

Validation:

- Order should normally be Served before Completed.
- Payment must be successful.
- Completed order should not allow item status changes unless manager override exists.

---

## 8. Kitchen Grouping Logic

## 8.1 Recommended MVP grouping

Group order items by:

```text
RestaurantId
MenuItemId
OrderItem.Status
Note
```

Basic rule:

```text
Only group items with the same MenuItemId and same preparation note.
```

Reason:

The same dish with different cooking notes may require different preparation.

Example:

```text
Chicken Rice - normal
Chicken Rice - no onion
Chicken Rice - less spicy
```

These should be displayed as separate groups unless the business decides notes do not affect preparation.

---

## 8.2 Kitchen grouping query concept

The backend should query `OrderItems` where:

```text
OrderItem.Status IN (Confirmed, Cooking)
Order.Status NOT IN (Cancelled, Completed)
RestaurantId = current restaurant
```

The grouped response should include:

```text
MenuItemId
MenuItemName
TotalQuantity
Status
Note
AverageCookingTime
PriorityScore
SuggestedPriorityLevel
OldestCreatedAt
EstimatedFinishTime
Items[]
```

Each item in `Items[]` should include:

```text
OrderItemId
OrderId
OrderCode
TableId
TableName
Quantity
Note
Status
CreatedAt
ConfirmedAt
CookingStartedAt
EstimatedCookingMinutes
```

---

## 8.3 Example API response

```json
[
  {
    "menuItemId": 10,
    "menuItemName": "Chicken Rice",
    "status": "Confirmed",
    "note": "less spicy",
    "totalQuantity": 6,
    "averageCookingTime": 12,
    "priorityScore": 85,
    "suggestedPriorityLevel": "High",
    "oldestCreatedAt": "2026-05-24T10:00:00",
    "items": [
      {
        "orderItemId": 101,
        "orderId": 1,
        "orderCode": "ORD-001",
        "tableName": "Table 01",
        "quantity": 2,
        "note": "less spicy",
        "status": "Confirmed",
        "confirmedAt": "2026-05-24T10:01:00",
        "estimatedCookingMinutes": 12
      },
      {
        "orderItemId": 102,
        "orderId": 2,
        "orderCode": "ORD-002",
        "tableName": "Table 03",
        "quantity": 1,
        "note": "less spicy",
        "status": "Confirmed",
        "confirmedAt": "2026-05-24T10:02:00",
        "estimatedCookingMinutes": 12
      }
    ]
  }
]
```

---

## 9. Kitchen Priority Logic

The system should support priority sorting for chef.

Since `MenuItem.AverageCookingTime` already exists, use it to estimate urgency.

### 9.1 Priority factors

Recommended factors:

```text
WaitingTimeMinutes
AverageCookingTime
OrderAge
Quantity
ItemStatus
```

Where:

```text
WaitingTimeMinutes = now - OrderItem.ConfirmedAt
AverageCookingTime = OrderItem.EstimatedCookingMinutes
```

### 9.2 Simple priority formula

Suggested MVP formula:

```text
PriorityScore = WaitingTimeMinutes + AverageCookingTime
```

Meaning:

- Items waiting for a long time become more urgent.
- Items with longer cooking time should be started earlier.

### 9.3 Improved priority formula

If more control is needed:

```text
PriorityScore = (WaitingTimeMinutes * 1.5) + (AverageCookingTime * 1.0) + (Quantity * 0.5)
```

Suggested levels:

```text
Low:    PriorityScore < 20
Medium: 20 <= PriorityScore < 40
High:   PriorityScore >= 40
```

### 9.4 Group priority

For a grouped dish, calculate:

```text
GroupWaitingTime = now - oldest ConfirmedAt in group
GroupAverageCookingTime = average or max EstimatedCookingMinutes in group
GroupQuantity = total quantity
```

Recommended group priority formula:

```text
GroupPriorityScore = (GroupWaitingTime * 1.5) + (GroupAverageCookingTime * 1.0) + (GroupQuantity * 0.5)
```

Sort kitchen grouped items by:

```text
PriorityScore DESC
OldestConfirmedAt ASC
AverageCookingTime DESC
```

This helps the chef know which dish group should be started first.

---

## 10. Recommended API Endpoints

## 10.1 Customer APIs

### Create order

```http
POST /api/orders
```

Request:

```json
{
  "restaurantId": "uuid",
  "tableId": "uuid",
  "items": [
    {
      "menuItemId": "uuid",
      "quantity": 2,
      "note": "less spicy"
    }
  ],
  "note": "customer order note"
}
```

Behavior:

```text
Create order with PendingConfirmation.
Create order items with Pending.
Copy menu item price and average cooking time into order item.
```

---

## 10.2 Waiter APIs

### Get pending confirmation orders

```http
GET /api/waiter/orders/pending-confirmation?restaurantId={restaurantId}
```

### Confirm order

```http
POST /api/waiter/orders/{orderId}/confirm
```

Behavior:

```text
Order PendingConfirmation → Confirmed
OrderItems Pending → Confirmed
Set ConfirmedAt
```

### Get ready-to-serve items

```http
GET /api/waiter/items/ready-to-serve?restaurantId={restaurantId}
```

Recommended response grouping:

```text
Group by table and order.
Show only OrderItems with Ready status.
```

### Mark items as served

```http
POST /api/waiter/items/mark-served
```

Request:

```json
{
  "orderItemIds": ["uuid-1", "uuid-2"]
}
```

Behavior:

```text
OrderItems Ready → Served
Set ServedAt
Recalculate affected Order statuses
```

---

## 10.3 Kitchen APIs

### Get grouped kitchen items

```http
GET /api/kitchen/items/grouped?restaurantId={restaurantId}&status=Confirmed
```

Optional filters:

```text
status=Confirmed
status=Cooking
kitchenStationId
menuItemId
priorityLevel
```

Behavior:

```text
Return grouped OrderItems for chef screen.
Group by RestaurantId + MenuItemId + Status + Note.
Calculate total quantity and priority.
Sort by priority.
```

### Start cooking selected items

```http
POST /api/kitchen/items/start-cooking
```

Request:

```json
{
  "orderItemIds": ["uuid-1", "uuid-2"]
}
```

Behavior:

```text
OrderItems Confirmed → Cooking
Set CookingStartedAt
Recalculate affected Order statuses
```

### Mark selected items as ready

```http
POST /api/kitchen/items/mark-ready
```

Request:

```json
{
  "orderItemIds": ["uuid-1", "uuid-2"]
}
```

Behavior:

```text
OrderItems Cooking → Ready
Set ReadyAt
Recalculate affected Order statuses
```

---

## 10.4 Order detail API

### Get order detail

```http
GET /api/orders/{orderId}
```

Should return:

```text
Order information
Order items
Current status of each item
Payment summary if available
```

Customer UI can hide item-level status if desired.

---

## 11. Frontend Behavior

## 11.1 Customer screen

Customer should see simple statuses only:

```text
Waiting for confirmation
Preparing
Some dishes are ready
Ready to serve
Served
Completed
Cancelled
```

Customer does not need to see all backend item statuses unless the product requires it.

---

## 11.2 Waiter screen

Waiter should have:

```text
Pending confirmation orders
Ready-to-serve items grouped by table/order
Order detail screen
Mark served action
Cancel item/order action if allowed
```

Important waiter views:

```text
Table 01 - Order ORD-001
Ready:
- Chicken Rice x2
- Orange Juice x1
```

---

## 11.3 Chef screen

Chef should have:

```text
Grouped kitchen items
Priority score or priority label
Total quantity
Oldest waiting time
Estimated cooking time
List of related tables/orders
Start Cooking button
Mark Ready button
```

Suggested display:

```text
[High] Chicken Rice - 6 portions - Avg cooking: 12 mins - Waiting: 8 mins
- Table 01 / ORD-001: x2
- Table 03 / ORD-002: x1
- Table 05 / ORD-003: x3
```

Actions:

```text
Start Cooking
Mark Ready
View details
```

---

## 12. Backend Implementation Rules

## 12.1 Use transactions

Use database transaction for actions that update many order items:

```text
Confirm order
Start cooking grouped items
Mark items as ready
Mark items as served
Cancel order or item
```

Reason:

- Avoid partial update.
- Avoid inconsistent order status.

---

## 12.2 Validate status before transition

Example:

```text
Only Confirmed items can become Cooking.
Only Cooking items can become Ready.
Only Ready items can become Served.
```

Do not allow invalid transition:

```text
Pending → Ready
Confirmed → Served
Ready → Cooking
Served → Cancelled
```

Unless manager override is implemented.

---

## 12.3 Recalculate affected orders

After updating order items:

```text
1. Get distinct OrderIds from updated items.
2. Load active items for each order.
3. Calculate new Order.Status.
4. Save Order.Status.
```

---

## 12.4 Multi-restaurant safety

Every query and update must check `RestaurantId`.

Do not allow staff from one restaurant to update orders from another restaurant.

Required checks:

```text
Order.RestaurantId == currentUser.RestaurantId
OrderItem.Order.RestaurantId == currentUser.RestaurantId
MenuItem.RestaurantId == currentUser.RestaurantId
Table.RestaurantId == currentUser.RestaurantId
```

---

## 12.5 Concurrency protection

When multiple chefs use the kitchen screen at the same time, avoid double updates.

Recommended:

```text
Validate current status before update.
Update using WHERE Id IN (...) AND Status = expectedStatus.
Check affected rows count.
If affected rows count is smaller than requested item count, return conflict.
```

Example response:

```text
409 Conflict: Some items have already been updated by another user.
```

Optional:

```text
Add RowVersion / xmin / concurrency token.
```

For PostgreSQL, `xmin` or a manual `RowVersion` column can be used.

---

## 13. Cancellation Rules

Suggested MVP rules:

### Cancel whole order

Allowed when:

```text
Order.Status = PendingConfirmation or Confirmed
```

Not allowed when:

```text
Any item is Cooking, Ready, Served
```

Unless manager override exists.

### Cancel item

Allowed when:

```text
OrderItem.Status = Pending or Confirmed
```

Not allowed when:

```text
OrderItem.Status = Cooking, Ready, Served
```

Unless manager override exists.

After cancellation:

```text
Recalculate order total.
Recalculate order status.
```

If all items are cancelled:

```text
Order.Status = Cancelled
```

---

## 14. Edge Cases

### 14.1 Customer adds more items after order confirmed

Recommended approach:

```text
Create additional OrderItems under the same active Order.
New items should have Pending or Confirmed depending on business rule.
```

Safer MVP rule:

```text
New added items require waiter confirmation again.
New OrderItems.Status = Pending.
Order.Status can remain Preparing/PartiallyReady, but waiter should see new pending items.
```

Alternative:

```text
Create a new order for additional items from the same table.
```

For MVP, creating a new order is simpler.

---

### 14.2 Dish unavailable after customer submits order

Before waiter confirms:

```text
Waiter can remove/cancel unavailable item.
System recalculates total.
Customer may be notified.
```

---

### 14.3 Different notes for the same dish

Do not automatically group if preparation notes are different.

Example:

```text
Chicken Rice - normal
Chicken Rice - no onion
Chicken Rice - less spicy
```

These should appear as separate groups.

---

### 14.4 Partial serving

Allowed.

Example:

```text
Order has 3 items.
Only 1 item is Ready.
Waiter can serve that item first.
Order.Status = PartiallyServed.
```

---

### 14.5 Completed order

Once completed:

```text
Do not allow item status changes.
Do not allow cancellation.
Only manager can perform correction if required.
```

---

## 15. Suggested Backend Service Methods

Suggested services:

```text
OrderService
- CreateOrderAsync()
- ConfirmOrderAsync()
- GetOrderDetailAsync()
- CancelOrderAsync()
- RecalculateOrderStatusAsync()

KitchenService
- GetGroupedKitchenItemsAsync()
- StartCookingItemsAsync()
- MarkItemsReadyAsync()
- CalculatePriorityScore()

WaiterService
- GetPendingConfirmationOrdersAsync()
- GetReadyToServeItemsAsync()
- MarkItemsServedAsync()

OrderItemService
- UpdateItemStatusAsync()
- CancelItemAsync()
```

---

## 16. Suggested DTOs

### GroupedKitchenItemDto

```csharp
public class GroupedKitchenItemDto
{
    public Guid MenuItemId { get; set; }
    public string MenuItemName { get; set; }
    public string Status { get; set; }
    public string? Note { get; set; }
    public int TotalQuantity { get; set; }
    public int AverageCookingTime { get; set; }
    public double PriorityScore { get; set; }
    public string SuggestedPriorityLevel { get; set; }
    public DateTime? OldestConfirmedAt { get; set; }
    public List<GroupedKitchenOrderItemDto> Items { get; set; }
}
```

### GroupedKitchenOrderItemDto

```csharp
public class GroupedKitchenOrderItemDto
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; }
    public Guid TableId { get; set; }
    public string TableName { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int EstimatedCookingMinutes { get; set; }
}
```

### UpdateOrderItemsStatusRequest

```csharp
public class UpdateOrderItemsStatusRequest
{
    public List<Guid> OrderItemIds { get; set; }
}
```

---

## 17. Acceptance Criteria

### Order creation

- Customer can create an order from QR table.
- Order is created with `PendingConfirmation`.
- Order items are created with `Pending`.
- Price and estimated cooking time are copied from menu item.

### Waiter confirmation

- Waiter can view pending orders.
- Waiter can confirm order.
- Order items become `Confirmed`.
- Confirmed items appear on kitchen screen.

### Kitchen grouped view

- Chef can view confirmed items grouped by dish.
- Same dish with same note is grouped together.
- Different notes are displayed as separate groups.
- Group shows total quantity, related tables/orders, waiting time, estimated cooking time, and priority.

### Start cooking

- Chef can start cooking selected grouped items.
- Only `Confirmed` items can become `Cooking`.
- Affected order statuses are recalculated.

### Mark ready

- Chef can mark selected cooking items as ready.
- Only `Cooking` items can become `Ready`.
- Ready items appear on waiter ready-to-serve screen.
- Affected order statuses are recalculated.

### Mark served

- Waiter can mark ready items as served.
- Only `Ready` items can become `Served`.
- Affected order statuses are recalculated.

### Priority

- Kitchen groups are sorted by priority.
- Priority uses waiting time and average cooking time.
- Longer waiting and longer cooking time should increase priority.

### Security and restaurant isolation

- Staff can only access orders from their own restaurant.
- Customer can only create order for a valid table/menu item of that restaurant.

### Concurrency

- If another chef already updated an item, the API should return conflict instead of silently overriding data.

---

## 18. Recommended MVP Scope

Implement these first:

```text
1. Order and OrderItem statuses.
2. Customer creates order.
3. Waiter confirms order.
4. Backend grouped kitchen item API.
5. Chef starts cooking selected items.
6. Chef marks selected items as ready.
7. Waiter marks selected items as served.
8. Automatic order status recalculation.
9. Basic priority sorting.
```

Do not implement these in MVP unless needed:

```text
KitchenBatch table
Chef assignment
Kitchen station routing
Printed kitchen ticket
Advanced manager override
Detailed chef performance tracking
```

---

## 19. Final Recommended Flow

```text
Customer submits order
→ Order.Status = PendingConfirmation
→ OrderItems.Status = Pending

Waiter confirms order
→ Order.Status = Confirmed
→ OrderItems.Status = Confirmed

Chef sees grouped kitchen items
→ Group by MenuItemId + Status + Note
→ Sort by priority using waiting time and average cooking time

Chef starts cooking selected group/items
→ OrderItems.Status = Cooking
→ Order.Status = Preparing

Chef marks selected items ready
→ OrderItems.Status = Ready
→ Order.Status = PartiallyReady or ReadyToServe

Waiter serves ready items
→ OrderItems.Status = Served
→ Order.Status = PartiallyServed or Served

Payment completed
→ Order.Status = Completed
```

---

## 20. Important Notes For AI Agent

- Do not implement grouping only in frontend.
- Backend must expose grouped kitchen API.
- Frontend should only render grouped data and send selected `OrderItemIds` for actions.
- Do not update only `Order.Status`; always track `OrderItem.Status`.
- Recalculate `Order.Status` after every `OrderItem.Status` change.
- Use transaction for multi-item updates.
- Use restaurant isolation checks in every query/update.
- Use priority sorting based on waiting time and average cooking time.
- For MVP, dynamic grouping from `OrderItems` is enough. Do not create `KitchenBatch` unless the project already requires advanced kitchen tracking.
