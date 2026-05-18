import { useState } from 'react';
import type { CartItem } from '../types';
import { useRemoveCartItem } from '../hooks/useRemoveCartItem';
import { useUpdateCartItem } from '../hooks/useUpdateCartItem';

export function CartItemRow({ item }: { item: CartItem }) {
  const [qty, setQty] = useState(item.quantity);
  const update = useUpdateCartItem();
  const remove = useRemoveCartItem();

  return (
    <div className="card p-4 flex items-center justify-between gap-4">
      <div className="flex-1 min-w-0">
        <div className="font-medium truncate">{item.productName}</div>
        <div className="text-sm text-slate-500">{item.currency} {item.unitPrice.toFixed(2)} each</div>
      </div>
      <div className="flex items-center gap-2">
        <input
          type="number" min={1} max={100} value={qty}
          className="input w-20"
          onChange={(e) => setQty(Math.max(1, Number(e.target.value) || 1))}
          onBlur={() => qty !== item.quantity && update.mutate({ itemId: item.id, quantity: qty })}
        />
      </div>
      <div className="w-24 text-right font-semibold">{item.currency} {item.subtotal.toFixed(2)}</div>
      <button className="btn-outline text-red-600 border-red-300" onClick={() => remove.mutate(item.id)} disabled={remove.isPending}>
        Remove
      </button>
    </div>
  );
}
