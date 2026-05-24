namespace ScanNow.Domain.Enums
{
    public enum OrderStatus
    {
        PendingConfirmation,
        Confirmed,
        Preparing,
        PartiallyReady,
        ReadyToServe,
        PartiallyServed,
        Served,
        Completed,
        Cancelled
    }
}
