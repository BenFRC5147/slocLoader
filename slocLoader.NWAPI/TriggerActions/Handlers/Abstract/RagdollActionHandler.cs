﻿using slocLoader.TriggerActions.Data;
using slocLoader.TriggerActions.Enums;

namespace slocLoader.TriggerActions.Handlers.Abstract;

public abstract class RagdollActionHandler<TData> : ITriggerActionHandler where TData : BaseTriggerActionData
{

    public TargetType Targets => TargetType.Ragdoll;

    public abstract TriggerActionType ActionType { get; }

    public void HandleObject(GameObject interactingObject, BaseTriggerActionData data, TriggerListener listener)
    {
        if (data is TData t && interactingObject.TryGetComponent(out BasicRagdoll toy))
            HandleRagdoll(toy, t);
    }

    protected abstract void HandleRagdoll(BasicRagdoll player, TData data);

}
