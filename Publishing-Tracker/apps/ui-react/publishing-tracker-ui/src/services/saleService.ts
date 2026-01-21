import axios from 'axios';
import { Sale, CreateSale } from '../types/sale';

const API_URL = import.meta.env.VITE_API_BASE_URL + '/api/sales';

const getAuthToken = () => {
    return localStorage.getItem('token');
};

const getSales = async (): Promise<Sale[]> => {
    const token = getAuthToken();
    const response = await axios.get(API_URL, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const createSale = async (sale: CreateSale): Promise<Sale> => {
    const token = getAuthToken();
    const response = await axios.post(API_URL, sale, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

export const saleService = {
    getSales,
    createSale
};