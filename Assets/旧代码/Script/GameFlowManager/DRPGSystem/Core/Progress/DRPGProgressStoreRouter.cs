using System;
using System.Collections.Generic;

public sealed class DRPGProgressStoreRouter
{
    private readonly Dictionary<DRPGProgressScope, IDRPGProgressStore> stores =
        new Dictionary<DRPGProgressScope, IDRPGProgressStore>();

    public DRPGProgressStoreRouter(
        IEnumerable<IDRPGProgressStore> stores = null)
    {
        if (stores == null)
            return;

        foreach (IDRPGProgressStore store in stores)
        {
            if (store == null)
            {
                throw new ArgumentException(
                    "Progress stores cannot contain null.",
                    nameof(stores));
            }
            if (this.stores.ContainsKey(store.Scope))
            {
                throw new ArgumentException(
                    "Duplicate progress store scope: " + store.Scope,
                    nameof(stores));
            }
            this.stores.Add(store.Scope, store);
        }
    }

    public DRPGProgressMutationResult Apply(
        DRPGProgressMutationRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (!stores.TryGetValue(request.Scope, out IDRPGProgressStore store))
        {
            return DRPGProgressMutationResult.Reject(
                request,
                DRPGProgressMutationState.RejectedUnsupportedScope,
                "No progress store is registered for scope " +
                request.Scope + ".");
        }

        try
        {
            return store.Apply(request);
        }
        catch (Exception exception)
        {
            return DRPGProgressMutationResult.Reject(
                request,
                DRPGProgressMutationState.RejectedInvalidRequest,
                exception.Message);
        }
    }

    public bool TryGetStore(
        DRPGProgressScope scope,
        out IDRPGProgressStore store)
    {
        return stores.TryGetValue(scope, out store);
    }
}
