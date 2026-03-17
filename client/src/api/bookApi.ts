import axios from 'axios';
import type { BookSearchRequest, BookSearchResponse } from '../types';

const api = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
});

export async function searchBooks(request: BookSearchRequest): Promise<BookSearchResponse> {
  const { data } = await api.post<BookSearchResponse>('/books/search', request);
  return data;
}
