import axiosClient from '../api/axiosClient';
import { Sale, CreateSale } from '../types/sale';

const API_URL = '/api/sales';

const getSales = async (): Promise<Sale[]> => {
    const response = await axiosClient.get(API_URL);
    return response.data;
};

const createSale = async (sale: CreateSale): Promise<Sale> => {
    const response = await axiosClient.post(API_URL, sale);
    return response.data;
};

export const saleService = {
    getSales,
    createSale
};