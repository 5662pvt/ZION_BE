export interface CartItem {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
  currency: string;
}

export interface Cart {
  id: string;
  userId: string | null;
  anonymousId: string | null;
  subtotal: number;
  currency: string;
  totalQuantity: number;
  items: CartItem[];
}
