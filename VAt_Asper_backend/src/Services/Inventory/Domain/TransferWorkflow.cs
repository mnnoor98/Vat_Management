using Vat.Domain;

namespace Vat.Services.Inventory;

public static class TransferWorkflow
{
    public static string Next(string state, string action, string maker, string actor, string? checker)
    {
        if (action == "approve" && maker == actor)
            throw new DomainException("self_approval", "A different person must approve the transfer.", 409);
        if (action == "post" && string.IsNullOrWhiteSpace(checker))
            throw new DomainException("approval_required", "An approved transfer is required.", 409);
        return (state, action) switch
        {
            ("Draft", "submit") => "Submitted",
            ("Submitted", "approve") => "Approved",
            ("Approved", "post") => "Posted",
            ("Draft" or "Submitted" or "Approved", "cancel") => "Cancelled",
            _ => throw new DomainException("invalid_transition", "The transfer is not in the required state.", 409)
        };
    }
}
