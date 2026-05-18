import type { Cart } from '../types';
import { useClearCart } from '../hooks/useRemoveCartItem';

export function CartSummary({ cart }: { cart: Cart }) {
  const clear = useClearCart();
  return (
    <aside className="card p-4 sticky top-4">
      <h2 className="text-lg font-semibold mb-3">Order summary</h2>
      <div className="flex justify-between text-sm">
        <span>Items</span>
        <span>{cart.totalQuantity}</span>
      </div>
      <div className="flex justify-between text-sm mt-1">
        <span>Subtotal</span>
        <span className="font-semibold">{cart.currency} {cart.subtotal.toFixed(2)}</span>
      </div>
      <div className="mt-4 space-y-2">
        <button className="btn-primary w-full" disabled>Checkout (Phase 2)</button>
        <button className="btn-outline w-full" onClick={() => clear.mutate()} disabled={clear.isPending || cart.items.length === 0}>
          Clear cart
        </button>
      </div>
    </aside>
  );
}
