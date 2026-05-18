export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
  errors: ApiError[];
  pagination: Pagination | null;
}

export interface ApiError {
  field?: string | null;
  code: string;
  message: string;
}

export interface Pagination {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
