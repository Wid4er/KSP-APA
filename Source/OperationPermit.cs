using System;

namespace AirParkStability
{
    internal enum OriginalOperation { Park, Unpark }

    // A synchronous, instance-bound, single-use authorization for each protected method.
    // Never persisted, never carried into a later physics update, never a global bypass flag.
    internal sealed class OperationPermit
    {
        private object target;
        private OriginalOperation operation;
        private bool toggleAvailable, transitionAvailable;

        public void Invoke(object module, OriginalOperation op, Action original)
        {
            if (target != null) throw new InvalidOperationException("Nested AirPark authorization.");
            target = module;
            operation = op;
            toggleAvailable = op == OriginalOperation.Park;
            transitionAvailable = true;
            try { original(); }
            finally { target = null; toggleAvailable = transitionAvailable = false; }
        }

        public bool ConsumeToggle(object module)
        {
            if (!ReferenceEquals(target, module) || !toggleAvailable) return false;
            toggleAvailable = false;
            return true;
        }

        public bool ConsumeTransition(object module, OriginalOperation op)
        {
            if (!ReferenceEquals(target, module) || operation != op || !transitionAvailable) return false;
            transitionAvailable = false;
            return true;
        }

        public bool AllowsStill(object module)
        {
            return ReferenceEquals(target, module) && !transitionAvailable;
        }
    }
}
