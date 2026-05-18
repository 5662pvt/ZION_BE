export interface ProductSummary {
  id: string;
  name: string;
  slug: string;
  sku: string;
  price: number;
  currency: string;
  status: string;
  categoryName: string | null;
  primaryImageUrl: string | null;
}

export interface ProductImage {
  id: string;
  url: string;
  alt: string | null;
  displayOrder: number;
}

export interface ProductDetail {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  sku: string;
  price: number;
  currency: string;
  status: string;
  categoryId: string | null;
  categoryName: string | null;
  brand: string | null;
  images: ProductImage[];
}

export interface CategoryNode {
  id: string;
  name: string;
  slug: string;
  displayOrder: number;
  children: CategoryNode[];
}
