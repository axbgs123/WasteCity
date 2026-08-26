using System;
using WasteCity.Economy;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public interface IGrayboxConstructionPaymentPolicy3D
    {
        bool CanFundConstruction(
            CityResourceStorageModel storage,
            string resourceId,
            int amount);

        bool TryCommitConstructionCost(
            CityResourceStorageModel storage,
            string resourceId,
            int amount,
            out GrayboxConstructionPaymentReceipt3D receipt,
            out string error);

        bool TryRollbackConstructionCost(
            GrayboxConstructionPaymentReceipt3D receipt,
            out string error);
    }

    public sealed class GrayboxConstructionPaymentReceipt3D
    {
        internal GrayboxConstructionPaymentReceipt3D(
            GrayboxVoidDebtController3D owner,
            CityResourceStorageModel storage,
            string resourceId,
            int cashSpent,
            FormalVoidDebtSnapshot debtBefore,
            FormalVoidDebtSnapshot debtAfter,
            ulong storageRevisionAfter)
        {
            Owner = owner;
            Storage = storage;
            ResourceId = resourceId;
            CashSpent = cashSpent;
            DebtBefore = debtBefore;
            DebtAfter = debtAfter;
            StorageRevisionAfter = storageRevisionAfter;
        }

        internal GrayboxVoidDebtController3D Owner { get; }
        internal CityResourceStorageModel Storage { get; }
        internal string ResourceId { get; }
        internal int CashSpent { get; }
        internal FormalVoidDebtSnapshot DebtBefore { get; }
        internal FormalVoidDebtSnapshot DebtAfter { get; }
        internal ulong StorageRevisionAfter { get; }
        internal bool RolledBack { get; set; }
    }

    public sealed class GrayboxVoidDebtController3D :
        IGrayboxConstructionPaymentPolicy3D,
        ICityResourceCreditHook,
        IDisposable
    {
        private readonly FormalFateRuntime fate;
        private readonly FormalVoidDebtRuntime debt;
        private CityResourceStorageModel storage;
        private Func<bool> creditSuppression;
        private int internalCreditSuppressionDepth;

        public GrayboxVoidDebtController3D(
            FormalFateRuntime fate,
            FormalVoidDebtRuntime debt)
        {
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.debt = debt ?? throw new ArgumentNullException(nameof(debt));
        }

        public bool IsCreditSuppressed =>
            internalCreditSuppressionDepth > 0 ||
            creditSuppression?.Invoke() == true;

        public void Bind(
            CityResourceStorageModel storage,
            Func<bool> creditSuppression)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));
            if (ReferenceEquals(this.storage, storage))
            {
                this.creditSuppression = creditSuppression;
                if (!storage.TrySetCreditHook(this))
                    throw new InvalidOperationException(
                        "The city storage credit hook is owned elsewhere.");
                return;
            }

            Unbind();
            if (!storage.TrySetCreditHook(this))
                throw new InvalidOperationException(
                    "The city storage credit hook is owned elsewhere.");
            this.storage = storage;
            this.creditSuppression = creditSuppression;
        }

        public void Dispose()
        {
            Unbind();
        }

        public bool CanFundConstruction(
            CityResourceStorageModel storage,
            string resourceId,
            int amount)
        {
            if (!ReferenceEquals(this.storage, storage) ||
                !ResourceDefinitionCatalog.TryGet(resourceId, out _) ||
                amount < 0)
            {
                return false;
            }
            return amount == 0 || storage.CanSpendFromNetwork(
                resourceId,
                amount) || IsVoidDebtActive();
        }

        public bool TryCommitConstructionCost(
            CityResourceStorageModel storage,
            string resourceId,
            int amount,
            out GrayboxConstructionPaymentReceipt3D receipt,
            out string error)
        {
            receipt = null;
            if (!CanFundConstruction(storage, resourceId, amount))
            {
                error = "Construction cost cannot be funded.";
                return false;
            }

            int available = storage.GetNetworkAmount(resourceId);
            int cashSpent = Math.Min(amount, available);
            int borrowed = amount - cashSpent;
            if (borrowed > 0 && !IsVoidDebtActive())
            {
                error = "Only selected void debt construction can borrow.";
                return false;
            }

            FormalVoidDebtSnapshot debtBefore = debt.Capture();
            if (cashSpent > 0 && !storage.TrySpendFromNetwork(
                    resourceId,
                    cashSpent))
            {
                error = "Construction cash payment failed.";
                return false;
            }
            if (borrowed > 0 && !debt.TryBorrowConstruction(
                    resourceId,
                    borrowed,
                    out string debtError))
            {
                if (!RefundWithoutCredit(storage, resourceId, cashSpent))
                    error = debtError + " Construction cash rollback failed.";
                else error = debtError;
                return false;
            }

            receipt = new GrayboxConstructionPaymentReceipt3D(
                this,
                storage,
                resourceId,
                cashSpent,
                debtBefore,
                debt.Capture(),
                storage.Revision);
            error = string.Empty;
            return true;
        }

        public bool TryRollbackConstructionCost(
            GrayboxConstructionPaymentReceipt3D receipt,
            out string error)
        {
            if (receipt == null ||
                !ReferenceEquals(receipt.Owner, this) ||
                receipt.RolledBack ||
                !ReferenceEquals(receipt.Storage, storage))
            {
                error = "Construction payment receipt is invalid.";
                return false;
            }
            if (storage.Revision != receipt.StorageRevisionAfter ||
                !ReferenceEquals(debt.Capture(), receipt.DebtAfter))
            {
                error = "Construction payment changed before rollback.";
                return false;
            }
            if (!RefundWithoutCredit(
                    storage,
                    receipt.ResourceId,
                    receipt.CashSpent))
            {
                error = "Construction cash rollback failed.";
                return false;
            }
            if (!debt.TryRestore(receipt.DebtBefore, out error))
            {
                if (receipt.CashSpent > 0)
                    storage.TrySpendFromNetwork(
                        receipt.ResourceId,
                        receipt.CashSpent);
                return false;
            }
            receipt.RolledBack = true;
            error = string.Empty;
            return true;
        }

        public int GetRepaymentAmount(
            string resourceId,
            int requestedAmount)
        {
            if (IsCreditSuppressed || !IsVoidDebtActive() ||
                requestedAmount <= 0)
            {
                return 0;
            }
            return Math.Min(requestedAmount, debt.GetDebt(resourceId));
        }

        public bool TryCommitRepayment(
            string resourceId,
            int amount,
            out object rollbackState,
            out string error)
        {
            error = string.Empty;
            rollbackState = debt.Capture();
            if (amount <= 0 ||
                !debt.Repay(
                    resourceId,
                    amount,
                    out int repaid,
                    out int residual,
                    out error) ||
                repaid != amount || residual != 0)
            {
                debt.TryRestore((FormalVoidDebtSnapshot)rollbackState, out _);
                rollbackState = null;
                if (string.IsNullOrEmpty(error))
                    error = "Void debt repayment did not consume its credit.";
                return false;
            }
            return true;
        }

        public bool TryRollbackRepayment(object rollbackState, out string error)
        {
            if (!(rollbackState is FormalVoidDebtSnapshot snapshot))
            {
                error = "Void debt repayment rollback state is invalid.";
                return false;
            }
            return debt.TryRestore(snapshot, out error);
        }

        private void Unbind()
        {
            storage?.ClearCreditHook(this);
            storage = null;
            creditSuppression = null;
            internalCreditSuppressionDepth = 0;
        }

        private bool IsVoidDebtActive()
        {
            FormalFateSnapshot snapshot = fate.Capture();
            return snapshot.HasSelection &&
                string.Equals(
                    snapshot.SelectedId,
                    FormalFateCatalog.VoidDebtId,
                    StringComparison.Ordinal) &&
                snapshot.Level == debt.Level &&
                (snapshot.Level == 1 || snapshot.Level == 2);
        }

        private bool RefundWithoutCredit(
            CityResourceStorageModel target,
            string resourceId,
            int amount)
        {
            if (amount <= 0) return true;
            internalCreditSuppressionDepth++;
            try
            {
                return target.AddToNetwork(resourceId, amount) == amount;
            }
            finally
            {
                internalCreditSuppressionDepth--;
            }
        }

    }
}
