import axiosClient from '../api/axiosClient';
import { Book, CreateBook, UpdateBook, BookPerformance } from '../types/book';

const API_URL = '/api/books';

const getBooks = async (): Promise<Book[]> => {
    const response = await axiosClient.get(API_URL);
    return response.data;
};

const getBookById = async (id: number): Promise<Book> => {
    const response = await axiosClient.get(`${API_URL}/${id}`);
    return response.data;
};

const createBook = async (book: CreateBook): Promise<Book> => {
    const response = await axiosClient.post(API_URL, book);
    return response.data;
};

const updateBook = async (id: number, book: UpdateBook): Promise<Book> => {
    const response = await axiosClient.put(`${API_URL}/${id}`, book);
    return response.data;
};

const deleteBook = async (id: number): Promise<void> => {
    await axiosClient.delete(`${API_URL}/${id}`);
};

const getBookPerformance = async (id: number): Promise<BookPerformance[]> => {
    const response = await axiosClient.get(`${API_URL}/${id}/performance`);
    return response.data;
};

export const bookService = {
    getBooks,
    getBookById,
    createBook,
    updateBook,
    deleteBook,
    getBookPerformance
};