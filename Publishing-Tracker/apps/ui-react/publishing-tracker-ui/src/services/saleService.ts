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
    
    // Ensure all numeric values are properly formatted for .NET decimal parsing
    const formattedSale = {
        ...sale,
        bookId: Number(sale.bookId),
        platformId: Number(sale.platformId),
        quantity: Number(sale.quantity),
        unitPrice: Number(parseFloat(sale.unitPrice.toString()).toFixed(2)), // Ensure 2 decimal places
        royalty: Number(parseFloat(sale.royalty.toString()).toFixed(2)), // Ensure 2 decimal places
        saleDate: sale.saleDate instanceof Date
            ? sale.saleDate.toISOString() // Send as ISO string for .NET DateTime parsing
            : sale.saleDate
    };
    
    console.log('Sending formatted sale data:', formattedSale);
    
    const response = await axios.post(API_URL, formattedSale, {
        headers: {
            Authorization: `Bearer ${token}`,
            'Content-Type': 'application/json'
        }
    });
    return response.data;
};

export const saleService = {
    getSales,
    createSale
};