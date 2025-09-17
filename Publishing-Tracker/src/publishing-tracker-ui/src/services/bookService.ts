import axios from 'axios';
import { Book, CreateBook, UpdateBook } from '../types/book';

const API_URL = import.meta.env.VITE_API_BASE_URL + '/api/books';

const getAuthToken = () => {
    return localStorage.getItem('token');
};

const getBooks = async (): Promise<Book[]> => {
    const token = getAuthToken();
    const response = await axios.get(API_URL, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const getBookById = async (id: number): Promise<Book> => {
    const token = getAuthToken();
    const response = await axios.get(`${API_URL}/${id}`, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const createBook = async (book: CreateBook): Promise<Book> => {
    const token = getAuthToken();
    const response = await axios.post(API_URL, book, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const updateBook = async (id: number, book: UpdateBook): Promise<Book> => {
    const token = getAuthToken();
    const response = await axios.put(`${API_URL}/${id}`, book, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const deleteBook = async (id: number): Promise<void> => {
    const token = getAuthToken();
    await axios.delete(`${API_URL}/${id}`, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
};

const getBookPerformance = async (id: number): Promise<any[]> => {
    const token = getAuthToken();
    const response = await axios.get(`${API_URL}/${id}/performance`, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
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