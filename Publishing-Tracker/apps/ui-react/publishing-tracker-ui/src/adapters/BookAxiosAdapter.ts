import axiosClient from '../api/axiosClient';
import { Book, CreateBook, UpdateBook, BookPerformance } from '../types/book';
import { IBookPort } from '../ports';

const API_URL = '/api/books';

export const BookAxiosAdapter: IBookPort = {
    getBooks: async (search?: string): Promise<Book[]> => {
        const response = await axiosClient.get(API_URL, { params: { search } });
        return response.data;
    },
    getBookById: async (id: number): Promise<Book> => {
        const response = await axiosClient.get(`${API_URL}/${id}`);
        return response.data;
    },
    createBook: async (book: CreateBook): Promise<Book> => {
        const response = await axiosClient.post(API_URL, book);
        return response.data;
    },
    updateBook: async (id: number, book: UpdateBook): Promise<Book> => {
        const response = await axiosClient.put(`${API_URL}/${id}`, book);
        return response.data;
    },
    deleteBook: async (id: number): Promise<void> => {
        await axiosClient.delete(`${API_URL}/${id}`);
    },
    getBookPerformance: async (id: number): Promise<BookPerformance[]> => {
        const response = await axiosClient.get(`${API_URL}/${id}/performance`);
        return response.data;
    }
};