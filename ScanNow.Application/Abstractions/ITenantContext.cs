// ITenantContext lives in ScanNow.Domain.Abstractions so that Infrastructure
// (which does not reference Application) can implement it.
// Re-exported here for convenience so Application-layer code can use
// the same short namespace it already imports.
global using ITenantContext = ScanNow.Domain.Abstractions.ITenantContext;
