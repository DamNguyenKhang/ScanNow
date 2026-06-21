# Huong dan Tich hop SignalR: Trang thai don hang

Tai lieu nay mo ta contract realtime cho man hinh khach hang theo doi don da dat. Hub nay tach khoi `/hubs/cart`, vi gio hang va trang thai don hang co vong doi khac nhau.

## Ket noi

- Endpoint: `<NEXT_PUBLIC_API_URL>/hubs/orders`
- Client: `@microsoft/signalr`
- Authentication: khong can JWT; `sessionCode` hop le duoc dung de kiem tra don thuoc phien ban hien tai.

```typescript
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/hubs/orders`)
  .withAutomaticReconnect()
  .build();

await connection.start();
```

## Payload Cong Khai

```typescript
type CustomerOrderResponse = {
  orderId: string;
  orderNumber: string;
  status: OrderStatus;
  subTotal: number;
  vatAmount: number;
  serviceChargeAmount: number;
  totalAmount: number;
  createdAt: string;
  updatedAt: string | null;
  items: Array<{
    orderItemId: string;
    menuItemId: string;
    menuItemName: string;
    unitPrice: number;
    quantity: number;
    subTotal: number;
    note: string | null;
    estimatedCookingMinutes: number;
  }>;
};
```

Payload customer khong chua `OrderItemStatus`. UI hien thi tien trinh tong quat theo `OrderStatus`.

## Methods Va Event

### `JoinOrder(sessionCode, orderId)`

Goi sau khi nhan `orderId` tu API dat mon, hoac khi mo lai trang theo doi. Server chi cho join neu session con hieu luc va order la active order cua session. Method tra snapshot hien tai:

```typescript
const order = await connection.invoke<CustomerOrderResponse>("JoinOrder", sessionCode, orderId);
```

### `OrderUpdated`

Dang ky event de cap nhat cache/UI ngay khi trang thai thay doi:

```typescript
connection.on("OrderUpdated", (order: CustomerOrderResponse) => {
  queryClient.setQueryData(["public-order", sessionCode, order.orderId], current => {
    if (!current) return current;
    return { ...current, data: { ...current.data, result: order } };
  });
});
```

Backend phat event sau cac thay doi da luu thanh cong: dat/them mon hop le, xac nhan, bat dau nau, danh dau san sang, phuc vu, huy don, thanh toan tien mat, va PayOS duoc polling xac nhan thanh cong.

### `LeaveOrder(orderId)`

Goi khi unmount man hinh theo doi:

```typescript
await connection.invoke("LeaveOrder", orderId);
await connection.stop();
```

## Reconnect Va Thanh Toan

- Sau `onreconnected`, client goi lai `JoinOrder(sessionCode, orderId)` de nhan snapshot moi nhat.
- Nut refresh thu cong van co the goi `GET /api/public/sessions/{sessionCode}/orders/{orderId}` lam fallback.
- Voi PayOS, client tiep tuc polling `GET /api/public/sessions/{sessionCode}/payment-status` trong luc payment dang pending. Khi polling ghi nhan thanh cong, backend cap nhat order thanh `Completed` va phat `OrderUpdated`; client dung polling khi da nhan `Completed`.
